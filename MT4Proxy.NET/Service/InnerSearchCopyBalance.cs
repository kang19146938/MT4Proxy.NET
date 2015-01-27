using MT4CliWrapper;
using MT4Proxy.NET.Core;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Service
{
    [MT4Service(DisableMT4 = true)]
    class InnerCheckCopyBalance :IService
    {
        public void OnRequest(IInputOutput aServer, dynamic aJson)
        {
            var sql = string.Empty;
            var dock = new DockServer();
            var mt4_id = Convert.ToInt32(aJson["mt4_id"]);
            var balance = Convert.ToDouble(aJson["balance"]);
            //原生订单需要统计一下剩余保证金是否大于等于copy用金额
            //是的话，清除copy关系，但保持持仓copy订单
            var used_balance = 0.0;
            sql = "SELECT TOTAL(amount) FROM copy WHERE to_mt4=@mt4_id";
            using (var cmd = new SQLiteCommand(sql, dock.CopySource))
            {
                cmd.Parameters.AddWithValue("@mt4_id", mt4_id);
                var obj = cmd.ExecuteScalar();
                if (obj != null)
                    used_balance = Convert.ToDouble(obj);
            }
            //Q:余额是0或者负的
            //A:解除copy关系
            //Q:可用余额是负的
            //A:也解除
            if (balance <= 0 || balance - used_balance <= 0 ||
                balance < 2 * used_balance)
            {
                //解除here
                sql = "DELETE FROM copy WHERE to_mt4=@mt4_id";
                using (var cmd = new SQLiteCommand(sql, dock.CopySource))
                {
                    cmd.Parameters.AddWithValue("@mt4_id", mt4_id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
