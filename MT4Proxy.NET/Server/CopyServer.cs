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
            if (!Enable)
                return;
            RedisNewCopyOrderKey = aConfig.AppSettings["redis_copy_orders_id_key"];
            RedisCopyOrderFromTemplate = aConfig.AppSettings["redis_copy_orders_from_template"];
            RedisCopyOrderToTemplate = aConfig.AppSettings["redis_copy_orders_to_template"];
            RedisCopyUserTemplate = aConfig.AppSettings["redis_copy_user_template"];
            RedisCopyTargetTemplate = aConfig.AppSettings["redis_copy_target_template"];
            RedisCopyRateTemplate = aConfig.AppSettings["redis_copy_rate_template"];
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

        public static string RedisCopyTargetTemplate
        {
            get;
            private set;
        }

        public static string RedisCopyRateTemplate
        {
            get;
            private set;
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
                logger.Info("复制服务被禁用");
                return;
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
            logger.Info("复制服务已经启动");
        }

        public void Stop()
        {
            EnableRunning = false;
            PumpServer.OnNewTrade -= WhenNewTrade;
            if (!Enable)
                ServerContainer.FinishStop();
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
                var api = Poll.New();
                var trade_type = item.TradeType;
                var trade = item.Trade;
                var connection = Source.RedisCopy;
                var key = string.Empty;
                var handler = OnNewTrade;
                if(!string.IsNullOrWhiteSpace(trade.comment))
                {
                    var order_id = Convert.ToInt64(trade.comment, 16);
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
                        if(item.TradeType == TRANS_TYPE.TRANS_DELETE)
                            connection.Del(key);
                        continue;
                    }
                }
                if (handler != null)
                    handler(this, item);
                var mt4_from = trade.login;
                
                if(trade_type == TRANS_TYPE.TRANS_ADD)
                {
                    
                    /*
                    var key = string.Format(Poll.RedisCopyOrderKeyTemplate, trade.order);
                    var exists_order = connection.Exists(key);
                    if (exists_order)
                        continue;
                    */
                    key = string.Format(RedisCopyUserTemplate, mt4_from);
                    var items = connection.SMembers(key);
                    var trade_date = trade.timestamp.FromTime32();
                    foreach(var i in items)
                    {
                        var values = i.Split(',');
                        var mt4_to = int.Parse(values[0]);
                        var source = values[1];
                        var to_user_code = values[2];
                        var from_user_code = values[3];
                        key = string.Format(RedisCopyRateTemplate, mt4_to, mt4_from);
                        var rate = int.Parse(connection.Get(key));
                        var now = TimeServer.Now;
                        if (Math.Abs((now - trade_date).TotalSeconds) > 1)
                            break;
                        //开仓here
                        var volnum = (int)(rate * trade.volume * 0.01);
                        if (volnum < 0)
                            continue;
                        key = RedisNewCopyOrderKey;
                        var order_id = connection.Incr(key);
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
                            value = string.Format("{0},{1},{2}", 
                                args.order, args.volume, order_id);
                            connection.SAdd(key, value);
                        }
                    }
                }
                if(trade_type == TRANS_TYPE.TRANS_DELETE && !string.IsNullOrWhiteSpace(trade.symbol))
                {
                    key = string.Format(RedisCopyOrderFromTemplate, trade.order);
                    var items = connection.SMembers(key);
                    foreach(var i in items)
                    {
                        var arr = i.Split(',');
                        var copy_order = int.Parse(arr[0]);
                        var volume = int.Parse(arr[1]);
                        var order_id = arr[2];
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
                            connection.SRem(key, i);
                    }
                }
                Poll.Release(api);
            }
            ServerContainer.FinishStop();
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
