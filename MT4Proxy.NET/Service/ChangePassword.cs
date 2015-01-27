using MT4CliWrapper;
using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using NLog;
using NLog.Internal;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class ChangePassword : IService
    {
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            try
            {
                var dict = aJson;
                var is_real = (bool)dict["is_real"];
                var api = aServer.MT4;
                if (!is_real)
                    api = Poll.DemoAPI();
                var result = api.ChangePassword(Convert.ToInt32(dict["mt4UserID"]), dict["password"].ToString());
                dynamic resp = new ExpandoObject();
                resp.is_succ = result == RET_CODE.RET_OK;
                resp.err_msg = Utils.GetErrorMessage(result);
                aServer.Output = JsonConvert.SerializeObject(resp);
            }
            catch (Exception e)
            {
                var logger = Utils.CommonLog;
                logger.Error(e.Message + e.StackTrace);
            }
        }
    }
}
