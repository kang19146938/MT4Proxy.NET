using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    internal static class ServerContainer
    {
        private static List<IServer> _collection = new List<IServer>();
        public static void ForkServer(Type aServer)
        {
            var flag = aServer.GetInterface(typeof(IServer).Name);
            if(flag != null)
            {
                var item = Activator.CreateInstance(aServer) as IServer;
                _collection.Add(item);
                item.Initialize();
            }
        }

        public static void StopAll()
        {
            var items = _collection.ToList();
            _collection.Clear();
            items.Reverse();
            foreach (var i in items)
                i.Stop();
        }
    }
}
