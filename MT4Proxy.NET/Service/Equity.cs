using MT4CliWrapper;
using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using System;
using System.Dynamic;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class Equity : IService
    {
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            var login = Convert.ToInt32(aJson["mt4UserID"]);
            double equity = 0;
            double free = 0 ;
            var result = aServer.MT4.GetEquity(login, ref equity);
            result = aServer.MT4.GetMarginFree(login, ref free);
            equity = Math.Round(equity, 2);
            free = Math.Round(free, 2);
            dynamic resp = new ExpandoObject();
            resp.is_succ = result == RET_CODE.RET_OK;
            resp.err_msg = Utils.GetErrorMessage(result);
            resp.equity = equity.ToString("0.00");
            resp.margin_free = free.ToString("0.00");
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
