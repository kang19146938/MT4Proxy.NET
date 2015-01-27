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
    [MT4Service(EnableZMQ = true, DisableMT4 = true, ShowRequest=false, ShowResponse = true)]
    class ServerTime : IService
    {
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            dynamic resp = new ExpandoObject();
            resp.is_succ = true;
            resp.now = TimeServer.Now.ToTime32();
            resp.err_msg = "";
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
