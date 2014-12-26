using MT4CliWrapper;
using MySql.Data.MySqlClient;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    class SyncServer : ConfigBase, IServer
    {
        public SyncServer()
        {
            Source = new DockServer();
        }
        internal override void LoadConfig(NLog.Internal.ConfigurationManager aConfig)
        {
            AccountMT4FieldName = aConfig.AppSettings["account_field_name"];
        }
        public void Initialize()
        {
            
        }

        public void Stop()
        {
            ServerContainer.FinishStop();
        }

        internal static string AccountMT4FieldName
        {
            get;
            set;
        }

        private DockServer Source
        {
            get;
            set;
        }
        public void SyncMaster()
        {
            var logger = Utils.CommonLog;
            logger.Info("准备开始同步高手榜到MySQL");
            var now = DateTime.UtcNow.Date;
            var start_date = DateTime.UtcNow.AddDays(-30).Date;
            var sql_cmd = "SELECT mt4_id, SUM(`profit`+`storage`) / SUM(1000 * volume / leverage) AS profit_rate, " +
                "COUNT(*) AS total_orders, " +
                "SUM((close_price - open_price) * pow(10, digits -3) * (cmd * -2 + 1) * volume) AS total_pip " +
                "FROM tiger.history WHERE `timestamp` > @start_date " +
                "GROUP BY `mt4_id` ORDER BY profit_rate DESC limit 10;";
            var lstRate = new List<Tuple<int, double, int, double>>();
            using (var cmd = new MySqlCommand(sql_cmd, Source.MysqlSource))
            {
                cmd.Parameters.AddWithValue("@start_date", start_date);
                using (var master_reader = cmd.ExecuteReader())
                {
                    while (master_reader.Read())
                    {
                        var id = int.Parse(master_reader["mt4_id"].ToString());
                        var rate = double.Parse(master_reader["profit_rate"].ToString());
                        var pips = double.Parse(master_reader["total_pip"].ToString());
                        var orders = int.Parse(master_reader["total_orders"].ToString());
                        logger.Info(string.Format("MasterList|MT4ID:{0}, Rate:{1}", id, rate));
                        lstRate.Add(new Tuple<int, double, int, double>(id, rate, orders, pips));
                    }
                }
            }

            var dictProfitableCount = new Dictionary<int, double>();
            var dictProfile = new Dictionary<int, Tuple<string, string, string>>();
            foreach (var item in lstRate)
            {
                sql_cmd = "SELECT COUNT(*) AS profitable_count FROM history WHERE mt4_id = @mt4id AND `timestamp` > @start_date AND `profit` + `storage` > 0";
                using (var cmd = new MySqlCommand(sql_cmd, Source.MysqlSource))
                {
                    cmd.Parameters.AddWithValue("@mt4id", item.Item1);
                    cmd.Parameters.AddWithValue("@start_date", start_date);
                    using (var master_reader = cmd.ExecuteReader())
                    {
                        while (master_reader.Read())
                        {
                            var count = int.Parse(master_reader["profitable_count"].ToString());
                            logger.Info(string.Format("MasterList|MT4ID:{0}, ProfitableCount:{1}", item.Item1, count));
                            dictProfitableCount[item.Item1] = count;
                            break;
                        }
                    }
                }

                sql_cmd = string.Format("SELECT * FROM user WHERE {0} = @mt4id;",
                    AccountMT4FieldName);
                using (var cmd = new MySqlCommand(sql_cmd, Source.MysqlAccount))
                {
                    cmd.Parameters.AddWithValue("@mt4id", item.Item1);
                    using (var master_reader = cmd.ExecuteReader())
                    {
                        while (master_reader.Read())
                        {
                            var username = master_reader["username"].ToString();
                            var sex = master_reader["sex"].ToString();
                            var avatar = string.Empty;
                            dictProfile[item.Item1] = new Tuple<string, string, string>(username, sex, avatar);
                            logger.Info(string.Format("MasterList|MT4ID:{0}, username:{1}, sex:{2}, avatar:{3}",
                                item.Item1, username, sex, avatar));
                            break;
                        }
                    }
                }
                if (dictProfile.ContainsKey(item.Item1) && dictProfitableCount.ContainsKey(item.Item1))
                {
                    sql_cmd = "INSERT INTO `master`(`date`,`username`,`sex`,`orders`, " +
                    "`profit_rate`,`pips`, `percent_profitable`, `mt4_id`) " +
                    "VALUES(@date, @username, @sex, @orders, @profit_rate, @pips, " +
                    "@percent_profitable, @mt4_id);";
                    var percent_profitable = 0.0;
                    if (item.Item3 != 0)
                        percent_profitable = dictProfitableCount[item.Item1] / item.Item3;
                    using (var cmd = new MySqlCommand(sql_cmd, Source.MysqlSource))
                    {
                        cmd.Parameters.AddWithValue("@date", now);
                        cmd.Parameters.AddWithValue("@username", dictProfile[item.Item1].Item1);
                        cmd.Parameters.AddWithValue("@sex", dictProfile[item.Item1].Item2);
                        cmd.Parameters.AddWithValue("@orders", item.Item3);
                        cmd.Parameters.AddWithValue("@profit_rate", item.Item2);
                        cmd.Parameters.AddWithValue("@pips", item.Item4);
                        cmd.Parameters.AddWithValue("@percent_profitable", percent_profitable);
                        cmd.Parameters.AddWithValue("@mt4_id", item.Item1);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    logger.Info(string.Format("MasterList|MT4ID:{0} 数据残缺不能保存该记录", item.Item1));
                }
            }
        }

        public void SyncEquity()
        {
            var logger = Utils.CommonLog;
            logger.Info("准备开始同步equity信息到MySQL");
            string sql = string.Format(
                "SELECT DISTINCT {0} FROM user WHERE {0} IS NOT NULL;",
                AccountMT4FieldName);
            var users = new List<int>();
            using (var cmd = new MySqlCommand(sql, Source.MysqlAccount))
            {
                using (var mt4_reader = cmd.ExecuteReader())
                {
                    while (mt4_reader.Read())
                    {
                        var id = int.Parse(mt4_reader[AccountMT4FieldName].ToString());
                        users.Add(id);
                    }
                }
            }
            using (var mt4 = Poll.New())
            {
                var dict = new Dictionary<int, double>();
                double result = 0;
                double free = 0;
                double balance = 0;
                foreach (var id in users)
                {
                    var status = mt4.GetEquity(id, ref result, ref free, ref balance);
                    if (status != RET_CODE.RET_OK)
                    {
                        logger.Error(string.Format(
                            "同步MT4账户{0}的个人资产信息出现了问题", id));
                        continue;
                    }
                    dict[id] = result;
                }
                var mt4date = DateTime.UtcNow.AddHours(3).Date;
                sql = "INSERT INTO equity(mt4_id, date, value) VALUES(@mt4id, @date, @value)" +
                    "ON DUPLICATE KEY UPDATE value = @value";
                foreach (var kv in dict)
                {
                    try
                    {
                        using (var cmd = new MySqlCommand(sql, Source.MysqlSource))
                        {
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@mt4id", kv.Key);
                            cmd.Parameters.AddWithValue("@date", mt4date);
                            cmd.Parameters.AddWithValue("@value", kv.Value);
                            cmd.ExecuteNonQuery();
                        }
                        logger.Info(string.Format("已经同步用户{0}的资产", kv.Key));
                    }
                    catch
                    {
                        logger.Error(string.Format(
                            "同步MT4账户{0}的个人资产({1})信息出现了问题", kv.Key, kv.Value));
                    }
                }
            }
        }
    }
}
