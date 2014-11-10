using MT4CliWrapper;
using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using NLog;
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
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            try
            {
                var dict = aJson;
                var result = aServer.MT4.ChangePassword(Convert.ToInt32(dict.mt4UserID), dict.password.ToString());
                dynamic resp = new ExpandoObject();
                resp.is_succ = result == RET_CODE.RET_OK;
                resp.err_msg = Utils.GetErrorMessage(result);
                aServer.Output = JsonConvert.SerializeObject(resp);
            }
            catch (Exception e)
            {
                var logger = LogManager.GetLogger("common");
                logger.Error(e.Message + e.StackTrace);
            }
        }
    }
}
