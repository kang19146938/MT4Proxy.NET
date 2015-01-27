using MT4Proxy.NET.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    [MT4Service(DisableMT4 = true)]
    class CopyInfo: IService
    {
        public void OnRequest(IInputOutput aIO, dynamic aJson)
        {
            var from_mt4 = Convert.ToInt32(aJson["from_mt4"]);
            var real_mt4 = Convert.ToInt32(aJson["real_mt4"]);
            var demo_mt4 = Convert.ToInt32(aJson["demo_mt4"]);
            var dock = new DockServer();
            var real_amount = string.Empty;
            var demo_amount = string.Empty;
            var copy_count = 0;
            using(var db = dock.CopySource)
            {
                var sql = string.Empty;
                sql = "SELECT COUNT(*) FROM copy WHERE from_mt4=@mt4_from";
                using(var cmd = new SQLiteCommand(sql, db))
                {
                    cmd.Parameters.AddWithValue("@mt4_from", from_mt4);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null)
                        copy_count = Convert.ToInt32(obj);
                }
                sql = "SELECT amount FROM copy WHERE from_mt4=@mt4_from AND to_mt4=@mt4_to";
                using (var cmd = new SQLiteCommand(sql, db))
                {
                    cmd.Parameters.AddWithValue("@mt4_from", from_mt4);
                    cmd.Parameters.AddWithValue("@mt4_to", real_mt4);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null)
                        real_amount = Convert.ToDouble(obj).ToString("0.00");
                    else
                        real_amount = null;
                }
                sql = "SELECT amount FROM copy WHERE from_mt4=@mt4_from AND to_mt4=@mt4_to";
                using (var cmd = new SQLiteCommand(sql, db))
                {
                    cmd.Parameters.AddWithValue("@mt4_from", from_mt4);
                    cmd.Parameters.AddWithValue("@mt4_to", demo_mt4);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null)
                        demo_amount = Convert.ToDouble(obj).ToString("0.00");
                    else
                        demo_amount = null;
                }
            }
            var resp = Utils.MakeResponseObject(true, 0, "");
            resp.real_amount = real_amount;
            resp.demo_amount = demo_amount;
            resp.copy_count = copy_count;
            aIO.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
