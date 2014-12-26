using MT4CliWrapper;
using MT4Proxy.NET.Core;
using MT4Proxy.NET.EventArg;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MT4Proxy.NET
{
    internal class SaveServer: IServer
    {
        public SaveServer()
        {
            TradesSyncer = new MysqlServer();
            QuoteSyncer = new MysqlServer();
        }

        private MysqlServer TradesSyncer
        {
            get;
            set;
        }

        private MysqlServer QuoteSyncer
        {
            get;
            set;
        }

        private bool EnableRunning = false;

        public void Initialize()
        {
            EnableRunning = true;
            PumpServer.OnNewQuote += WhenNewQuote;
            PumpServer.OnNewTrade += WhenNewTrade;
            if (_quoteTimer == null)
            {
                _quoteTimer = new Timer(10000);
                _quoteTimer.Elapsed += SaveQuoteProc;
                SaveQuoteProc(_quoteTimer, null);
            }
            if(_tradeThread == null)
            {
                _tradeThread = new System.Threading.Thread(
                    () => 
                    {
                        while(Utils.SignalWait(ref EnableRunning, _tradeSignal))
                        {
                            TradeInfoEventArgs item = null;
                            _queTrades.TryDequeue(out item);
                            TradesSyncer.PushTrade(item.TradeType, item.Trade);
                        }
                        foreach (var item in _queTrades.ToArray())
                            TradesSyncer.PushTrade(item.TradeType, item.Trade);
                        ServerContainer.FinishStop();
                    });
                _tradeThread.IsBackground = true;
                _tradeThread.Start();
            }
            var logger = LogManager.GetLogger("common");
            logger.Info("订单与报价同步服务已经启动");
        }

        public void Stop()
        {
            EnableRunning = false;
            PumpServer.OnNewQuote -= WhenNewQuote;
            PumpServer.OnNewTrade -= WhenNewTrade;
        }

        private void SaveQuoteProc(object sender, ElapsedEventArgs e)
        {
            var timer = sender as Timer;
            timer.Stop();
            var dictBuffer = new Dictionary<Tuple<string, DateTime>, Tuple<double, double>>();
            while (!_queQuote.IsEmpty)
            {
                QuoteInfoEventArgs item = null;
                _queQuote.TryDequeue(out item);
                var dayFormat = item.Timestamp;
                var key = new Tuple<string, DateTime>(item.Symbol, dayFormat);
                if (!dictBuffer.ContainsKey(key))
                    dictBuffer[key] = new Tuple<double, double>(item.Ask, item.Bid);
            }
            var items = dictBuffer.Select(i => new Tuple<string, double, double, DateTime>
                (i.Key.Item1, i.Value.Item1, i.Value.Item2, i.Key.Item2));
            QuoteSyncer.UpdateQuote(items);
            if(EnableRunning)
                timer.Start();
        }

        void WhenNewTrade(object sender, TradeInfoEventArgs e)
        {
            if (EnableRunning)
            {
                _queTrades.Enqueue(e);
                _tradeSignal.Release();
            }
        }

        void WhenNewQuote(object sender, QuoteInfoEventArgs e)
        {
            _queQuote.Enqueue(e);
        }

        private ConcurrentQueue<QuoteInfoEventArgs>
            _queQuote = new ConcurrentQueue<QuoteInfoEventArgs>();
        private static ConcurrentQueue<TradeInfoEventArgs>
            _queTrades = new ConcurrentQueue<TradeInfoEventArgs>();
        private Timer _quoteTimer = null;
        private System.Threading.Thread _tradeThread = null;
        private static System.Threading.Semaphore _tradeSignal = 
            new System.Threading.Semaphore(0, 20000);
        
    }
}
