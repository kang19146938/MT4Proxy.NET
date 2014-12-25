using MySql.Data.MySqlClient;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using MT4CliWrapper;
using System.Text.RegularExpressions;

namespace MT4Proxy.NET.Core
{
    class MysqlServer: ConfigBase
    {
        public MysqlServer()
        {
            CreateTime = DateTime.MinValue;
        }

        internal override void LoadConfig(NLog.Internal.ConfigurationManager aConfig)
        {
            MysqlServer.ConnectString = aConfig.AppSettings["mysql_cs"];
            MysqlServer.AccountConnectString = aConfig.AppSettings["mysql_account_cs"];
            MysqlServer.AccountMT4FieldName = aConfig.AppSettings["account_field_name"];
            MysqlServer.CFD_List = new Dictionary<string, double>();
            MysqlServer.CFD_List["WTOil"] = 450000;
            MysqlServer.CFD_List["USDX"] = 450000;
            MysqlServer.CFD_List["DAX"] = 150000;
            MysqlServer.CFD_List["FFI"] = 150000;
            MysqlServer.CFD_List["NK"] = 20000;
            MysqlServer.CFD_List["HSI"] = 30000;
            MysqlServer.CFD_List["SFC"] = 100000;
            MysqlServer.CFD_List["mDJ"] = 200000;
            MysqlServer.CFD_List["mND"] = 400000;
            MysqlServer.CFD_List["mSP"] = 200000;
        }

        private MySqlConnection _connection = null;
        private MySqlConnection _account_connection = null;

        private MySqlConnection AccountConnection
        {
            get
            {
                RetryTimes = 3;
                while (RetryTimes-- > 0)
                {
                    try
                    {
                        if (_account_connection == null || ((DateTime.Now - CreateTime).TotalSeconds > 30))
                        {
                            if (_account_connection != null)
                            {
                                try
                                {
                                    _account_connection.Close();
                                    _account_connection = null;
                                }
                                catch { }
                            }
                            _account_connection = new MySqlConnection(AccountConnectString);
                            _account_connection.Open();
                            CreateTime = DateTime.Now;
                        }
                        break;
                    }
                    catch (Exception e)
                    {
                        var logger = LogManager.GetLogger("common");
                        logger.Warn(
                            string.Format("MySQL连接建立失败，一秒之后重试，剩余机会{0}",
                            RetryTimes + 1), e);
                        Thread.Sleep(1000);
                        continue;
                    }
                }
                if (RetryTimes == -1)
                {
                    var logger = LogManager.GetLogger("common");
                    logger.Error("MySQL连接建立失败，请立即采取措施保障丢失的数据！");
                    return null;
                }
                else
                {
                    return _account_connection;
                }
            }
            set
            {
                _account_connection = value;
            }
        }

        private MySqlConnection Connection
        {
            get
            {
                RetryTimes = 3;
                while (RetryTimes-- > 0)
                {
                    try
                    {
                        if (_connection == null || ((DateTime.Now - CreateTime).TotalSeconds > 30))
                        {
                            if (_connection != null)
                            {
                                try
                                {
                                    _connection.Close();
                                    _connection = null;
                                }
                                catch { }
                            }
                            _connection = new MySqlConnection(ConnectString);
                            _connection.Open();
                            CreateTime = DateTime.Now;
                        }
                        break;
                    }
                    catch(Exception e)
                    {
                        var logger = LogManager.GetLogger("common");
                        logger.Warn(
                            string.Format("MySQL连接建立失败，一秒之后重试，剩余机会{0}",
                            RetryTimes + 1), e);
                        Thread.Sleep(1000);
                        continue;
                    }
                }
                if (RetryTimes == -1)
                {
                    var logger = LogManager.GetLogger("common");
                    logger.Error("MySQL连接建立失败，请立即采取措施保障丢失的数据！");
                    return null;
                }
                else
                {
                    return _connection;
                }
            }
            set
            {
                _connection = value;
            }
        }

