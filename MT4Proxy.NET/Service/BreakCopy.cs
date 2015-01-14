using MT4CliWrapper;
using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using NLog.Internal;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    /// <summary>
    /// 中断用户间现有持仓copy关系
    /// </summary>
    [MT4Service(EnableZMQ=true)]
    class BreakCopy : ConfigBase, IService
    {
        private static string RedisCopyBreakTemplate
        {
            get;
            set;
        }

        private static string RedisCopyFromTemplate
        {
            get;
            set;
        }

        private static string RedisCopyToTemplate
        {
            get;
            set;
        }

        internal override void LoadConfig(ConfigurationManager aConfig)
        {
            RedisCopyFromTemplate = aConfig.AppSettings["redis_copy_orders_from_template"];
            RedisCopyToTemplate = aConfig.AppSettings["redis_copy_orders_to_template"];
            RedisCopyBreakTemplate = aConfig.AppSettings["redis_copy_break_template"];
        }
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            var from_user_code = Convert.ToInt32(aJson["from_code"]);
            var to_user_code = Convert.ToInt32(aJson["to_code"]);
            var to_source = Convert.ToString(aJson["source"]);
            var auto_delete = Convert.ToBoolean(aJson["auto_delete"]);
            var db = new DockServer();
            var key = string.Empty;
            MT4CliWrapper.MT4Wrapper api = null;
            if (auto_delete)
                api = Poll.New();
            using (var redis = db.RedisCopy)
            {
                key = string.Format(RedisCopyBreakTemplate, to_user_code, from_user_code);
                var orders = redis.SMembers(key);
                foreach (var order in orders)
                {
                    key = string.Format(RedisCopyFromTemplate, order);
                    var copys = redis.SMembers(key);
                    foreach (var copy_link in copys)
                    {
                        var arr = copy_link.Split(',');
                        var copy_order = int.Parse(arr[0]);
                        var volume = int.Parse(arr[1]);
                        var order_id = arr[2];
                        var source = arr[3];
                        var to_user = Convert.ToInt32(arr[4]);
                        var direction = Convert.ToInt16(arr[6]);
                        if (to_user != to_user_code || to_source != source)
                            continue;
                        var order_id_key = string.Format(RedisCopyToTemplate, order_id);
                        redis.Del(order_id_key);
                        redis.SRem(key, copy_link);
                        if (auto_delete)
                        {
                            try
                            {
                                var args2 = new TradeTransInfoArgsResult
                                {
                                    type = TradeTransInfoTypes.TT_BR_ORDER_DELETE,
                                    cmd = direction,
                                    order = copy_order,
                                    volume = volume,
                                };
                                var result = api.TradeTransaction(ref args2);
                                if (result == MT4CliWrapper.RET_CODE.RET_OK)
                                    Utils.CommonLog.Info("BreakCopy强制平仓复制订单{0}成功",
                                        copy_order);
                                else
                                    Utils.CommonLog.Info("BreakCopy强制平仓复制订单{0}成功，原因{1}",
                                        copy_order, result);
                            }
                            catch (Exception e)
                            {
                                Utils.CommonLog.Error("BreakCopy强制平仓出现问题，在订单{0},{1},{2}",
                                    copy_order, e.Message, e.StackTrace);
                            }
                        }
                    }
                }
                if (auto_delete)
                    api.Dispose();
            }
            dynamic resp = new ExpandoObject();
            resp.is_succ = true;
            resp.errMsg = string.Empty;
            resp.errCode = 0;
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
