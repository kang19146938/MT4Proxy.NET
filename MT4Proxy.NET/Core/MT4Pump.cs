using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using System.Collections.Concurrent;
using MT4CliWrapper;

namespace MT4Proxy.NET.Core
{
    class MT4Pump
    {
        public MT4Pump()
        {
            if(Syncer == null)
                Syncer = new MysqlSyncer();
        }

        public static MysqlSyncer Syncer
        {
            get;
            set;
        }

        private MT4API MT4
        {
            get;
            set;
        }

        private Timer Timer
        {
            get;
            set;
        }

        public static volatile bool EnableRestart;
        private System.Threading.Thread _storageThread = null;
        private static System.Threading.Thread _tradeThread = null;
        private static ConcurrentQueue<Tuple<TRANS_TYPE, TradeRecordResult>>
            _queTrades = new ConcurrentQueue<Tuple<TRANS_TYPE, TradeRecordResult>>();
        private static System.Threading.Semaphore _tradeSignal = new System.Threading.Semaphore(0, 20000);
        private static volatile int _lastTradeTime = 0;
        private static ConcurrentBag<int> _tradeOrders = new ConcurrentBag<int>();

        public static void PushTrade(TRANS_TYPE aType, TradeRecordResult aTrade)
        {
            /*
             * MT4日期是1970年起按秒计到32位有符号数里，2039年就跪了，不过2039年安卓控制服务器了吧，这玩意应该淘汰了
             */
            if (aTrade.timestamp < _lastTradeTime)
                return;
            _queTrades.Enqueue(new Tuple<TRANS_TYPE, TradeRecordResult>(aType, aTrade));
            _tradeSignal.Release();
        }

        private static void SaveTradeProc(object aArg)
        {
            Tuple<TRANS_TYPE, TradeRecordResult> item = null;
            while (EnableRestart)
            {
                _tradeSignal.WaitOne();
                _queTrades.TryDequeue(out item);
                if (item == null)
                    continue;
                var trade = item.Item2;
                var trade_type = item.Item1;
                if (trade.timestamp != _lastTradeTime)
                {
                    _tradeOrders = new ConcurrentBag<int>();
                    _lastTradeTime = trade.timestamp;
                }
                if (_tradeOrders.Contains(trade.order))
                    continue;
                _tradeOrders.Add(trade.order);
                Syncer.PushTrade(trade_type, trade);
            }
        }

        public static void StartPump()
        {
            EnableRestart = true;
            if (_tradeThread == null)
            {
                _tradeThread = new System.Threading.Thread(SaveTradeProc);
                _tradeThread.IsBackground = false;
                _tradeThread.Start();
            }
            for (int i = 0; i < MT4API.PumperCount; i++)
            {
                var timer = new Timer(10000);
                timer.Interval = 10000;
                var pump = new MT4Pump() { Timer = timer };
                timer.Elapsed += (sender, e) =>
                {
                    pump.RestartPump(pump);
                };
                timer.Start();
                pump.RestartPump(pump);
            }
        }

        private void RestartPump(MT4Pump aPump)
        {
            aPump.Timer.Stop();
            if (!EnableRestart)
                return;
            int retryTimes = 3;
            var mt4 = new MT4API(true);
            while(retryTimes-- > 0)
            {
                if (!mt4.IsPumpAlive())
                {
                    Logger logger = LogManager.GetLogger("common");
                    logger.Warn(
                        string.Format("MT4推送接收连接建立失败，一秒之后重试，剩余机会{0}", 
                        retryTimes + 1));
                    System.Threading.Thread.Sleep(1000);
                    mt4.ConnectPump();
                    continue;
                }
                break;
            }
            if(retryTimes == -1)
            {
                Logger logger = LogManager.GetLogger("common");
                logger.Error("MT4推送接收连接建立失败，请立即采取措施保障丢失的数据！");
            }
            else
            {
                if (aPump.MT4 != null)
                    aPump.MT4.Dispose();
                aPump.MT4 = mt4;
            }
            aPump.Timer.Start();
        }
    }
}