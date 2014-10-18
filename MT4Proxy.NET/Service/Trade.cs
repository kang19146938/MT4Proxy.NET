using MT4Proxy.NET.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    [MT4Service(RedisKey = "trade:lists2", RedisOutputList="trade:lists3")]
    class Trade : IService
    {
        public void OnRequest(IServer aServer, string aContent)
        {
            Console.WriteLine(aContent);
            aServer.Output = aContent;
        }
    }
}
