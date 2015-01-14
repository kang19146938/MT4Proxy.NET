using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 系统服务管理方法群
    /// </summary>
    internal static class ServerContainer
    {
        private static List<IServer> _collection = new List<IServer>();

        public static void ForkServer<T>() where T : IServer, new()
        {
            var item = new T() as IServer;
            _collection.Add(item);
            item.Initialize();
        }

        public static void StopAll()
        {
            var items = _collection.ToList();
            _collection.Clear();
            items.Reverse();
            foreach (var i in items)
                i.Stop();
            var finish_count = 0;
            var watch = new Stopwatch();
            watch.Start();
            while(finish_count < items.Count)
            {
                var timeout = 10000 - watch.ElapsedMilliseconds;
                if (timeout < 0)
                    timeout = 0;
                var fine = _stopSignal.WaitOne(TimeSpan.FromMilliseconds(timeout));
                if (fine)
                    finish_count++;
                else
                    break;
            }
        }

        public static void FinishStop()
        {
            _stopSignal.Release();
        }

        private static System.Threading.Semaphore _stopSignal = 
            new System.Threading.Semaphore(0, 20000);
    }
}
