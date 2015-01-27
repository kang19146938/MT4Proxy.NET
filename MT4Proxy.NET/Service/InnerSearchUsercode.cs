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
    class InnerSearchUsercode : IService
    {
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            var source = new DockServer();
            var sql = string.Empty;
            var ucode = 0;
            var from_ucode = 0;
            sql = "SELECT user_code FROM user WHERE mt4=@mt4_id";
            using (var db = source.CopySource)
            {
                using (var cmd = new SQLiteCommand(sql, db))
                {
                    cmd.Parameters.AddWithValue("@mt4_id", Convert.ToInt32(aJson["mt4_id"]));
                    var from_obj = cmd.ExecuteScalar();
                    if (from_obj != null)
                        ucode = Convert.ToInt32(from_obj);
                }
                sql = "SELECT user_code FROM copy_order LEFT JOIN user ON copy_order.mt4_from=user.mt4 WHERE order_to=@order_id";
                using (var cmd = new SQLiteCommand(sql, db))
                {
                    cmd.Parameters.AddWithValue("@order_id", Convert.ToInt32(aJson["order_id"]));
                    var from_obj = cmd.ExecuteScalar();
                    if (from_obj != null)
                        from_ucode = Convert.ToInt32(from_obj);
                }
            }
            dynamic resp = new ExpandoObject();
            resp.is_succ = true;
            resp.ucode = ucode;
            resp.from_ucode = from_ucode;
            resp.err_msg = "";
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
}
