using Newtonsoft.Json;
using System;
using MT4Proxy.NET.Core;
using System.Dynamic;
using MT4CliWrapper;

namespace MT4Proxy.NET.Service
{
    [MT4Service(RedisKey = "users:list", EnableRedis = false, EnableZMQ = true)]
    class OpenAccount:IService
    {
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            try
            {
                var dict = aJson;
                aServer.RedisOutputList = string.Format("users:{0}:{1}:list", aJson.mt4UserID, aJson.taskId);
                var args = new UserRecordArgs
                {
                    login = Convert.ToInt32(dict.mt4UserID),
                    password = dict.password,
                    name = dict.name,
                    email = dict.email,
                    group = dict.group,
                    leverage = Convert.ToInt32(dict.leverage)
                };
                var result = aServer.MT4.UserRecordNew(args);
                dynamic resp = new ExpandoObject();
                resp.is_succ = result == RET_CODE.RET_OK;
                resp.err_msg = Utils.GetErrorMessage(result);
                aServer.Output = JsonConvert.SerializeObject(resp);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message + e.StackTrace);
            }
        }
    }
}
