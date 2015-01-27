using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using MT4CliWrapper;
using System.Threading;
using CSRedis;
using NLog;
using MT4Proxy.NET.EventArg;
using MT4Proxy.NET.Service;
using System.Data.SQLite;
using System.Dynamic;
using Castle.Zmq;
using System.Web.Script.Serialization;
using Newtonsoft.Json;


namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 单线程的复制系统
    /// </summary>
    class CopyServer : ConfigBase, IServer
    {
        private static Context _zmqContext = new Context();
        private static JavaScriptSerializer _jss = new JavaScriptSerializer();

        public static event EventHandler<TradeInfoEventArgs> OnNewTrade = null;
        internal override void LoadConfig(NLog.Internal.ConfigurationManager aConfig)
        {
            Enable = bool.Parse(aConfig.AppSettings["enable_copy"]);
            RedisCopyActivityTemplate = aConfig.AppSettings["redis_copy_activity_seq_template"];
            InnerServer = aConfig.AppSettings["copy_inner_server"];
            CopyCheckAddr = aConfig.AppSettings["copy_check_server"];
        }
        private static bool Enable
        {
            get;
            set;
        }

        private static string InnerServer
        {
            get;
            set;
        }

        private static string RedisCopyActivityTemplate
        {
            get;
            set;
        }

        private static string CopyCheckAddr
        {
            get;
            set;
        }

        public CopyServer()
        {
            Source = new DockServer();
        }

        private bool EnableRunning;

        public void Initialize()
        {
            var logger = Utils.CommonLog;
            if(!Enable)
            {
                logger.Info("复制服务被禁用,启动但不处理复制关系");
            }
            EnableRunning = true;
            PumpServer.OnNewTrade += WhenNewTrade;
            if (_thProc == null)
            {
                _thProc = new Thread(CopyProc);
                _thProc.Name = "CopyServer";
                _thProc.IsBackground = true;
                _thProc.Start();
            }
            OpenAccount.OnCreateUser += WhenCreateUser;
            ServerContainer.ForkServer<SaveServer>();
            logger.Info("复制服务已经完成启动");
        }

        void WhenCreateUser(object sender, CreateUserEventArgs e)
        {
            try
            {
                var dock = new DockServer();
                using (var db = dock.CopySource)
                {
                    var sql = "REPLACE INTO user(mt4 ,user_code) VALUES(@mt4_id, @user_code)";
                    using (var cmd = new SQLiteCommand(sql, db))
                    {
                        cmd.Parameters.AddWithValue("@mt4_id", e.MT4ID);
                        cmd.Parameters.AddWithValue("@user_code", e.Usercode);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception exp)
            {
                Utils.CommonLog.Error("保存开户邀请码出错,MT4:{0},usercode:{1},{2},{3}",
                    e.MT4ID, e.Usercode, exp.Message, exp.StackTrace);
            }
        }

        public void Stop()
        {
            EnableRunning = false;
            PumpServer.OnNewTrade -= WhenNewTrade;
            OpenAccount.OnCreateUser -= WhenCreateUser;
        }

        public void WhenNewTrade(object sender, TradeInfoEventArgs e)
        {
            _queNewTrades.Enqueue(e);
            _signal.Release();
        }

        private void CopyProc()
        {
            TradeInfoEventArgs item = null;
            var sql = string.Empty;
            while (Utils.SignalWait(ref EnableRunning, _signal))
            {
                _queNewTrades.TryDequeue(out item);
                try
                {
                    var trade_type = item.TradeType;
                    var trade = item.Trade;
                    var key = string.Empty;
                    var handler = OnNewTrade;
                    //if (handler != null)
                    //    handler(this, item);
                    //continue;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(InnerServer))
                        {
                            sql = "SELECT user_code FROM user WHERE mt4=@mt4_id";
                            using (var cmd = new SQLiteCommand(sql, Source.CopySource))
                            {
                                cmd.Parameters.AddWithValue("@mt4_id", item.Trade.login);
                                var user_code_obj = cmd.ExecuteScalar();
                                var user_code = string.Empty;
                                if (user_code_obj != null)
                                    user_code = user_code_obj.ToString();
                                item.Usercode = user_code;
                            }
                            sql = "SELECT mt4_from FROM copy_order WHERE order_to=@order_id";
                            using (var cmd = new SQLiteCommand(sql, Source.CopySource))
                            {
                                cmd.Parameters.AddWithValue("@order_id", item.Trade.order);
                                var from_obj = cmd.ExecuteScalar();
                                if (from_obj != null)
                                {
                                    item.FromUsercode = from_obj.ToString();
                                    item.ToUsercode = item.Usercode;
                                }
                            }
                        }
                        else
                        {
                            using (var reqSocket = _zmqContext.CreateSocket(SocketType.Req))
                            {
                                reqSocket.Connect(InnerServer);
                                dynamic reqBody = new ExpandoObject();
                                reqBody.__api = "InnerSearchUsercode";
                                reqBody.mt4_id = trade.login;
                                reqBody.order_id = trade.order;
                                var req = JsonConvert.SerializeObject(reqBody) as string;
                                reqSocket.Send(req);
                                var repBody = _jss.Deserialize<dynamic>(reqSocket.RecvString());
                                var code = Convert.ToInt32(repBody["ucode"]);
                                if (code != 0) item.Usercode = code.ToString();
                                var from_code = Convert.ToInt32(repBody["from_ucode"]);
                                if (from_code != 0)
                                {
                                    item.FromUsercode = from_code.ToString();
                                    item.ToUsercode = code.ToString();
                                }
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        Utils.CommonLog.Error("组装订单出现问题，订单号{0},{1},{2}",
                            trade.order, e.Message, e.StackTrace);
                    }
                    if (handler != null)
                        handler(this, item);
                    if ((trade_type == TRANS_TYPE.TRANS_ADD || trade_type == TRANS_TYPE.TRANS_UPDATE) &&
                        trade.cmd <= (int)Utils.TRADE_COMMAND.OP_SELL)
                    {
                        var apiEquity = Poll.New();
                        var equity = 0.0;
                        var balance = 0.0;
                        var free = 0.0;
                        apiEquity.GetEquity(trade.login, ref equity, ref free, ref balance);
                        Poll.Release(apiEquity);
                        dynamic reqBody = new ExpandoObject();
                        reqBody.mt4_id = trade.login;
                        reqBody.balance = balance;
                        reqBody.__api = "InnerCheckCopyBalance";
                        using (var reqSocket = _zmqContext.CreateSocket(SocketType.Req))
                        {
                            reqSocket.Connect(CopyCheckAddr);
                            var req = JsonConvert.SerializeObject(reqBody) as string;
                            reqSocket.Send(req);
                            reqSocket.RecvString();
                        }
                    }
                    if (!Enable) continue;
                    if (trade_type == TRANS_TYPE.TRANS_ADD &&
                        trade.comment == "copy") continue;
                    Task.Factory.StartNew(() => { StartLink(trade_type, trade); });
                }
                catch(Exception e)
                {
                    Utils.CommonLog.Error(string.Format("复制服务出错,{0},{1}",
                        e.Message, e.StackTrace));
                }
            }
            ServerContainer.FinishStop();
        }

        private void StartLink(TRANS_TYPE trade_type, TradeRecordResult trade)
        {
            var dock = new DockServer();
            try
            {
                using (var db = dock.CopySource)
                {
                    var sql = string.Empty;
                    var mt4_from = trade.login;
                    var jss = new JavaScriptSerializer();
                    if ((trade_type == TRANS_TYPE.TRANS_ADD || trade_type == TRANS_TYPE.TRANS_UPDATE) && 
                        trade.cmd <= (int)Utils.TRADE_COMMAND.OP_SELL)
                    {
                        var trade_date = trade.timestamp.FromTime32();
                        Utils.CommonLog.Info("MT4:{0}的订单{1}已收到",
                            mt4_from, trade.order);
                        try
                        {
                            sql = "SELECT * FROM copy WHERE from_mt4=@mt4_id";
                            var lst = new List<dynamic>();
                            using (var cmd = new SQLiteCommand(sql, db))
                            {
                                cmd.Parameters.AddWithValue("@mt4_id", mt4_from);
                                using (var copy_reader = cmd.ExecuteReader())
                                {
                                    while (copy_reader.Read())
                                    {
                                        dynamic copy_item = new ExpandoObject();
                                        copy_item.from_mt4 = int.Parse(copy_reader["from_mt4"]
                                            .ToString());
                                        copy_item.to_mt4 = int.Parse(copy_reader["to_mt4"]
                                            .ToString());
                                        copy_item.source = int.Parse(copy_reader["source"]
                                            .ToString());
                                        copy_item.amount = double.Parse(copy_reader["amount"]
                                            .ToString());
                                        lst.Add(copy_item);
                                    }
                                }
                            }
                            var real_copy = lst.Where(i => i.source == 0);
                            var demo_copy = lst.Where(i => i.source != 0);
                            if (lst.Count > 0)
                            {
                                MT4Wrapper api = null;
                                var equity = 0.0;
                                var balance = 0.0;
                                var free = 0.0;
                                using (api = Poll.New())
                                {
                                    api.GetEquity(mt4_from, ref equity, ref free, ref balance);
                                }

                                if (real_copy.Count() > 0)
                                {
                                    using (api = Poll.New())
                                    {
                                        CopyNewTrade(trade, mt4_from, real_copy,
                                            trade_date, api, balance, dock, 0);
                                    }
                                }
                                if (demo_copy.Count() > 0)
                                {
                                    using (api = Poll.DemoAPI())
                                    {
                                        CopyNewTrade(trade, mt4_from, demo_copy,
                                            trade_date, api, balance, dock, 1);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Utils.CommonLog.Error("执行copy功能出现故障,订单号{0},{1},{2}",
                                trade.order, e.Message, e.StackTrace);
                        }
                    }
                    if (trade_type == TRANS_TYPE.TRANS_DELETE && !string.IsNullOrWhiteSpace(trade.symbol))
                    {
                        try
                        {
                            sql = "SELECT * FROM copy_order WHERE order_from=@order_id";
                            var lst = new List<dynamic>();
                            using (var cmd = new SQLiteCommand(sql, db))
                            {
                                cmd.Parameters.AddWithValue("@order_id", trade.order);
                                using (var copy_reader = cmd.ExecuteReader())
                                {
                                    while (copy_reader.Read())
                                    {
                                        dynamic copy_item = new ExpandoObject();
                                        copy_item.mt4_from = int.Parse(copy_reader["mt4_from"]
                                            .ToString());
                                        copy_item.mt4_to = int.Parse(copy_reader["mt4_to"]
                                            .ToString());
                                        copy_item.source = int.Parse(copy_reader["source"]
                                            .ToString());
                                        copy_item.order_from = int.Parse(copy_reader["order_from"]
                                            .ToString());
                                        copy_item.order_to = int.Parse(copy_reader["order_to"]
                                            .ToString());
                                        copy_item.direction = int.Parse(copy_reader["direction"]
                                            .ToString());
                                        copy_item.volume = int.Parse(copy_reader["volume"]
                                             .ToString());
                                        lst.Add(copy_item);
                                    }
                                }
                            }
                            if (lst.Count > 0)
                            {
                                Utils.CommonLog.Info("可复制订单{0}已平仓，copy数量{1}",
                                    trade.order, lst.Count);
                            }
                            var real_copy = lst.Where(i => i.source == 0);
                            var demo_copy = lst.Where(i => i.source != 0);
                            MT4Wrapper api = null;
                            if (real_copy.Count() > 0)
                                using (api = Poll.New())
                                {
                                    CopyBalanceTrade(trade, mt4_from, real_copy, api,
                                        dock, 0);
                                }
                            if (demo_copy.Count() > 0)
                                using (api = Poll.DemoAPI())
                                {
                                    CopyBalanceTrade(trade, mt4_from, demo_copy, api,
                                        dock, 1);
                                }
                        }
                        catch (Exception e)
                        {
                            Utils.CommonLog.Error("执行copy平仓功能出现故障,订单号{0},{1},{2}",
                                trade.order, e.Message, e.StackTrace);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Utils.CommonLog.Error(string.Format("复制StartLink出错,{0},{1}",
                    e.Message, e.StackTrace));
            }
        }

        private static void CopyBalanceTrade(TradeRecordResult trade,
            int mt4_from, IEnumerable<dynamic> items,
            MT4Wrapper api, DockServer dock, int source)
        {
            var key = string.Empty;
            foreach (var i in items)
            {
                var copy_order = 0;
                try
                {
                    copy_order = i.order_to;
                    var volume = i.volume;
                    var to_source = i.source == 0 ? "real" : "demo";
                    var mt4_to = i.mt4_to;
                    Utils.CommonLog.Info("自动平掉复制订单{0}的copy订单{1}，成交量{2},位置{3}",
                        trade.order, copy_order, volume, to_source);
                    var args = new TradeTransInfoArgsResult
                    {
                        type = TradeTransInfoTypes.TT_BR_ORDER_CLOSE,
                        cmd = (short)trade.cmd,
                        order = copy_order,
                        price = trade.close_price,
                        volume = volume,
                    };
                    var result = api.TradeTransaction(ref args);
                    if (result == RET_CODE.RET_OK)
                    {
                        var sql = string.Empty;
                        sql = "INSERT INTO pre_delete(order_from) VALUES(@order_from)";
                        using (var cmd = new SQLiteCommand(sql, dock.CopySource))
                        {
                            cmd.Parameters.AddWithValue("@order_from", trade.order);
                            cmd.ExecuteNonQuery();
                        }
                        double amount = 0.0;
                        sql = "SELECT amount FROM copy WHERE from_mt4 = @mt4_from AND to_mt4 = @mt4_to";
                        using (var cmd = new SQLiteCommand(sql, dock.CopySource))
                        {
                            cmd.Parameters.AddWithValue("@mt4_from", mt4_from);
                            cmd.Parameters.AddWithValue("@mt4_to", mt4_to);
                            var amount_obj = cmd.ExecuteScalar();
                            if (amount_obj == null)
                            {
                                Utils.CommonLog.Warn("收到一个很奇怪的需要结算盈利的copy单，找不到copy表记录" +
                                    "单号{0}, MT4{1}", trade.order, trade.login);
                                continue;
                            }
                            else
                            {
                                amount = Convert.ToDouble(amount_obj);
                            }

                        }
                        //算钱模式
                        var order = api.AdmTradesRequest(copy_order, false);
                        if (order.profit + order.storage + amount < 0)
                            amount = 0.0;
                        else
                            amount += trade.profit + trade.storage;
                        sql = "UPDATE copy SET amount=@amount WHERE from_mt4=@mt4_from AND to_mt4=@mt4_to";
                        using (var cmd = new SQLiteCommand(sql, dock.CopySource))
                        {
                            cmd.Parameters.AddWithValue("@mt4_from", mt4_from);
                            cmd.Parameters.AddWithValue("@mt4_to", mt4_to);
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.ExecuteNonQuery();
                        }
                        sql = "SELECT user_code FROM user WHERE mt4=@mt4_id";
                        using (var cmd = new SQLiteCommand(sql, dock.CopySource))
                        {
                            cmd.Parameters.AddWithValue("@mt4_id", mt4_to);
                            var user_code_obj = cmd.ExecuteScalar();
                            if(user_code_obj != null)
                            {
                                key = string.Format(RedisCopyActivityTemplate, trade.login);
                                dock.RedisCopy.ZAdd(key, TimeServer.Now.ToTime32().ToString(), 
                                    user_code_obj.ToString());
                                Utils.CommonLog.Info("自动平掉复制订单{0}的copy订单{1}成功",
                                    trade.order, copy_order);
                            }
                            else
                            {
                                Utils.CommonLog.Info("Copy平仓复制订单{0}的copy订单{1}无法反查邀请码信息，MT4账号{2}",
                                    trade.order, copy_order, mt4_to);
                            }
                        }
                        
                    }
                    else
                    {
                        Utils.CommonLog.Warn("自动平掉复制订单{0}的copy订单{1}失败，原因{2}",
                            trade.order, copy_order, result);
                    }
                }
                catch (Exception e)
                {
                    Utils.CommonLog.Error("子订单{0}平仓出现问题{1},{2}",
                        copy_order, e.Message, e.StackTrace);
                }
            }
        }
        
        private static void CopyNewTrade(TradeRecordResult trade, 
            int mt4_from, IEnumerable<dynamic> items, DateTime trade_date,
            MT4Wrapper api, double balance, DockServer dock, int source)
        {
            foreach (var i in items)
            {
                var mt4_to = 0;
                try
                {
                    mt4_to = i.to_mt4;
                    var to_source = i.source;
                    var amount = i.amount;
                    Utils.CommonLog.Info("将[MT4:{0}]的订单{1}复制给[MT4:{2}]",
                        mt4_from, trade.order, mt4_to);
                    var volume = ForexMath.CopyTransform(balance, trade.volume, amount);
                    var now = TimeServer.Now;
                    if (to_source == 0 && Math.Abs((now - trade_date).TotalSeconds) > 1)
                    {
                        Utils.CommonLog.Info("将[MT4:{0}]的订单{1}复制给[MT4:{2}],时间过长,单据时间:{3},此时:{4}",
                        mt4_from, trade.order, mt4_to, trade_date, now);
                        continue;
                    }
                    if (volume <= 0)
                    {
                        Utils.CommonLog.Info("将[MT4:{0}]的订单{1}复制给[MT4:{2}]，复制单成交量过小，跳过复制",
                            mt4_from, trade.order, mt4_to);
                        continue;
                    }
                    var args = new TradeTransInfoArgsResult
                    {
                        type = TradeTransInfoTypes.TT_BR_ORDER_OPEN,
                        cmd = (short)trade.cmd,
                        orderby = mt4_to,
                        price = trade.open_price,
                        symbol = trade.symbol,
                        volume = volume,
                        comment = "copy",
                    };
                    var result = api.TradeTransaction(ref args);
                    if (result == RET_CODE.RET_OK)
                    {
                        var sql = 
                            "INSERT INTO copy_order(mt4_from, mt4_to, order_from, order_to," + 
                            "direction, volume, source) VALUES(@mt4_from, @mt4_to, @order_from," + 
                            "@order_to, @direction, @volume, @source)";
                        using(var cmd = new SQLiteCommand(sql, dock.CopySource))
                        {
                            cmd.Parameters.AddWithValue("@mt4_from", mt4_from);
                            cmd.Parameters.AddWithValue("@mt4_to", mt4_to);
                            cmd.Parameters.AddWithValue("@order_from", trade.order);
                            cmd.Parameters.AddWithValue("@order_to", args.order);
                            cmd.Parameters.AddWithValue("@direction", args.cmd);
                            cmd.Parameters.AddWithValue("@volume", volume);
                            cmd.Parameters.AddWithValue("@source", source);
                            cmd.ExecuteNonQuery();
                        }
                        Utils.CommonLog.Info("将[MT4:{0}]的订单{1}复制给[MT4:{2}]，复制成功",
                            mt4_from, trade.order, mt4_to);
                    }
                    else
                    {
                        Utils.CommonLog.Warn("将[MT4:{0}]的订单{1}复制给[MT4:{2}]，复制结果失败，原因:{5}",
                            mt4_from, trade.order, mt4_to, result);
                    }
                }
                catch (Exception e)
                {
                    Utils.CommonLog.Error("Copy订单{0}复制给MT4[{1}]出现问题{2},{3}",
                        trade.order, mt4_to, e.Message, e.StackTrace);
                }
            }
        }
        
        private Thread _thProc = null;
        private ConcurrentQueue<TradeInfoEventArgs> _queNewTrades =
            new ConcurrentQueue<TradeInfoEventArgs>();
        private DockServer Source
        {
            get;
            set;
        }
        private NameValueCollection _collection = new NameValueCollection();
        private System.Threading.Semaphore _signal = new System.Threading.Semaphore(0, 20000);
    }
}
