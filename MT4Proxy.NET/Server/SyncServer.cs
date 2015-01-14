using MT4CliWrapper;
using MySql.Data.MySqlClient;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Dynamic;
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

        public void SyncSummary()
        {
            var logger = Utils.CommonLog;
            var start_date = TimeServer.Now.AddDays(-360);
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
            sql = "SELECT mt4_id, timestamp, cmd, leverage, digits, volume, pov, " +
                "storage, profit, pip_coefficient, open_price, close_price " + 
                "FROM history WHERE mt4_id=@mt4_id AND timestamp>=@timestamp";
            var working_pairs = new Tuple<int, IEnumerable<dynamic>>[users.Count];
            int i = 0;
            foreach(var mt4_id in users)
            {
                var lstOrders = new List<dynamic>();
                using (var cmd = new MySqlCommand(sql, Source.MysqlSource))
                {
                    cmd.Parameters.AddWithValue("@mt4_id", mt4_id);
                    cmd.Parameters.AddWithValue("@timestamp", start_date);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dynamic item = new ExpandoObject();
                            item.mt4_id = (int)reader["mt4_id"];
                            item.timestamp = (DateTime)reader["timestamp"];
                            item.cmd = Convert.ToInt32(reader["cmd"]);
                            item.leverage = (int)reader["leverage"];
                            item.digits = Convert.ToInt32(reader["digits"]);
                            item.volume = (int)reader["volume"];
                            item.pov = Convert.ToDouble(reader["pov"]);
                            item.storage =  Convert.ToDouble(reader["storage"]);
                            item.profit =  Convert.ToDouble(reader["profit"]);
                            item.pip_coefficient = Convert.ToDouble(reader["pip_coefficient"]);
                            item.open_price = Convert.ToDouble(reader["open_price"]);
                            item.close_price = Convert.ToDouble(reader["close_price"]);
                            lstOrders.Add(item);        
                        }
                        working_pairs[i++] = new Tuple<int, IEnumerable<dynamic>>
                            (mt4_id, lstOrders);  
                    }
                }
            }
            var taskArray = new Task[users.Count];
            var queResults = new ConcurrentQueue<dynamic>();
            for (i = 0; i < taskArray.Length; i++)
            {
                taskArray[i] = Task.Factory.StartNew((Object obj) =>
                {
                    var date_now = TimeServer.Now;
                    var data = obj as Tuple<int, IEnumerable<dynamic>>;
                    var mt4_id = data.Item1;
                    var orders = data.Item2;
                    dynamic result = new ExpandoObject();
                    //360days
                    orders = orders.Where(x => x.timestamp >= date_now.AddDays(-360));
                    result.range360 = 
                        CalcInDateRange(orders, date_now.AddDays(-360));
                    //180days
                    orders = orders.Where(x => x.timestamp >= date_now.AddDays(-180));
                    result.range180 = 
                        CalcInDateRange(orders, date_now.AddDays(-180));
                    //90days
                    orders = orders.Where(x => x.timestamp >= date_now.AddDays(-90));
                    result.range90 = 
                        CalcInDateRange(orders, date_now.AddDays(-90));
                    //30days
                    orders = orders.Where(x => x.timestamp >= date_now.AddDays(-30));
                    result.range30 = 
                        CalcInDateRange(orders, date_now.AddDays(-30));
                    //7days
                    orders = orders.Where(x => x.timestamp >= date_now.AddDays(-7));
                    result.range7 = 
                        CalcInDateRange(orders, date_now.AddDays(-7));
                    result.mt4_id = mt4_id;
                    queResults.Enqueue(result);
                },
                working_pairs[i]);
            }
            Utils.CommonLog.Info("开始并行分析单子zzZZZ");
            Task.WaitAll(taskArray);
            Utils.CommonLog.Info("准备保存统计数据");
            foreach(var result in queResults.ToArray())
            {
                var r360 = result.range360;
                var range = 360;
                SaveSummaryItem(r360, range, result.mt4_id);
                var r180 = result.range180;
                range = 180;
                SaveSummaryItem(r180, 180, result.mt4_id);
                var r90 = result.range90;
                range = 90;
                SaveSummaryItem(r90, 90, result.mt4_id);
                var r30 = result.range30;
                range = 30;
                SaveSummaryItem(r30, 30, result.mt4_id);
                var r7 = result.range7;
                range = 7;
                SaveSummaryItem(r7, 7, result.mt4_id);
            }
        }

        private void SaveSummaryItem(dynamic range_result, int range, int mt4_id)
        {
            var sql = "INSERT INTO rate(mt4_id, `range`, order_count, total_profit_rate, " +
                "total_volume, total_pips, profit_rate, max_profit, min_profit, " +
                "avg_profit, deficit_rate, max_deficit, min_deficit, avg_deficit) " +
                "VALUES(@mt4_id, @range, @order_count, @total_profit_rate, " +
                "@total_volume, @total_pips, @profit_rate, @max_profit, @min_profit, " +
                "@avg_profit, @deficit_rate, @max_deficit, @min_deficit, @avg_deficit) " +
                "ON DUPLICATE KEY UPDATE " +
                "order_count=@order_count, total_profit_rate=@total_profit_rate, " +
                "total_volume=@total_volume, total_pips=@total_pips, profit_rate=@profit_rate, max_profit=@max_profit, min_profit=@min_profit, " +
                "avg_profit=@avg_profit, deficit_rate=@deficit_rate, max_deficit=@max_deficit, min_deficit=@min_deficit, avg_deficit=@avg_deficit";
            try
            {
                using (var cmd = new MySqlCommand(sql, Source.MysqlSource))
                {
                    cmd.Parameters.AddWithValue("@mt4_id", mt4_id);
                    cmd.Parameters.AddWithValue("@range", range);
                    cmd.Parameters.AddWithValue("@order_count", range_result.order_count);
                    cmd.Parameters.AddWithValue("@total_profit_rate", range_result.total_profit_rate);
                    cmd.Parameters.AddWithValue("@total_volume", range_result.total_volume);
                    cmd.Parameters.AddWithValue("@total_pips", range_result.total_pips);
                    cmd.Parameters.AddWithValue("@profit_rate", range_result.profit_rate);
                    cmd.Parameters.AddWithValue("@max_profit", range_result.max_profit);
                    cmd.Parameters.AddWithValue("@min_profit", range_result.min_profit);
                    cmd.Parameters.AddWithValue("@avg_profit", range_result.avg_profit);
                    cmd.Parameters.AddWithValue("@deficit_rate", range_result.deficit_rate);
                    cmd.Parameters.AddWithValue("@max_deficit", range_result.max_deficit);
                    cmd.Parameters.AddWithValue("@min_deficit", range_result.min_deficit);
                    cmd.Parameters.AddWithValue("@avg_deficit", range_result.avg_deficit);
                    cmd.ExecuteNonQuery();
                }
            }
            catch(Exception e)
            {
                Utils.CommonLog.Error("保存统计数据出现了问题,{0},{1}", e.Message, e.StackTrace);
            }
        }

        private static dynamic CalcInDateRange(IEnumerable<dynamic> orders, DateTime begin)
        {
            var profit_rate = (0.0).ToString("0.00");
            var deficit_rate = (0.0).ToString("0.00");
            var total_profit_rate = (0.0).ToString("0.00");
            var total_volume = (0.0).ToString("0.00");
            var total_pips = (0.0).ToString("0.0");
            var max_profit = (0.0).ToString("0.00");
            var min_profit = (0.0).ToString("0.00");
            var avg_profit = (0.0).ToString("0.00");
            var max_deficit = (0.0).ToString("0.00");
            var min_deficit = (0.0).ToString("0.00");
            var avg_deficit = (0.0).ToString("0.00");
            var count = orders.Count();
            try
            {
                if (count > 0)
                {
                    var profits = orders.Where(x => x.profit + x.storage >= 0.0);
                    var deficits = orders.Where(x => x.profit + x.storage < 0.0);

                    var profit_count = profits.Count();
                    var deficit_count = deficits.Count();

                    profit_rate = ((double)profit_count * 100 / count)
                        .ToString("0.00");
                    deficit_rate = ((double)deficit_count * 100 / count)
                        .ToString("0.00");

                    total_profit_rate = (orders.Sum(x => (double)(x.profit + x.storage))
                        / orders.Sum(x => (double)(x.pov * x.volume / x.leverage)))
                        .ToString("0.00");
                    total_volume = (orders.Sum(x => x.volume) / 100.0).ToString("0.00");
                    total_pips = orders.Sum(x =>
                        (double)((x.close_price - x.open_price) * Math.Pow(10, x.digits - 3) *
                        (x.cmd * -2 + 1) * x.volume * x.pip_coefficient)).ToString("0.0");

                    if (profits.Count() > 0)
                    {
                        max_profit = profits.Max(x => (double)((x.profit + x.storage) * x.leverage /
                            (x.pov * x.volume))).ToString("0.00");
                        min_profit = profits.Min(x => (double)((x.profit + x.storage) * x.leverage /
                            (x.pov * x.volume))).ToString("0.00");
                        avg_profit = profits.Average(x => (double)((x.profit + x.storage) * x.leverage /
                            (x.pov * x.volume))).ToString("0.00");
                    }
                    if (deficits.Count() > 0)
                    {
                        max_deficit = deficits.Min(x => (double)((x.profit + x.storage) * x.leverage /
                            (x.pov * x.volume))).ToString("0.00");
                        min_deficit = deficits.Max(x => (double)((x.profit + x.storage) * x.leverage /
                            (x.pov * x.volume))).ToString("0.00");
                        avg_deficit = deficits.Average(x => (double)((x.profit + x.storage) * x.leverage /
                            (x.pov * x.volume))).ToString("0.00");
                    }
                }
            }
            catch(Exception e)
            {
                Utils.CommonLog.Error("计算用户订单统计出现问题{0},{1}", e.Message, e.StackTrace);
            }
            var order_count = count.ToString();
            dynamic result = new ExpandoObject();
            result.order_count = order_count;
            result.profit_rate = profit_rate;
            result.deficit_rate = deficit_rate;
            result.total_profit_rate = total_profit_rate;
            result.total_volume = total_volume;
            result.total_pips = total_pips;
            result.max_profit = max_profit;
            result.min_profit = min_profit;
            result.avg_profit = avg_profit;
            result.max_deficit = max_deficit;
            result.min_deficit = min_deficit;
            result.avg_deficit = avg_deficit;
            return result;
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
