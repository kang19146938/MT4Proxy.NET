using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class ServerTime : IService
    {
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            var time = aServer.MT4.ServerTime();
            dynamic resp = new ExpandoObject();
            resp.is_succ = true;
            resp.now = time;
            resp.err_msg = "";
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
