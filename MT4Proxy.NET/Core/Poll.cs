using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using MT4CliWarpper;
using System.Threading;

namespace MT4Proxy.NET.Core
{
    internal class Poll
    {
        private static ConcurrentDictionary<int, MT4API> _poll = new ConcurrentDictionary<int, MT4API>();
        static int _taskid = 0;

        public static MT4Wrapper Fetch(int id)
        {
            MT4API fetch = null;
            _poll.TryRemove(id, out fetch);
            if (fetch == null || (DateTime.Now - fetch.LastPushTime) > new TimeSpan(0,0,10))
            {
                fetch = new MT4API(id);
            }
            else
            {
                fetch.LastPushTime = DateTime.Now;
            }
            return fetch;
        }

        public static MT4Wrapper New()
        {
            while(true)
            {
                var i = Interlocked.Increment(ref _taskid);
                return new MT4API(i);
            }
        }

        public static void Bringback(MT4Wrapper aWrapper)
        {
            var dtwrapper = (MT4API)aWrapper;
            _poll.TryAdd(dtwrapper.ID, dtwrapper);
        }

        public static IEnumerable<int> Keys
        {
            get
            {
                return _poll.Keys.ToList();
            }
        }

        private static void DogProc()
        {
            try
            {
                while (true)
                {
                    foreach (var i in Poll.Keys)
                    {
                        MT4API fetch = null;
                        Poll._poll.TryRemove(i, out fetch);
                        if (fetch != null)
                            if (fetch == null || (DateTime.Now - fetch.LastPushTime) > new TimeSpan(0, 0, 10))
                                fetch.Dispose();
                            else
                                Poll._poll.TryAdd(i, fetch);
                    }
                    Thread.Sleep(1000);
                }
            }
            catch(Exception e)
            {

            }
        }

        public static void init()
        {
            MT4Wrapper.init();
            var thDog = new Thread(DogProc);
            thDog.IsBackground = true;
            thDog.Start();
        }

        public static void uninit()
        {
            MT4Wrapper.uninit();
        }
    }
}
