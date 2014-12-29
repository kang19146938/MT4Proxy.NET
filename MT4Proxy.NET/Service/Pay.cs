using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using System;
using System.Dynamic;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class Pay : IService
    {
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            var dict = aJson;
            var args = new TradeTransInfoArgsResult
            {
                type = TradeTransInfoTypes.TT_BR_BALANCE,
                cmd = 6,
                orderby = Convert.ToInt32(dict["mt4UserID"]),
                price = Convert.ToDouble(dict["price"])
            };
            var result = aServer.MT4.TradeTransaction(ref args);
            dynamic resp = new ExpandoObject();
            resp.is_succ = result == 0;
            resp.errMsg = Utils.GetErrorMessage(result);
            resp.errCode = (int)result;
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