        private DateTime CreateTime
        {
            get;
            set;
        }

        private int RetryTimes
        {
            get;
            set;
        }

        public static Dictionary<string, double> CFD_List
        {
            get;
            set;
        }

        public void UpdateQuote(IEnumerable<Tuple<string, double, double, DateTime>> aItems)
        {
                var sql = string.Empty;
                using (var cmd = new MySqlCommand())
                {
                    cmd.Connection = Connection;
                    sql = "INSERT INTO quote(symbol, date, ask, bid) VALUES(@symbol, @date, @ask, @bid) " +
                        "ON DUPLICATE KEY UPDATE ask = @ask, bid = @bid";
                    cmd.CommandText = sql;
                    foreach (var i in aItems)
                    {
                        try
                        {
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@symbol", i.Item1);
                            cmd.Parameters.AddWithValue("@date", i.Item4);
                            cmd.Parameters.AddWithValue("@ask", i.Item2);
                            cmd.Parameters.AddWithValue("@bid", i.Item3);
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e)
                        {
                            var logger = LogManager.GetLogger("common");
                            logger.Error(string.Format(
                                "行情MySQL操作失败,错误信息{0}\nSymbol:{1},Date:{2},Ask:{3},Bid:{4}",
                                e.Message, i.Item1, i.Item4, i.Item2, i.Item3));
                            Connection = null;
                        }
                    }
                }
        }

        public void PushTrade(TRANS_TYPE aType, TradeRecordResult aRecord)
        {
            string symbolPattern = @"^(?<symbol>[A-Za-z]+)(?<leverage>\d*)$";
            string symbolPattern_CFD = @"^(?<symbol>[A-Za-z]+)_(?<number>\d*)$";
            var recordString = string.Empty;
            recordString = JsonConvert.SerializeObject(aRecord);
            try
            {
                foreach (Match j in Regex.Matches(aRecord.symbol, symbolPattern))
                {
                    var leverage = 100;
                    var match = j.Groups;
                    if (!string.IsNullOrWhiteSpace(match["leverage"].ToString()))
                        leverage = int.Parse(match["leverage"].ToString());
                    Trade2Mysql(aType, aRecord, j, leverage, 100000);
                }
                foreach (Match j in Regex.Matches(aRecord.symbol, symbolPattern_CFD))
                {
                    var match = j.Groups;
                    var symbol = match["symbol"].ToString();
                    var pov = 100000.0;
                    if (CFD_List.ContainsKey(symbol))
                        pov = CFD_List[symbol];
                    Trade2Mysql(aType, aRecord, j, 100, pov, 10.0f);
                }
            }
            catch(Exception e)
            {
                var logger = LogManager.GetLogger("common");
                if(e is MySqlException)
                {
                    var e_mysql = e as MySqlException;
                    if(e_mysql.Number ==1062 && e_mysql.Message.Contains("cb_unique") && e_mysql.Message.Contains("Duplicate entry"))
                    {
                        logger.Info(string.Format("交易MySQL数据已存在,单号:{0}\n原始数据:{1}", aRecord.order, recordString));
                        return;
                    }
                }
                logger.Error(string.Format("交易MySQL操作失败,错误信息{0}\n原始数据:{1}", e.Message, recordString));
                Connection = null;
            }
        }

