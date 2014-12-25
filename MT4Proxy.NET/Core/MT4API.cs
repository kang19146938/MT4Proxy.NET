using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MT4CliWrapper;
using NLog;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using MT4Proxy.NET.EventArg;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// C++\CLI MT4类的高层包装，用于适配Poll系统。
    /// </summary>
    internal class MT4API : MT4Wrapper
    {
        public static void init(string aServerAddr, int aUser, string aPasswd)
        {
            MT4Wrapper.init(aServerAddr, aUser, aPasswd, FetchCache, UpdateCache, RemoveCache);
        }

        public MT4API(bool aPump):base(aPump)
        { }

        public MT4API():this(false)
        {

        }

        public MT4API(string aHost, int aLogin, string aPasswd)
            :base(aHost, aLogin, aPasswd)
        { }

        internal static int PumperCount
        {
            get;
            set;
        }

        internal DateTime LastPushTime
        {
            get;
            set;
        }

        public MT4API(int aID)
            : base(false)
        {
            LastPushTime = DateTime.Now;
            ID = aID;
        }

        public int ID
        {
            get;
            private set;
        }

        public bool IsOutOfDate
        {
            get
            {
                return (DateTime.Now - LastPushTime) > new TimeSpan(0, 0, 10);
            }
        }

        protected override void OnPumpTrade(TRANS_TYPE aType, TradeRecordResult aRecord)
        {
            var handler = OnNewTrade;
            if(handler != null)
                handler(this, new TradeInfoEventArgs(aType, aRecord));
        }

        public event EventHandler<TradeInfoEventArgs> OnNewTrade = null;
        public event EventHandler<QuoteInfoEventArgs> OnNewQuote = null;

        protected override void OnPumpAskBid(SymbolInfoResult[] aSymbols)
        {
            string symbolPattern = @"^(?<symbol>[A-Za-z]+)(?<leverage>\d*)$";
            string symbolPattern_CFD = @"^(?<symbol>[A-Za-z]+)_(?<number>\d*)$";
            foreach (var symbol in aSymbols)
            {
                foreach (Match j in Regex.Matches(symbol.symbol, symbolPattern))
                    InvokeQuote(symbol, j);
                foreach (Match j in Regex.Matches(symbol.symbol, symbolPattern_CFD))
                    InvokeQuote(symbol, j);
            }
        }

        private void InvokeQuote(SymbolInfoResult symbol, Match j)
        {
            var match = j.Groups;
            var symbol_origin = match["symbol"].ToString().ToUpper();
            var handler = OnNewQuote;
            if (handler != null)
                handler(this, new QuoteInfoEventArgs(symbol_origin,
                    symbol.ask, symbol.bid, symbol.lasttime));
        }

        private static object FetchCache(IntPtr aKey)
        {
            object result = null;
            _dictCache.TryGetValue(aKey, out result);
            return result;
        }

        private static void UpdateCache(IntPtr aKey, object aValue)
        {
            _dictCache.TryAdd(aKey, aValue);
        }

        private static object RemoveCache(IntPtr aKey)
        {
            object nouse = null;
            _dictCache.TryRemove(aKey, out nouse);
            return nouse;
        }

        private static ConcurrentDictionary<IntPtr, object> _dictCache = 
            new ConcurrentDictionary<IntPtr, object>();
    }
}
