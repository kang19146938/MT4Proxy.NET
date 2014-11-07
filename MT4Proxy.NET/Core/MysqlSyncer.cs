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

namespace MT4Proxy.NET.Core
{
    class MysqlSyncer
    {
        public MysqlSyncer()
        {
            CreateTime = DateTime.MinValue;
        }

        private MySqlConnection _connection = null;
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
                        Logger logger = LogManager.GetLogger("common");
                        logger.Warn(
                            string.Format("MySQL连接建立失败，一秒之后重试，剩余机会{0}",
                            RetryTimes + 1), e);
                        Thread.Sleep(1000);
                        continue;
                    }
                }
                if (RetryTimes == -1)
                {
                    Logger logger = LogManager.GetLogger("common");
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
                            Logger logger = LogManager.GetLogger("common");
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
            var recordString = string.Empty;
            var sql = string.Empty;
            try
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.Connection = Connection;
                    recordString = JsonConvert.SerializeObject(aRecord);
                    if(aType == TRANS_TYPE.TRANS_ADD)
                    {
                        sql = "INSERT INTO `order`(mt4_id, order_id, content) VALUES(@mt4id, @orderid, @content) " +
                            "ON DUPLICATE KEY UPDATE content = @content";
                        cmd.CommandText = sql;
                        cmd.Parameters.AddWithValue("@mt4id", aRecord.login);
                        cmd.Parameters.AddWithValue("@orderid", aRecord.order);
                        cmd.Parameters.AddWithValue("@content", recordString);
                        cmd.ExecuteNonQuery();
                    }
                    else if (aType == TRANS_TYPE.TRANS_DELETE)
                    {
                        sql = "INSERT INTO history(mt4_id, timestamp, order_id, content) VALUES(@mt4id, @timestamp, @orderid, @content) " +
                            "ON DUPLICATE KEY UPDATE content = @content";
                        cmd.CommandText = sql;
                        cmd.Parameters.AddWithValue("@mt4id", aRecord.login);
                        cmd.Parameters.AddWithValue("@timestamp", aRecord.timestamp.FromTime32());
                        cmd.Parameters.AddWithValue("@orderid", aRecord.order);
                        cmd.Parameters.AddWithValue("@content", recordString);
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                        cmd.CommandText = "DELETE FROM `order` WHERE order_id = @orderid";
                        cmd.Parameters.AddWithValue("@orderid", aRecord.order);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch(Exception e)
            {
                Logger logger = LogManager.GetLogger("common");
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

        public void SyncEquity()
        {
            Logger logger = LogManager.GetLogger("common");
            string sql = "SELECT DISTINCT mt4_real FROM user WHERE mt4_real IS NOT NULL;";
            var users = new List<int>();
            using(var cmd = new MySqlCommand(sql, Connection))
            {
                using(var mt4_reader = cmd.ExecuteReader())
                {
                    while (mt4_reader.Read())
                    {
                        var id = int.Parse(mt4_reader["mt4_real"].ToString());
                        users.Add(id);
                    }
                }
            }
            var mt4 = Poll.New();
            var dict = new Dictionary<int, double>();
            double result = 0;
            foreach(var id in users)
            {
                var status = mt4.GetEquity(id, ref result);
                if(status != RET_CODE.RET_OK)
                {
                    logger.Error(string.Format(
                        "同步MT4账户{0}的个人资产信息出现了问题", id));
                    continue;
                }
                dict[id] = result;
            }
            sql = "INSERT INTO equity(mt4_id, value) VALUES(@mt4id, @value)" +
                "ON DUPLICATE KEY UPDATE value = @value";
            foreach (var kv in dict)
            {
                try
                {
                    using (var cmd = new MySqlCommand(sql, Connection))
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@mt4id", kv.Key);
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

        public static string ConnectString
        {
            get;
            set;
        }
    }
}
