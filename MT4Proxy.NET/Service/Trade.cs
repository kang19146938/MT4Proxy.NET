using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using System;
using System.Dynamic;
using System.Collections.Generic;
using System.Linq;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class Trade : IService
    {
        private static SortedSet<int> _leverageSet = new SortedSet<int>(new int[] { 25, 50, 100, 200 });
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            dynamic resp = new ExpandoObject();
            var dict = aJson;
            if(!_leverageSet.Contains(dict["leverage"]))
            {
                resp = Utils.MakeResponseObject(false, -1, "杠杆不正确");
                aServer.Output = JsonConvert.SerializeObject(resp);
                return;
            }
            var args = new TradeTransInfoArgs
            {
                type = Convert.ToInt32(dict["type"]),
                cmd = Convert.ToInt16(dict["cmd"]),
                orderby = Convert.ToInt32(dict["mt4UserID"]),
                price = Convert.ToDouble(dict["price"]),
                order = Convert.ToInt32(dict["mt4OrderID"]),
                symbol = string.Format("{0}{1}", dict["symbol"], dict["leverage"]),
                volume = Convert.ToInt32(dict["volume"]),
                sl = Convert.ToDouble(dict["sl"]),
                tp = Convert.ToDouble(dict["tp"]),
            };
            //if(args.type == TradeTransInfoTypes.TT_BR_BALANCE)
            var result = aServer.MT4.TradeTransaction(args);
            resp.is_succ = result == 0;
            resp.errMsg = Utils.GetErrorMessage(result);
            resp.errCode = (int)result;
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