        private void Trade2Mysql(TRANS_TYPE aType, TradeRecordResult aRecord, Match j, int leverage, double pov, float pip_coefficient=1.0f)
        {
            var sql = string.Empty;
            var match = j.Groups;
            aRecord.symbol = match["symbol"].ToString();
            using (var cmd = new MySqlCommand())
            {
                cmd.Connection = Connection;
                if (aType == TRANS_TYPE.TRANS_ADD)
                {
                    sql = "INSERT INTO `order`(mt4_id, order_id, " +
                        "login, symbol, digits, cmd, volume, open_time, " +
                        "state, open_price, sl, tp, close_time, value_date, " +
                        "expiration, reason, commission, commission_agent, storage, " +
                        "close_price, profit, taxes, magic, comment, internal_id, " +
                        "activation, spread, margin_rate, leverage, pov, pip_coefficient) " +
                        " VALUES(@mt4id, @orderid, " +
                        "@login, @symbol, @digits, @cmd, @volume, @open_time, " +
                        "@state, @open_price, @sl, @tp, @close_time, @value_date, " +
                        "@expiration, @reason, @commission, @commission_agent, @storage, " +
                        "@close_price, @profit, @taxes, @magic, @comment, @internal_id, " +
                        "@activation, @spread, @margin_rate, @leverage, @pov, @pip_coefficient)" +
                        "ON DUPLICATE KEY UPDATE " +
                        "login=@login, symbol=@symbol, digits=@digits, cmd=@cmd, volume=@volume, open_time=@open_time, " +
                        "state=@state, open_price=@open_price, sl=@sl, tp=@tp, close_time=@close_time, value_date=@value_date, " +
                        "expiration=@expiration, reason=@reason, commission=@commission, commission_agent=@commission_agent, storage=@storage, " +
                        "close_price=@close_price, profit=@profit, taxes=@taxes, magic=@magic, comment=@comment, internal_id=@internal_id, " +
                        "activation=@activation, spread=@spread, margin_rate=@margin_rate, " +
                        "leverage=@leverage, pov=@pov, pip_coefficient=@pip_coefficient";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@mt4id", aRecord.login);
                    cmd.Parameters.AddWithValue("@orderid", aRecord.order);
                    cmd.Parameters.AddWithValue("@login", aRecord.login);
                    cmd.Parameters.AddWithValue("@symbol", aRecord.symbol);
                    cmd.Parameters.AddWithValue("@digits", aRecord.digits);
                    cmd.Parameters.AddWithValue("@cmd", aRecord.cmd);
                    cmd.Parameters.AddWithValue("@volume", aRecord.volume);
                    cmd.Parameters.AddWithValue("@open_time", aRecord.open_time.FromTime32());
                    cmd.Parameters.AddWithValue("@state", aRecord.state);
                    cmd.Parameters.AddWithValue("@open_price", aRecord.open_price);
                    cmd.Parameters.AddWithValue("@sl", aRecord.sl);
                    cmd.Parameters.AddWithValue("@tp", aRecord.tp);
                    cmd.Parameters.AddWithValue("@close_time", aRecord.close_time.FromTime32());
                    cmd.Parameters.AddWithValue("@value_date", aRecord.value_date);
                    cmd.Parameters.AddWithValue("@expiration", aRecord.expiration);
                    cmd.Parameters.AddWithValue("@reason", aRecord.reason);
                    cmd.Parameters.AddWithValue("@commission", aRecord.commission);
                    cmd.Parameters.AddWithValue("@commission_agent", aRecord.commission_agent);
                    cmd.Parameters.AddWithValue("@storage", aRecord.storage);
                    cmd.Parameters.AddWithValue("@close_price", aRecord.close_price);
                    cmd.Parameters.AddWithValue("@profit", aRecord.profit);
                    cmd.Parameters.AddWithValue("@taxes", aRecord.taxes);
                    cmd.Parameters.AddWithValue("@magic", aRecord.magic);
                    cmd.Parameters.AddWithValue("@comment", aRecord.comment);
                    cmd.Parameters.AddWithValue("@internal_id", aRecord.internal_id);
                    cmd.Parameters.AddWithValue("@activation", aRecord.activation);
                    cmd.Parameters.AddWithValue("@spread", aRecord.spread);
                    cmd.Parameters.AddWithValue("@margin_rate", aRecord.margin_rate);
                    cmd.Parameters.AddWithValue("@leverage", leverage);
                    cmd.Parameters.AddWithValue("@pov", pov);
                    cmd.Parameters.AddWithValue("@pip_coefficient", pip_coefficient);
                    cmd.ExecuteNonQuery();
                }
                else if (aType == TRANS_TYPE.TRANS_DELETE)
                {
                    sql = "INSERT INTO history(mt4_id, timestamp, order_id, " +
                        "login, symbol, digits, cmd, volume, open_time, " +
                        "state, open_price, sl, tp, close_time, value_date, " +
                        "expiration, reason, commission, commission_agent, storage, " +
                        "close_price, profit, taxes, magic, comment, internal_id, " +
                        "activation, spread, margin_rate, leverage, pov, pip_coefficient) " +
                        "VALUES(@mt4id, @timestamp, @orderid, " +
                        "@login, @symbol, @digits, @cmd, @volume, @open_time, " +
                        "@state, @open_price, @sl, @tp, @close_time, @value_date, " +
                        "@expiration, @reason, @commission, @commission_agent, @storage, " +
                        "@close_price, @profit, @taxes, @magic, @comment, @internal_id, " +
                        "@activation, @spread, @margin_rate, @leverage, @pov, @pip_coefficient)" +
                        "ON DUPLICATE KEY UPDATE " +
                        "login=@login, symbol=@symbol, digits=@digits, cmd=@cmd, volume=@volume, open_time=@open_time, " +
                        "state=@state, open_price=@open_price, sl=@sl, tp=@tp, close_time=@close_time, value_date=@value_date, " +
                        "expiration=@expiration, reason=@reason, commission=@commission, commission_agent=@commission_agent, storage=@storage, " +
                        "close_price=@close_price, profit=@profit, taxes=@taxes, magic=@magic, comment=@comment, internal_id=@internal_id, " +
                        "activation=@activation, spread=@spread, margin_rate=@margin_rate, " +
                        "leverage=@leverage, pov=@pov, pip_coefficient=@pip_coefficient";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@mt4id", aRecord.login);
                    cmd.Parameters.AddWithValue("@timestamp", aRecord.timestamp.FromTime32());
                    cmd.Parameters.AddWithValue("@orderid", aRecord.order);
                    cmd.Parameters.AddWithValue("@login", aRecord.login);
                    cmd.Parameters.AddWithValue("@symbol", aRecord.symbol);
                    cmd.Parameters.AddWithValue("@digits", aRecord.digits);
                    cmd.Parameters.AddWithValue("@cmd", aRecord.cmd);
                    cmd.Parameters.AddWithValue("@volume", aRecord.volume);
                    cmd.Parameters.AddWithValue("@open_time", aRecord.open_time.FromTime32());
                    cmd.Parameters.AddWithValue("@state", aRecord.state);
                    cmd.Parameters.AddWithValue("@open_price", aRecord.open_price);
                    cmd.Parameters.AddWithValue("@sl", aRecord.sl);
                    cmd.Parameters.AddWithValue("@tp", aRecord.tp);
                    cmd.Parameters.AddWithValue("@close_time", aRecord.close_time.FromTime32());
                    cmd.Parameters.AddWithValue("@value_date", aRecord.value_date);
                    cmd.Parameters.AddWithValue("@expiration", aRecord.expiration);
                    cmd.Parameters.AddWithValue("@reason", aRecord.reason);
                    cmd.Parameters.AddWithValue("@commission", aRecord.commission);
                    cmd.Parameters.AddWithValue("@commission_agent", aRecord.commission_agent);
                    cmd.Parameters.AddWithValue("@storage", aRecord.storage);
                    cmd.Parameters.AddWithValue("@close_price", aRecord.close_price);
                    cmd.Parameters.AddWithValue("@profit", aRecord.profit);
                    cmd.Parameters.AddWithValue("@taxes", aRecord.taxes);
                    cmd.Parameters.AddWithValue("@magic", aRecord.magic);
                    cmd.Parameters.AddWithValue("@comment", aRecord.comment);
                    cmd.Parameters.AddWithValue("@internal_id", aRecord.internal_id);
                    cmd.Parameters.AddWithValue("@activation", aRecord.activation);
                    cmd.Parameters.AddWithValue("@spread", aRecord.spread);
                    cmd.Parameters.AddWithValue("@margin_rate", aRecord.margin_rate);
                    cmd.Parameters.AddWithValue("@leverage", leverage);
                    cmd.Parameters.AddWithValue("@pov", pov);
                    cmd.Parameters.AddWithValue("@pip_coefficient", pip_coefficient);
                    cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    cmd.CommandText = "DELETE FROM `order` WHERE order_id = @orderid";
                    cmd.Parameters.AddWithValue("@orderid", aRecord.order);
                    cmd.ExecuteNonQuery();
                }
            }
        }


