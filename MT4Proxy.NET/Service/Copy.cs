using MT4Proxy.NET.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using Newtonsoft.Json;
using MT4CliWrapper;

namespace MT4Proxy.NET.Service
{
    [MT4Service(DisableMT4=true)]
    class Copy : IService
    {
        public void OnRequest(IInputOutput aIO, dynamic aJson)
        {
            var from_mt4 = Convert.ToInt32(aJson["from_mt4"]);
            var to_mt4 = Convert.ToInt32(aJson["to_mt4"]);
            var amount = Convert.ToDouble(aJson["amount"]);
            var source = aJson["source"].ToString();
            var from_user_code = Convert.ToInt32(aJson["from_user_code"]);
            var to_user_code = Convert.ToInt32(aJson["to_user_code"]);
            var dock = new DockServer();
            var db = dock.CopySource;
            var sql = string.Empty;
            sql = "REPLACE INTO user(mt4 ,user_code) VALUES(@mt4_id, @user_code)";
            using (var cmd = new SQLiteCommand(sql, db))
            {
                cmd.Parameters.AddWithValue("@mt4_id", from_mt4);
                cmd.Parameters.AddWithValue("@user_code", from_user_code);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SQLiteCommand(sql, db))
            {
                cmd.Parameters.AddWithValue("@mt4_id", to_mt4);
                cmd.Parameters.AddWithValue("@user_code", to_user_code);
                cmd.ExecuteNonQuery();
            }
            if(amount < 100)
            {
                var result = Utils.MakeResponseObject(false, 1, "复制金额过小");
                aIO.Output = JsonConvert.SerializeObject(result);
                return;
            }
            MT4Wrapper api = null; 
            if(source == "demo")
            {
                api = Poll.DemoAPI();
            }
            else
            {
                api = Poll.New();
            }
            double equity = 0;
            double free = 0;
            double balance = 0;
            using (api)
            {
                api.GetEquity(to_mt4, ref equity, ref free, ref balance);
                if (balance == 0)
                {
                    var result = Utils.MakeResponseObject(false, 2, "余额不足");
                    aIO.Output = JsonConvert.SerializeObject(result);
                    return;
                }
                if (amount / balance > 0.5)
                {
                    var result = Utils.MakeResponseObject(false, 3, "复制比例过大");
                    aIO.Output = JsonConvert.SerializeObject(result);
                    return;
                }
            }
            var offset = 0.0;
            sql = "SELECT * FROM copy WHERE from_mt4=@mt4_from AND to_mt4=@mt4_to";
            using (var cmd = new SQLiteCommand(sql, db))
            {
                cmd.Parameters.AddWithValue("@mt4_from", from_mt4);
                cmd.Parameters.AddWithValue("@mt4_to", to_mt4);
                using (var group_reader = cmd.ExecuteReader())
                {
                    while (group_reader.Read())
                    {
                        offset = -double.Parse(group_reader["amount"].ToString());
                        break;
                    }
                }
            }
            sql = "SELECT COUNT(*) AS total_count, TOTAL(amount) as total_amount FROM " +
                    "copy WHERE to_mt4 = @mt4_id";
            using (var cmd = new SQLiteCommand(sql, db))
            {
                cmd.Parameters.AddWithValue("@mt4_id", to_mt4);
                using (var group_reader = cmd.ExecuteReader())
                {
                    while (group_reader.Read())
                    {
                        var count = 
                            int.Parse(group_reader["total_count"].ToString());
                        var total_amount = 
                            double.Parse(group_reader["total_amount"].ToString());
                        if(count > 9)
                        {
                            var result = Utils.MakeResponseObject(false, 4, "复制人数已达到上限");
                            aIO.Output = JsonConvert.SerializeObject(result);
                            return;
                        }
                        if (total_amount + amount + offset > balance)
                        {
                            var result = Utils.MakeResponseObject(false, 5, "余额不足");
                            aIO.Output = JsonConvert.SerializeObject(result);
                            return;
                        }
                        break;
                    }
                }
            }
            sql = "REPLACE INTO copy(from_mt4, to_mt4, source, amount) VALUES" +
                "(@from_mt4, @to_mt4, @source, @amount)";
            using (var cmd = new SQLiteCommand(sql, db))
            {
                cmd.Parameters.AddWithValue("@from_mt4", from_mt4);
                cmd.Parameters.AddWithValue("@to_mt4", to_mt4);
                cmd.Parameters.AddWithValue("@source", source == "demo" ? 1 : 0);
                cmd.Parameters.AddWithValue("@amount", amount);
                cmd.ExecuteNonQuery();
            }
            aIO.Output = JsonConvert.SerializeObject(
                Utils.MakeResponseObject(true, 0, string.Empty));
        }
    }
}
