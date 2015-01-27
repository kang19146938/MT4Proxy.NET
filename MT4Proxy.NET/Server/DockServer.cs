using CSRedis;
using MySql.Data.MySqlClient;
using NLog;
using NLog.Internal;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 对接数据库连接服务
    /// 对于单个线程、协程，需要独立配置该对象，不可共用
    /// </summary>
    internal class DockServer: ConfigBase,IServer
    {
        internal override void LoadConfig(ConfigurationManager aConfig)
        {
            ConnectString = aConfig.AppSettings["mysql_cs"];
            AccountConnectString = aConfig.AppSettings["mysql_account_cs"];
            RedisSocialHost = aConfig.AppSettings["redis_social_host"];
            RedisSocialPort = int.Parse(aConfig.AppSettings["redis_social_port"]);
            RedisSocialPasswd = aConfig.AppSettings["redis_social_password"];
            RedisSocialDb = int.Parse(aConfig.AppSettings["redis_social_db"]);
            RedisCopyHost = aConfig.AppSettings["redis_copy_host"];
            RedisCopyPort = int.Parse(aConfig.AppSettings["redis_copy_port"]);
            RedisCopyPasswd = aConfig.AppSettings["redis_copy_password"];
            RedisCopyDb = int.Parse(aConfig.AppSettings["redis_copy_db"]);
            CopyConnectString = aConfig.AppSettings["copy_cs"];
        }

        public DockServer()
        {
            _localSource = new DockItem() { Name = "LocalSource" };
            _localSource.Create = new Func<object>(() =>
            {
                var result = new SQLiteConnection(CopyConnectString);
                result.Open();
                return result;
            });
            _localSource.Shutdown = new Action<object>((i) =>
            {
                var connection = i as SQLiteConnection;
                connection.Close();
            });

            _mysqlSource = new DockItem() { Name = "MySQLSource" };
            _mysqlSource.Create = new Func<object>(() =>
            {
                var result = new MySqlConnection(ConnectString);
                result.Open();
                return result;
            });
            _mysqlSource.Shutdown = new Action<object>((i) =>
            {
                var connection = i as MySqlConnection;
                connection.Close();
            });

            _mysqlAccount = new DockItem() { Name = "MySQLAccount" };
            _mysqlAccount.Create = new Func<object>(() =>
            {
                var result = new MySqlConnection(AccountConnectString);
                result.Open();
                return result;
            });
            _mysqlAccount.Shutdown = new Action<object>((i) =>
            {
                var connection = i as MySqlConnection;
                connection.Close();
            });

            _redisSocial = new DockItem() { Name = "RedisSocial" };
            _redisSocial.Create = new Func<object>(() =>
            {
                var result = new RedisClient(RedisSocialHost, RedisSocialPort);
                result.Auth(RedisSocialPasswd);
                result.Select(RedisSocialDb);
                return result;
            });
            _redisSocial.Shutdown = new Action<object>((i) =>
            {
                var connection = i as RedisClient;
                connection.Dispose();
            });

            _redisCopy = new DockItem() { Name = "RedisCopy" };
            _redisCopy.Create = new Func<object>(() =>
            {
                var result = new RedisClient(RedisCopyHost, RedisCopyPort);
                result.Auth(RedisCopyPasswd);
                result.Select(RedisCopyDb);
                return result;
            });
            _redisCopy.Shutdown = new Action<object>((i) =>
            {
                var connection = i as RedisClient;
                connection.Dispose();
            });
        }

        public void Initialize()
        {
            var logger = Utils.CommonLog;
            logger.Info("数据对接服务已启动");
        }

        public void Stop()
        {
            ServerContainer.FinishStop();
        }

        private DockItem _mysqlSource = null;
        private DockItem _mysqlAccount = null;
        private DockItem _redisSocial = null;
        private DockItem _redisCopy = null;
        private DockItem _localSource = null;

        public SQLiteConnection CopySource
        {
            get
            {
                return Bones<SQLiteConnection>(_localSource);
            }
            set
            {
                _localSource.Connection = value;
            }
        }

        public MySqlConnection MysqlSource
        {
            get
            {
                return Bones<MySqlConnection>(_mysqlSource);
            }
            set
            {
                _mysqlSource.Connection = value;
            }
        }

        public MySqlConnection MysqlAccount
        {
            get
            {
                return Bones<MySqlConnection>(_mysqlAccount);
            }
            set
            {
                _mysqlAccount.Connection = value;
            }
        }

        public RedisClient RedisSocial
        {
            get
            {
                return Bones<RedisClient>(_redisSocial);
            }
            set
            {
                _redisSocial.Connection = value;
            }
        }

        public RedisClient RedisCopy
        {
            get
            {
                return Bones<RedisClient>(_redisCopy);
            }
            set
            {
                _redisCopy.Connection = value;
            }
        }

        private T Bones<T>(DockItem aItem) where T : IDisposable
        {
            aItem.RetryTimes = aItem.MaxRetryTimes;
            while (aItem.RetryTimes-- > 0)
            {
                try
                {
                    if (aItem.Connection == null ||
                        ((DateTime.Now - aItem.CreateTime).TotalSeconds > 30))
                    {
                        if (aItem.Connection != null)
                        {
                            try
                            {
                                aItem.Shutdown(aItem.Connection);
                                aItem.Connection = null;
                            }
                            catch { }
                        }
                        aItem.Connection = aItem.Create();
                        aItem.CreateTime = DateTime.Now;
                    }
                    break;
                }
                catch (Exception e)
                {
                    var logger = Utils.CommonLog;
                    logger.Warn(
                        string.Format("{0}连接建立失败，一秒之后重试，剩余机会{1}:{2}",
                        aItem.Name, aItem.RetryTimes + 1, e.Message), e);
                    Thread.Sleep(aItem.RetryPeriodMS);
                    continue;
                }
            }
            if (aItem.RetryTimes == -1)
            {
                var logger = Utils.CommonLog;
                logger.Error(string.Format("{0}连接建立失败，请立即采取措施保障丢失的数据！",
                    aItem.Name));
                return default(T);
            }
            else
            {
                return (T)aItem.Connection;
            }
        }


        private static string CopyConnectString
        {
            get;
            set;
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

        private static string RedisSocialHost
        {
            get;
            set;
        }

        private static int RedisSocialPort
        {
            get;
            set;
        }

        private static string RedisSocialPasswd
        {
            get;
            set;
        }

        private static int RedisSocialDb
        {
            get;
            set;
        }

        private static string RedisCopyHost
        {
            get;
            set;
        }

        private static int RedisCopyPort
        {
            get;
            set;
        }

        private static string RedisCopyPasswd
        {
            get;
            set;
        }

        private static int RedisCopyDb
        {
            get;
            set;
        }
    }
}
