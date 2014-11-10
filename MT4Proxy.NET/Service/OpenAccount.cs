using Newtonsoft.Json;
using System;
using MT4Proxy.NET.Core;
using System.Dynamic;
using MT4CliWrapper;
using NLog;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class OpenAccount:IService
    {
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            try
            {
                var dict = aJson;
                var api = aServer.MT4;
                var is_real = Convert.ToBoolean(dict.is_real);
                var args = new UserRecordArgs
                {
                    login = Convert.ToInt32(dict.mt4UserID),
                    password = dict.password,
                    name = dict.name,
                    email = dict.email,
                    group = Poll.MT4Group,
                    leverage = Convert.ToInt32(dict.leverage)
                };
                if(!is_real)
                {
                    args.group = Poll.MT4DemoGroup;
                    api = new MT4API(Poll.MT4DemoHost, Poll.MT4DemoAdminID, Poll.MT4DemoPasswd);
                }

                var result = api.OpenAccount(args);
                if(result == RET_CODE.RET_OK)
                {
                    var money_args = new TradeTransInfoArgs
                    {
                        type = TradeTransInfoTypes.TT_BR_BALANCE,
                        cmd = 6,
                        orderby = Convert.ToInt32(dict.mt4UserID),
                        price = 10000.0
                    };
                    result = api.TradeTransaction(money_args);
                    dynamic resp = new ExpandoObject();
                    resp.is_succ = result == RET_CODE.RET_OK;
                    resp.err_msg = Utils.GetErrorMessage(result);
                    aServer.Output = JsonConvert.SerializeObject(resp);
                    if(result != RET_CODE.RET_OK)
                    {
                        var logger = LogManager.GetLogger("common");
                        logger.Error(string.Format("模拟账户充钱失败ID:{0},PASSWD:{1},NAME:{2},EMAIL:{3},GROUP:{4}",
                        args.login, args.password, args.name, args.email, args.group));
                    }
                }
                else
                {
                    dynamic resp = new ExpandoObject();
                    resp.is_succ = result == RET_CODE.RET_OK;
                    resp.err_msg = Utils.GetErrorMessage(result);
                    aServer.Output = JsonConvert.SerializeObject(resp);
                    var logger = LogManager.GetLogger("common");
                    logger.Error(string.Format("开户失败ID:{0},PASSWD:{1},NAME:{2},EMAIL:{3},GROUP:{4}",
                        args.login, args.password, args.name, args.email, args.group));
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message + e.StackTrace);
            }
        }
    }
}
