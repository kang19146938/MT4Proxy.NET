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


namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 单线程的复制系统
    /// </summary>
    class CopyServer : ConfigBase, IServer
    {
        public static event EventHandler<TradeInfoEventArgs> OnNewTrade = null;
        internal override void LoadConfig(NLog.Internal.ConfigurationManager aConfig)
        {
            Enable = bool.Parse(aConfig.AppSettings["enable_copy"]);
            RedisNewCopyOrderKey = aConfig.AppSettings["redis_copy_orders_id_key"];
            RedisCopyActivityTemplate = aConfig.AppSettings["redis_copy_activity_seq_template"];
            RedisCopyOrderFromTemplate = aConfig.AppSettings["redis_copy_orders_from_template"];
            RedisCopyOrderToTemplate = aConfig.AppSettings["redis_copy_orders_to_template"];
            RedisCopyUserTemplate = aConfig.AppSettings["redis_copy_user_template"];
            RedisCopyRateTemplate = aConfig.AppSettings["redis_copy_rate_template"];
            RedisCopyBreakTemplate = aConfig.AppSettings["redis_copy_break_template"];
            RedisCacheUcodeTemplate = aConfig.AppSettings["redis_copy_cache_ucode_template"];
            MT4Addr = aConfig.AppSettings["mt4demo_host"];
            MT4User = int.Parse(aConfig.AppSettings["mt4demo_user"]);
            MT4Pass = aConfig.AppSettings["mt4demo_passwd"];
        }
        private static bool Enable
        {
            get;
            set;
        }

        public static string RedisNewCopyOrderKey
        {
            get;
            set;
        }

        public static string RedisCopyActivityTemplate
        {
            get;
            set;
        }

        public static string RedisCopyOrderFromTemplate
        {
            get;
            private set;
        }

        public static string RedisCopyOrderToTemplate
        {
            get;
            private set;
        }

        public static string RedisCopyUserTemplate
        {
            get;
            private set;
        }

        public static string RedisCopyRateTemplate
        {
            get;
            private set;
        }

        public static string RedisCopyBreakTemplate
        {
            get;
            private set;
        }

        public static string RedisCacheUcodeTemplate
        {
            get;
            private set;
        }

        private static string MT4Addr
        {
            get;
            set;
        }

        private static int MT4User
        {
            get;
            set;
        }

        private static string MT4Pass
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
                _thProc.IsBackground = true;
                _thProc.Start();
            }
            ServerContainer.ForkServer<SaveServer>();
            logger.Info("复制服务已经完成启动");
        }

        public void Stop()
        {
            EnableRunning = false;
            PumpServer.OnNewTrade -= WhenNewTrade;
        }

        public void WhenNewTrade(object sender, TradeInfoEventArgs e)
        {
            _queNewTrades.Enqueue(e);
            _signal.Release();
        }

        private void CopyProc()
        {
            TradeInfoEventArgs item = null;
            while (Utils.SignalWait(ref EnableRunning, _signal))
            {
                _queNewTrades.TryDequeue(out item);
                try
                {
                    var trade_type = item.TradeType;
                    var trade = item.Trade;
                    var connection = Source.RedisCopy;
                    var key = string.Empty;
                    var handler = OnNewTrade;
                    if (!string.IsNullOrWhiteSpace(trade.comment))
                    {
                        try
                        {
                            var order_id = 0L;
                            if (long.TryParse(trade.comment, out order_id))
                            {
                                key = string.Format(RedisCopyOrderToTemplate, order_id);
                                var value = connection.Get(key);
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    var values = value.Split(',');
                                    var ucode_from = values[0];
                                    var ucode_to = values[1];
                                    item.FromUsercode = ucode_from;
                                    item.ToUsercode = ucode_to;
                                    if (handler != null)
                                        handler(this, item);
                                    if (item.TradeType == TRANS_TYPE.TRANS_DELETE)
                                    {
                                        connection.Del(key);
                                        key = string.Format(RedisCopyActivityTemplate, trade.login);
                                        connection.ZAdd(key, TimeServer.Now.ToTime32().ToString(), ucode_from);
                                    }
                                    continue;
                                }
                            }
                        }
                        catch(FormatException eFormat)
                        {
                            Utils.CommonLog.Warn("复制服务收到一条奇怪的数据格式:{0},在{1}",
                                trade.comment, eFormat.StackTrace);
                        }
                        catch(Exception e)
                        {
                            Utils.CommonLog.Error("复制服务启动存单事件出现问题,{0},{1}",
                                e.Message, e.StackTrace);
                        }
                    }
                    try
                    {
                        key = string.Format(RedisCacheUcodeTemplate, trade.login);
                        var cache_code = connection.Get(key);
                        item.Usercode = cache_code;
                    }
                    catch(Exception e)
                    {
                        Utils.CommonLog.Error("在获取订单所有者邀请码时遇到问题，{0},{1}",
                            e.Message, e.StackTrace);
                    }
                    if (handler != null)
                        handler(this, item);
                    if (!Enable)
                        continue;
                    var mt4_from = trade.login;
                    if (trade_type == TRANS_TYPE.TRANS_ADD)
                    {
                        key = string.Format(RedisCopyUserTemplate, mt4_from);
                        var items = connection.SMembers(key);
                        var trade_date = trade.timestamp.FromTime32();
                        Utils.CommonLog.Info("MT4:{0}的订单{1}已收到",
                            mt4_from, trade.order);
                        var copy_spilt = items.Select(i => i.Split(','));
                        var real_copy = copy_spilt.Where(i => i[1] == "real");
                        var demo_copy = copy_spilt.Where(i => i[1] == "demo");
                        //to real account first
                        MT4Wrapper api = null;
                        if(real_copy.Count() > 0)
                            using (api = Poll.New())
                            {
                                CopyNewTrade(trade, connection, mt4_from, real_copy,
                                    trade_date, api);
                            }
                        if(demo_copy.Count() > 0)
                            using (api = new MT4Wrapper(MT4Addr, MT4User, MT4Pass))
                            {
                                CopyNewTrade(trade, connection, mt4_from, demo_copy,
                                    trade_date, api);
                            }
                    }
                    if (trade_type == TRANS_TYPE.TRANS_DELETE && !string.IsNullOrWhiteSpace(trade.symbol))
                    {
                        key = string.Format(RedisCopyOrderFromTemplate, trade.order);
                        var items = connection.SMembers(key);
                        if (items.Count() > 0)
                        {
                            Utils.CommonLog.Info("可复制订单{0}已平仓，copy数量{1}",
                                trade.order, items.Count());
                        }
                        var copy_spilt = items.Select(i => i.Split(','));
                        var real_copy = copy_spilt.Where(i => i[1] == "real");
                        var demo_copy = copy_spilt.Where(i => i[1] == "demo");
                        MT4Wrapper api = null;
                        if(real_copy.Count() > 0)
                            using (api = Poll.New())
                            {
                                CopyBalanceTrade(trade, connection, real_copy, api);
                            }
                        if (demo_copy.Count() > 0)
                            using (api = new MT4Wrapper(MT4Addr, MT4User, MT4Pass))
                            {
                                CopyBalanceTrade(trade, connection, demo_copy, api);
                            }
                    }
                }
                catch(Exception e)
                {
                    Utils.CommonLog.Error(string.Format("复制服务出错,{0},{1}",
                        e.Message, e.StackTrace));
                }
            }
            ServerContainer.FinishStop();
        }

        private static void CopyBalanceTrade(TradeRecordResult trade, 
            RedisClient connection, IEnumerable<string[]> items,
            MT4Wrapper api)
        {
            var key = string.Empty;
            foreach (var i in items)
            {
                try
                {
                    var arr = i;
                    var copy_order = int.Parse(arr[0]);
                    var volume = int.Parse(arr[1]);
                    var order_id = arr[2];
                    var source = arr[3];
                    var to_user_code = arr[4];
                    var from_user_code = arr[5];
                    Utils.CommonLog.Info("自动平掉复制订单{0}的copy订单{1}，成交量{2},位置{3},redis单号{4}",
                        trade.order, copy_order, volume, source, order_id);
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
                        key = string.Format(RedisCopyBreakTemplate, to_user_code, from_user_code);
                        connection.SRem(key, trade.order);
                        key = string.Format(RedisCopyOrderFromTemplate, trade.order);
                        connection.SRem(key, i);
                        Utils.CommonLog.Info("自动平掉复制订单{0}的copy订单{1}成功",
                            trade.order, copy_order);
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
                        i, e.Message, e.StackTrace);
                }
            }
        }

        private static void CopyNewTrade(TradeRecordResult trade, RedisClient connection, 
            int mt4_from, IEnumerable<string[]> items, DateTime trade_date,
            MT4Wrapper api)
        {
            var key = string.Empty;
            foreach (var i in items)
            {
                var order_id = 0L;
                try
                {
                    var values = i;
                    var mt4_to = int.Parse(values[0]);
                    var source = values[1];
                    var from_user_code = values[2];
                    var to_user_code = values[3];
                    Utils.CommonLog.Info("将{0}(MT4:{1})的订单{2}复制给{3}(MT4:{4})",
                        from_user_code, mt4_from, trade.order, to_user_code, mt4_to);
                    key = string.Format(RedisCopyRateTemplate, mt4_to, mt4_from);
                    var rate = double.Parse(connection.Get(key));
                    var now = TimeServer.Now;
                    if (source != "demo" && Math.Abs((now - trade_date).TotalSeconds) > 1)
                    {
                        Utils.CommonLog.Info("将邀请码{0}(MT4:{1})的订单{2}复制给{3}(MT4:{4}),时间过长,单据时间:{5},此时:{6}",
                        from_user_code, mt4_from, trade.order, to_user_code, mt4_to, trade_date, now);
                        continue;
                    }
                    var volnum = (int)(rate * trade.volume);
                    if (volnum <= 0)
                    {
                        Utils.CommonLog.Info("邀请码{0}(MT4:{1})的订单{2}复制给{3}(MT4:{4})，复制单成交量过小，跳过复制",
                            from_user_code, mt4_from, trade.order, to_user_code, mt4_to);
                        continue;
                    }
                    key = RedisNewCopyOrderKey;
                    order_id = connection.Incr(key);
                    key = string.Format(RedisCopyOrderToTemplate, order_id);
                    var value = string.Format("{0},{1}", from_user_code, to_user_code);
                    connection.Set(key, value);
                    var args = new TradeTransInfoArgsResult
                    {
                        type = TradeTransInfoTypes.TT_BR_ORDER_OPEN,
                        cmd = (short)trade.cmd,
                        orderby = mt4_to,
                        price = trade.open_price,
                        symbol = trade.symbol,
                        volume = volnum,
                        comment = order_id.ToString("X"),
                    };
                    var result = api.TradeTransaction(ref args);
                    if (result == RET_CODE.RET_OK)
                    {
                        key = string.Format(RedisCopyOrderFromTemplate, trade.order);
                        value = string.Format("{0},{1},{2},{3},{4},{5},{6}",
                            args.order, args.volume, order_id, source, to_user_code,
                            from_user_code, trade.cmd);
                        connection.SAdd(key, value);
                        key = string.Format(RedisCopyBreakTemplate, to_user_code, from_user_code);
                        value = trade.order.ToString();
                        connection.SAdd(key, value);
                        Utils.CommonLog.Info("邀请码{0}(MT4:{1})的订单{2}复制给{3}(MT4:{4})，复制成功",
                            from_user_code, mt4_from, trade.order, to_user_code, mt4_to);
                    }
                    else
                    {
                        if (order_id != 0)
                        {
                            key = string.Format(RedisCopyOrderToTemplate, order_id);
                            connection.Del(key);
                            order_id = 0;
                        }
                        key = string.Format(RedisCopyBreakTemplate, to_user_code, from_user_code);
                        connection.SRem(key, trade.order);
                        Utils.CommonLog.Warn("邀请码{0}(MT4:{1})的订单{2}复制给{3}(MT4:{4})，复制结果失败，原因:{5}",
                            from_user_code, mt4_from, trade.order, to_user_code, mt4_to, result);
                    }
                }
                catch (Exception e)
                {
                    Utils.CommonLog.Error("子订单{0}复制出现问题{1},{2}",
                        i, e.Message, e.StackTrace);
                    if (order_id != 0)
                    {
                        try
                        {
                            key = string.Format(RedisCopyOrderToTemplate, order_id);
                            connection.Del(key);
                        }
                        catch
                        { }
                    }
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
