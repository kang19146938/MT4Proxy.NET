using MT4CliWrapper;
using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using System;
using System.Dynamic;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class Equity:IService
    {
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            var login = Convert.ToInt32(aJson["mt4UserID"]);
            double equity = 0;
            var result = aServer.MT4.GetEquity(login, ref equity);
            dynamic resp = new ExpandoObject();
            resp.is_succ = result == RET_CODE.RET_OK;
            resp.err_msg = Utils.GetErrorMessage(result);
            resp.equity = equity;
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
