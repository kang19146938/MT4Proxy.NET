using MT4CliWrapper;
using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    [MT4Service(DisableMT4 = true)]
    class Uncopy : IService
    {
        public void OnRequest(IInputOutput aIO, dynamic aJson)
        {
            var from_mt4 = Convert.ToInt32(aJson["from_mt4"]);
            var to_mt4 = Convert.ToInt32(aJson["to_mt4"]);
            var auto_delete = Convert.ToBoolean(aJson["auto_delete"]);
            var sql = string.Empty;
            var dock = new DockServer();
            var db = dock.CopySource;
            if (auto_delete)
            {
                sql = "SELECT * FROM copy_order WHERE mt4_to = @mt4_to";
                using (var cmd = new SQLiteCommand(sql, db))
                {
                    cmd.Parameters.AddWithValue("@mt4_to", to_mt4);
                    using (var order_reader = cmd.ExecuteReader())
                    {
                        while (order_reader.Read())
                        {
                            dynamic item = new ExpandoObject();
                            item.mt4 = int.Parse(order_reader["mt4_to"].ToString());
                            item.volume = int.Parse(order_reader["volume"].ToString());
                            item.direction =
                                int.Parse(order_reader["direction"].ToString());
                            item.order = int.Parse(order_reader["order_to"].ToString());
                            item.source = int.Parse(order_reader["source"].ToString());
                            var args = new TradeTransInfoArgsResult
                            {
                                type = TradeTransInfoTypes.TT_BR_ORDER_DELETE,
                                cmd = (short)item.direction,
                                order = item.order,
                                volume = item.volume,
                            };
                            MT4Wrapper api = null;
                            if (item.source == 0)
                                api = Poll.New();
                            else
                                api = Poll.DemoAPI();
                            using (api)
                            {
                                var result = api.TradeTransaction(ref args);
                                if (result == RET_CODE.RET_OK)
                                {
                                    Utils.CommonLog.Info("用户{0}关闭copy功能成功平仓{1}订单，报价{2}",
                                        args.orderby, args.order, args.price);
                                }
                            }
                        }
                    }
                }
            }
            sql = "DELETE FROM copy WHERE from_mt4=@mt4_from AND to_mt4=@mt4_to";
            using (var cmd = new SQLiteCommand(sql, db))
            {
                cmd.Parameters.AddWithValue("@mt4_from", from_mt4);
                cmd.Parameters.AddWithValue("@mt4_to", to_mt4);
                cmd.ExecuteNonQuery();
            }
            sql = "DELETE FROM copy_order WHERE mt4_to=@mt4_to";
            using (var cmd = new SQLiteCommand(sql, db))
            {
                cmd.Parameters.AddWithValue("@mt4_to", to_mt4);
                cmd.ExecuteNonQuery();
            }
            aIO.Output = JsonConvert.SerializeObject(
                Utils.MakeResponseObject(true, 0, string.Empty));
        }
    }
}
