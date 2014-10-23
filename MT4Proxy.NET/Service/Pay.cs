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
    class Pay : IService
    {
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            var dict = aJson;
            var args = new TradeTransInfoArgs
            {
                type = TradeTransInfoTypes.TT_BR_BALANCE,
                cmd = Convert.ToInt16(dict.cmd),
                orderby = Convert.ToInt32(dict.mt4UserID),
                price = Convert.ToDouble(dict.price)
            };
            var result = aServer.MT4.TradeTransaction(args);
            dynamic resp = new ExpandoObject();
            resp.is_succ = result == 0;
            resp.errMsg = Utils.GetErrorMessage(result);
            resp.errCode = (int)result;
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
