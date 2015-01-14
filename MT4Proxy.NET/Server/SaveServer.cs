using MT4CliWrapper;
using MT4Proxy.NET.Core;
using MT4Proxy.NET.EventArg;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using NLog;
using NLog.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace MT4Proxy.NET
{
    internal class SaveServer: ConfigBase, IServer
    {
        public SaveServer()
        {
            TradeSource = new DockServer();
            QuoteSource = new DockServer();
        }

        internal override void LoadConfig(ConfigurationManager aConfig)
        {
            RedisSocialTradeKey = aConfig.AppSettings["redis_social_trade_key"];
        }

        private static string RedisSocialTradeKey
        {
            get;
            set;
        }

        private bool EnableRunning = false;

        public void Initialize()
        {
            CFD_List = new Dictionary<string, double>();
            CFD_List["WTOil"] = 450000;
            CFD_List["USDX"] = 450000;
            CFD_List["DAX"] = 150000;
            CFD_List["FFI"] = 150000;
            CFD_List["NK"] = 20000;
            CFD_List["HSI"] = 30000;
            CFD_List["SFC"] = 100000;
            CFD_List["mDJ"] = 200000;
            CFD_List["mND"] = 400000;
            CFD_List["mSP"] = 200000;
            EnableRunning = true;
            PumpServer.OnNewQuote += WhenNewQuote;
            CopyServer.OnNewTrade += WhenNewTrade;
            if (_quoteTimer == null)
            {
                _quoteTimer = new Timer(10000);
                _quoteTimer.Elapsed += SaveQuoteProc;
                SaveQuoteProc(_quoteTimer, null);
            }
            if(_tradeThread == null)
            {
                _tradeThread = new System.Threading.Thread(
                    () => 
                    {
                        while(Utils.SignalWait(ref EnableRunning, _tradeSignal))
                        {
                            TradeInfoEventArgs item = null;
                            _queTrades.TryDequeue(out item);
                            PushTrade(item);
                        }
                        foreach (var item in _queTrades.ToArray())
                            PushTrade(item);
                        ServerContainer.FinishStop();
                    });
                _tradeThread.IsBackground = true;
                _tradeThread.Start();
            }
            var logger = Utils.CommonLog;
            logger.Info("订单与报价同步服务已经启动");
        }

        public void Stop()
        {
            EnableRunning = false;
            PumpServer.OnNewQuote -= WhenNewQuote;
            CopyServer.OnNewTrade -= WhenNewTrade;
        }

        private void SaveQuoteProc(object sender, ElapsedEventArgs e)
        {
            var timer = sender as Timer;
            timer.Stop();
            var dictBuffer = new Dictionary<Tuple<string, DateTime>, Tuple<double, double>>();
            while (!_queQuote.IsEmpty)
            {
                QuoteInfoEventArgs item = null;
                _queQuote.TryDequeue(out item);
                var dayFormat = item.Timestamp;
                var key = new Tuple<string, DateTime>(item.Symbol, dayFormat);
                if (!dictBuffer.ContainsKey(key))
                    dictBuffer[key] = new Tuple<double, double>(item.Ask, item.Bid);
            }
            var items = dictBuffer.Select(i => new Tuple<string, double, double, DateTime>
                (i.Key.Item1, i.Value.Item1, i.Value.Item2, i.Key.Item2));
            UpdateQuote(items);
            if(EnableRunning)
                timer.Start();
        }

        private DockServer TradeSource
        {
            get;
            set;
        }

        private DockServer QuoteSource
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
                cmd.Connection = QuoteSource.MysqlSource;
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
                        var logger = Utils.CommonLog;
                        logger.Error(string.Format(
                            "行情MySQL操作失败,错误信息{0}\nSymbol:{1},Date:{2},Ask:{3},Bid:{4}",
                            e.Message, i.Item1, i.Item4, i.Item2, i.Item3));
                        TradeSource.MysqlSource = null;
                    }
                }
            }
        }

        public void PushTrade(TradeInfoEventArgs aTrade)
        {
            string symbolPattern = @"^(?<symbol>[A-Za-z]+)(?<leverage>\d*)$";
            string symbolPattern_CFD = @"^(?<symbol>[A-Za-z]+)_(?<number>\d*)$";
            var recordString = string.Empty;
            try
            {
                foreach (Match j in Regex.Matches(aTrade.Trade.symbol, symbolPattern))
                {
                    var leverage = 100;
                    var match = j.Groups;
                    if (!string.IsNullOrWhiteSpace(match["leverage"].ToString()))
                        leverage = int.Parse(match["leverage"].ToString());
                    Trade2Social(aTrade);
                    Trade2Mysql(aTrade.TradeType, aTrade.Trade, j, leverage, 100000, 1.0f,
                        aTrade.FromUsercode, aTrade.ToUsercode);
                }
                foreach (Match j in Regex.Matches(aTrade.Trade.symbol, symbolPattern_CFD))
                {
                    var match = j.Groups;
                    var symbol = match["symbol"].ToString();
                    var pov = 100000.0;
                    if (CFD_List.ContainsKey(symbol))
                        pov = CFD_List[symbol];
                    Trade2Social(aTrade);
                    Trade2Mysql(aTrade.TradeType, aTrade.Trade, j, 100, pov, 10.0f);
                }
            }
            catch (Exception e)
            {
                recordString = JsonConvert.SerializeObject(aTrade);
                var logger = Utils.CommonLog;
                if (e is MySqlException)
                {
                    var e_mysql = e as MySqlException;
                    if (e_mysql.Number == 1062 && e_mysql.Message.Contains("cb_unique") && e_mysql.Message.Contains("Duplicate entry"))
                    {
                        logger.Info(string.Format("交易MySQL数据已存在,单号:{0}\n原始数据:{1}", aTrade.Trade.order, recordString));
                        return;
                    }
                }
                logger.Error(string.Format("交易MySQL操作失败,错误信息{0}\n原始数据:{1}", e.Message, recordString));
                TradeSource.MysqlSource = null;
            }
        }

        private void Trade2Social(TradeInfoEventArgs e)
        {
            try
            {
                if (e.TradeType != TRANS_TYPE.TRANS_DELETE)
                    return;
                var key = RedisSocialTradeKey;
                var recordString = JsonConvert.SerializeObject(e);
                TradeSource.RedisSocial.LPush(key, recordString);
            }
            catch(Exception excp)
            {
                Utils.CommonLog.Error("同步到social数据库失败,{0},{1}", excp.Message, excp.StackTrace);
            }
        }

        private void Trade2Mysql(TRANS_TYPE aType, TradeRecordResult aRecord, Match j, 
            int leverage, double pov, float pip_coefficient = 1.0f, 
            string aFromCode=null, string aToCode=null)
        {
            var sql = string.Empty;
            var match = j.Groups;
            aRecord.symbol = match["symbol"].ToString();
            using (var cmd = new MySqlCommand())
            {
                cmd.Connection = TradeSource.MysqlSource;
                if (aType == TRANS_TYPE.TRANS_ADD)
                {
                    sql = "INSERT INTO `order`(mt4_id, order_id, " +
                        "login, symbol, digits, cmd, volume, open_time, " +
                        "state, open_price, sl, tp, close_time, value_date, " +
                        "expiration, reason, commission, commission_agent, storage, " +
                        "close_price, profit, taxes, magic, comment, internal_id, " +
                        "activation, spread, margin_rate, leverage, pov, pip_coefficient, " + 
                        "from_code, to_code) " +
                        " VALUES(@mt4id, @orderid, " +
                        "@login, @symbol, @digits, @cmd, @volume, @open_time, " +
                        "@state, @open_price, @sl, @tp, @close_time, @value_date, " +
                        "@expiration, @reason, @commission, @commission_agent, @storage, " +
                        "@close_price, @profit, @taxes, @magic, @comment, @internal_id, " +
                        "@activation, @spread, @margin_rate, @leverage, @pov, @pip_coefficient, " +
                        "@from_code, @to_code) " +
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
                    cmd.Parameters.AddWithValue("@from_code", aFromCode);
                    cmd.Parameters.AddWithValue("@to_code", aToCode);
                    cmd.ExecuteNonQuery();
                }
                else if (aType == TRANS_TYPE.TRANS_DELETE)
                {
                    sql = "INSERT INTO history(mt4_id, timestamp, order_id, " +
                        "login, symbol, digits, cmd, volume, open_time, " +
                        "state, open_price, sl, tp, close_time, value_date, " +
                        "expiration, reason, commission, commission_agent, storage, " +
                        "close_price, profit, taxes, magic, comment, internal_id, " +
                        "activation, spread, margin_rate, leverage, pov, pip_coefficient, " + 
                        "from_code, to_code) " +
                        "VALUES(@mt4id, @timestamp, @orderid, " +
                        "@login, @symbol, @digits, @cmd, @volume, @open_time, " +
                        "@state, @open_price, @sl, @tp, @close_time, @value_date, " +
                        "@expiration, @reason, @commission, @commission_agent, @storage, " +
                        "@close_price, @profit, @taxes, @magic, @comment, @internal_id, " +
                        "@activation, @spread, @margin_rate, @leverage, @pov, @pip_coefficient, " + 
                        "@from_code, @to_code) " +
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
                    cmd.Parameters.AddWithValue("@from_code", aFromCode);
                    cmd.Parameters.AddWithValue("@to_code", aToCode);
                    cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    cmd.CommandText = "DELETE FROM `order` WHERE order_id = @orderid";
                    cmd.Parameters.AddWithValue("@orderid", aRecord.order);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        void WhenNewTrade(object sender, TradeInfoEventArgs e)
        {
            if (EnableRunning)
            {
                _queTrades.Enqueue(e);
                _tradeSignal.Release();
            }
        }

        void WhenNewQuote(object sender, QuoteInfoEventArgs e)
        {
            _queQuote.Enqueue(e);
        }

        private ConcurrentQueue<QuoteInfoEventArgs>
            _queQuote = new ConcurrentQueue<QuoteInfoEventArgs>();
        private static ConcurrentQueue<TradeInfoEventArgs>
            _queTrades = new ConcurrentQueue<TradeInfoEventArgs>();
        private Timer _quoteTimer = null;
        private System.Threading.Thread _tradeThread = null;
        private static System.Threading.Semaphore _tradeSignal = 
            new System.Threading.Semaphore(0, 20000);
        
    }
}
