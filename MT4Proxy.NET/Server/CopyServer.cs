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


namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 单线程的复制系统,后面还是可以横扩的
    /// </summary>
    class CopyServer : ConfigBase, IServer
    {
        internal override void LoadConfig(NLog.Internal.ConfigurationManager aConfig)
        {
            RedisHost = aConfig.AppSettings["redis_host"];
            RedisPort = int.Parse(aConfig.AppSettings["redis_port"]);
            RedisPasswd = aConfig.AppSettings["redis_password"];
            RedisCopyKey = aConfig.AppSettings["redis_copy_order_id_key"];
            RedisCopyOrderTemplate = aConfig.AppSettings["redis_copy_orders_key"];
            RedisCopyUserTemplate = aConfig.AppSettings["redis_copy_user_key"];
            RedisCopyTargetTemplate = aConfig.AppSettings["redis_copy_target_key"];
            RedisCopyRateTemplate = aConfig.AppSettings["redis_copy_rate_key"];
        }
        private static string RedisHost
        { 
            get; 
            set; 
        }

        private static int RedisPort
        {
            get;
            set;
        }

        private static string RedisPasswd
        {
            get;
            set;
        }

        public static string RedisCopyOrderTemplate
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

        public static string RedisCopyKey
        {
            get;
            private set;
        }

        public CopyServer()
        {
            CreateTime = DateTime.MinValue;
        }

        private bool EnableRunning;

        public void Initialize()
        {
            EnableRunning = true;
            if (_thProc == null)
            {
                _thProc = new Thread(CopyProc);
                _thProc.IsBackground = true;
                _thProc.Start();
            }
        }
        private RedisClient Connection
        {
            get
            {
                RetryTimes = 3;
                while (RetryTimes-- > 0)
                {
                    try
                    {
                        if (_connection == null || ((DateTime.Now - CreateTime).TotalSeconds > 30))
                        {
                            if (_connection != null)
                            {
                                try
                                {
                                    _connection.Dispose();
                                    _connection = null;
                                }
                                catch { }
                            }
                            _connection = new RedisClient(RedisHost, RedisPort);
                            _connection.Auth(RedisPasswd);
                            CreateTime = DateTime.Now;
                        }
                        break;
                    }
                    catch (Exception e)
                    {
                        Logger logger = LogManager.GetLogger("common");
                        logger.Warn(
                            string.Format("MySQL连接建立失败，一秒之后重试，剩余机会{0}",
                            RetryTimes + 1), e);
                        Thread.Sleep(1000);
                        continue;
                    }
                }
                if (RetryTimes == -1)
                {
                    Logger logger = LogManager.GetLogger("common");
                    logger.Error("MySQL连接建立失败，请立即采取措施保障丢失的数据！");
                    return null;
                }
                else
                {
                    return _connection;
                }
            }
            set
            {
                _connection = value;
            }
        }

        private int RetryTimes
        {
            get;
            set;
        }

        private RedisClient _connection = null;

        private DateTime CreateTime
        {
            get;
            set;
        }

        public void Stop()
        {
            EnableRunning = false;
        }

        public void PushTrade(TRANS_TYPE aType, TradeRecordResult aTrade)
        {
            _queNewTrades.Enqueue(new Tuple<TRANS_TYPE, TradeRecordResult>(aType, aTrade));
            _signal.Release();
        }

        private void CopyProc()
        {
            Tuple<TRANS_TYPE, TradeRecordResult> item = null;
            while (Utils.SignalWait(ref EnableRunning, _signal))
            {
                _queNewTrades.TryDequeue(out item);
                var trade_type = item.Item1;
                var trade = item.Item2;
                var connection = Connection;
                if (IsCopyTrade(trade.order))
                    continue;
                var key = string.Empty;
                var mt4_from = trade.login;
                var api = Poll.New();
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
                        var user_code = values[2];
                        key = string.Format(RedisCopyRateTemplate, mt4_to, mt4_from);
                        var rate = int.Parse(connection.Get(key));
                        var now = DateTime.Now.AddHours(3);
                        //if (Math.Abs((now - trade_date).TotalSeconds) > 1)
                        //    break;
                        var order_no = (int)connection.Incr(RedisCopyKey);
                        //开仓here
                        var volnum = (int)(rate * trade.volume * 0.01);
                        if (volnum < 0)
                            continue;
                        var args = new TradeTransInfoArgs
                        {
                            type = TradeTransInfoTypes.TT_BR_ORDER_OPEN,
                            cmd = (short)trade.cmd,
                            orderby = mt4_to,
                            price = trade.open_price,
                            order = order_no,
                            symbol = trade.symbol,
                            volume = volnum,
                        };
                        var result = api.TradeTransaction(args);
                        if (result == RET_CODE.RET_OK)
                        {
                            key = string.Format(RedisCopyOrderTemplate, trade.order);
                            connection.SAdd(key, order_no);
                        }
                    }
                }
                if(trade_type == TRANS_TYPE.TRANS_DELETE && !string.IsNullOrWhiteSpace(trade.symbol))
                {
                    key = string.Format(RedisCopyOrderTemplate, trade.order);
                    var items = connection.SMembers(key);
                    foreach(var i in items)
                    {
                        var mt4_to = int.Parse(i);
                        //平仓here
                        var args = new TradeTransInfoArgs
                        {
                            type = TradeTransInfoTypes.TT_BR_ORDER_CLOSE,
                            cmd = (short)trade.cmd,
                            orderby = mt4_to,
                            price = trade.open_price,
                        };
                        var result = api.TradeTransaction(args);
                        if (result == RET_CODE.RET_OK)
                        {
                            connection.SRem(key, i);
                        }
                    }
                }
                Poll.Release(api);
            }
            ServerContainer.StopFinish();
        }

        private bool IsCopyTrade(int aOrder)
        {
            if (aOrder < 1073741824)
                return false;
            return true;
        }


        private Thread _thProc = null;
        private ConcurrentQueue<Tuple<TRANS_TYPE, TradeRecordResult>> _queNewTrades = 
            new ConcurrentQueue<Tuple<TRANS_TYPE, TradeRecordResult>>();
        private MysqlServer _db = new MysqlServer();
        private NameValueCollection _collection = new NameValueCollection();
        private System.Threading.Semaphore _signal = new System.Threading.Semaphore(0, 20000);
    }
}
