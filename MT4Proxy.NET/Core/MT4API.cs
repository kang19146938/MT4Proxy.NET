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

namespace MT4Proxy.NET.Core
{
    internal class MT4API : MT4Wrapper
    {
        public static void init(string aServerAddr, int aUser, string aPasswd, int aPumperCount)
        {
            if (aPumperCount < 1)
            {
                string errInfo = "接收MT4推送的线程数量不正确";
                Logger logger = LogManager.GetLogger("common");
                logger.Error(errInfo);
                throw new Exception(errInfo);
            }
            else
            {
                PumperCount = aPumperCount;
                MT4Wrapper.init(aServerAddr, aUser, aPasswd, FetchCache, UpdateCache, RemoveCache);
            }
        }

        public MT4API(bool aPump):base(aPump)
        { }

        public MT4API():base(false)
        {

        }

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
            //Console.WriteLine("order:{0}", JsonConvert.SerializeObject(aRecord));
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
            Console.WriteLine("pong {0}", nouse);
            return nouse;
        }

        private static ConcurrentDictionary<IntPtr, object> _dictCache = 
            new ConcurrentDictionary<IntPtr, object>();
    }
}
