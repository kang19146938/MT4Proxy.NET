using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
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
            while(finish_count < items.Count)
            {
                var fine = _stopSignal.WaitOne(10000);
                if (fine)
                    finish_count++;
                else
                    break;
            }
            MT4API.uninit();
        }

        public static void StopFinish()
        {
            _stopSignal.Release();
        }

        private static System.Threading.Semaphore _stopSignal = 
            new System.Threading.Semaphore(0, 20000);
    }
}
