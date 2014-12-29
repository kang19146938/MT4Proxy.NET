using Newtonsoft.Json;
using System;
using MT4Proxy.NET.Core;
using System.Dynamic;
using MT4CliWrapper;
using NLog;
using NLog.Internal;

namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class OpenAccount : ConfigBase, IService
    {
        private static string MT4Addr
        {
            get;
            set;
        }

        private static int MT4User
        {
            get;
            set;
        }

        private static string MT4Pass
        {
            get;
            set;
        }

        private static string MT4Group
        { get; set; }

        public static double InitEqutiy
        {
            get;
            private set;
        }

        internal override void LoadConfig(ConfigurationManager aConfig)
        {
            MT4Addr = aConfig.AppSettings["mt4demo_host"];
            MT4User = int.Parse(aConfig.AppSettings["mt4demo_user"]);
            MT4Pass = aConfig.AppSettings["mt4demo_passwd"];
            MT4Group = aConfig.AppSettings["mt4demo_group"];
            InitEqutiy = double.Parse(aConfig.AppSettings["init_equity"]);
        }
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            try
            {
                var dict = aJson;
                var api = aServer.MT4;
                var is_real = Convert.ToBoolean(dict["is_real"]);
                var args = new UserRecordArgs
                {
                    login = Convert.ToInt32(dict["mt4UserID"]),
                    password = dict["password"],
                    name = dict["name"],
                    email = dict["email"],
                    group = Poll.MT4Group,
                    leverage = Convert.ToInt32(dict["leverage"])
                };
                if(!is_real)
                {
                    args.group = MT4Group;
                    api = new MT4API(MT4Addr, MT4User, MT4Pass);
                }

                var result = api.OpenAccount(args);
                if(result == RET_CODE.RET_OK)
                {
                    if (!is_real && InitEqutiy > 0)
                    {
                        var money_args = new TradeTransInfoArgsResult
                        {
                            type = TradeTransInfoTypes.TT_BR_BALANCE,
                            cmd = 6,
                            orderby = Convert.ToInt32(dict["mt4UserID"]),
                            price = 10000.0
                        };
                        result = api.TradeTransaction(ref money_args);
                        dynamic resp = new ExpandoObject();
                        resp.is_succ = result == RET_CODE.RET_OK;
                        resp.err_msg = Utils.GetErrorMessage(result);
                        aServer.Output = JsonConvert.SerializeObject(resp);
                        if (result != RET_CODE.RET_OK)
                        {
                            var logger = Utils.CommonLog;
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
                    }
                }
                else
                {
                    dynamic resp = new ExpandoObject();
                    resp.is_succ = result == RET_CODE.RET_OK;
                    resp.err_msg = Utils.GetErrorMessage(result);
                    aServer.Output = JsonConvert.SerializeObject(resp);
                    var logger = Utils.CommonLog;
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