        public void SyncMaster()
        {
            var logger = LogManager.GetLogger("common");
            logger.Info("准备开始同步高手榜到MySQL");
            var now = DateTime.UtcNow.Date;
            var start_date = DateTime.UtcNow.AddDays(-30).Date;
            var sql_cmd = "SELECT mt4_id, SUM(`profit`+`storage`) / SUM(1000 * volume / leverage) AS profit_rate, " + 
                "COUNT(*) AS total_orders, " +
                "SUM((close_price - open_price) * pow(10, digits -3) * (cmd * -2 + 1) * volume) AS total_pip " +
                "FROM tiger.history WHERE `timestamp` > @start_date " + 
                "GROUP BY `mt4_id` ORDER BY profit_rate DESC limit 10;";
            var lstRate = new List<Tuple<int, double, int, double>>();
            using (var cmd = new MySqlCommand(sql_cmd, Connection))
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
            foreach(var item in lstRate)
            {
                sql_cmd = "SELECT COUNT(*) AS profitable_count FROM history WHERE mt4_id = @mt4id AND `timestamp` > @start_date AND `profit` + `storage` > 0";
                using (var cmd = new MySqlCommand(sql_cmd, Connection))
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
                using (var cmd = new MySqlCommand(sql_cmd, AccountConnection))
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
                if(dictProfile.ContainsKey(item.Item1) && dictProfitableCount.ContainsKey(item.Item1))
                {
                    sql_cmd = "INSERT INTO `master`(`date`,`username`,`sex`,`orders`, " +
                    "`profit_rate`,`pips`, `percent_profitable`, `mt4_id`) " +
                    "VALUES(@date, @username, @sex, @orders, @profit_rate, @pips, " +
                    "@percent_profitable, @mt4_id);";
                    var percent_profitable = 0.0;
                    if (item.Item3 != 0)
                        percent_profitable = dictProfitableCount[item.Item1] / item.Item3;
                    using (var cmd = new MySqlCommand(sql_cmd, Connection))
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
            var logger = LogManager.GetLogger("common");
            logger.Info("准备开始同步equity信息到MySQL");
            string sql = string.Format(
                "SELECT DISTINCT {0} FROM user WHERE {0} IS NOT NULL;",
                AccountMT4FieldName);
            var users = new List<int>();
            using(var cmd = new MySqlCommand(sql, AccountConnection))
            {
                using(var mt4_reader = cmd.ExecuteReader())
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
                foreach (var id in users)
                {
                    var status = mt4.GetEquity(id, ref result);
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
                        using (var cmd = new MySqlCommand(sql, Connection))
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

        public IEnumerable<KeyValuePair<string,string>> PullCopyData()
        {
            var list = new LinkedList<KeyValuePair<string, string>>();
            var sql_cmd = "SELECT * FROM copy;";
            using (var cmd = new MySqlCommand(sql_cmd, Connection))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var from = reader["from_mt4"].ToString();
                        var to = reader["to_mt4"].ToString();
                        list.AddLast(new LinkedListNode<KeyValuePair<string, string>>
                            (new KeyValuePair<string, string>(from, to)));
                    }
                }
            }
            return list;
        }

        internal static string ConnectString
        {
            get;
            set;
        }

        internal static string AccountConnectString
        {
            get;
            set;
        }

        internal static string AccountMT4FieldName
        {
            get;
            set;
        }
    }
}
