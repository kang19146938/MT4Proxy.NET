using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MT4Proxy.NET.Core;
using System.Dynamic;

namespace MT4Proxy.NET.Service
{
    [MT4Service(RedisKey="users:list")]
    class OpenAccount:IService
    {
        public void OnRequest(IServer aServer, string aJson)
        {
            aServer.Logger.Info(string.Format("OpenAccount,recv request:{0}", aJson));
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(aJson);
            aServer.RedisOutputList = string.Format("users:{0}:{1}:list", dict["mt4UserID"], dict["taskId"]);
            var args = new UserRecordArgs {
                login = int.Parse(dict["mt4UserID"]),
                password = dict["password"],
                name = dict["name"],
                email = dict["email"],
                group = dict["group"],
                leverage = int.Parse(dict["leverage"])
            };
            var result = aServer.MT4.UserRecordNew(args);
            dynamic resp = new ExpandoObject();
            resp.is_succ = result == 0;
            resp.err_msg = MT4CliWrapper.MT4Wrapper.GetErrorMessage(result);
            aServer.Output = JsonConvert.SerializeObject(resp);
            aServer.Logger.Info(string.Format("OpenAccount,response:{0}", aServer.Output));
        }
    }
}
