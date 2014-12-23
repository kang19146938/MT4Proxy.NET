using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using MT4CliWrapper;


namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 单线程的复制系统
    /// </summary>
    class Copy
    {
        public Copy()
        {

        }

        public void Initialize()
        {
            var items = _db.PullCopyData();
            foreach (var i in items)
                _collection.Add(i.Key, i.Value);
        }

        public void PushTrade(TRANS_TYPE aType, TradeRecordResult aTrade)
        {
            _queNewTrades.Enqueue(new Tuple<TRANS_TYPE, TradeRecordResult>(aType, aTrade));
            _signal.Release();
        }


        private ConcurrentQueue<Tuple<TRANS_TYPE, TradeRecordResult>> _queNewTrades = 
            new ConcurrentQueue<Tuple<TRANS_TYPE, TradeRecordResult>>();
        private MysqlServer _db = new MysqlServer();
        private NameValueCollection _collection = new NameValueCollection();
        private System.Threading.Semaphore _signal = new System.Threading.Semaphore(0, 20000);
    }
}
