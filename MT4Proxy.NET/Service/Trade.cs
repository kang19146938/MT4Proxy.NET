using MT4Proxy.NET.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ=true)]
    class Trade : IService
    {
        public void OnRequest(IServer aServer, Dictionary<string, string> aContent)
        {
            Console.WriteLine(aContent);
            aServer.Output = aContent.ToString();
        }
    }
}
