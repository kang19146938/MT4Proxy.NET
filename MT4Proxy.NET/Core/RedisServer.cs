using NLog.Internal;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using CSRedis;
using MT4CliWrapper;
using NLog;
using Newtonsoft.Json;

namespace MT4Proxy.NET.Core
{
    public class RedisServer : IServer, IDisposable
    {
        private static ConnectionMultiplexer _redis = null;
        private static ConfigurationManager _config = new ConfigurationManager();
        public RedisServer()
        {
            MT4 = Poll.New();
        }

        public MT4Wrapper MT4
        {
            get;
            private set;
        }
        public static IDatabase FetchRedisConn
        {
            get
            {
                return _redis.GetDatabase(0);
            }
        }

        public static RedisClient FetchRedisConnWithBlock
        {
            get
            {
                var client = new RedisClient(_config.AppSettings["redis_host"], int.Parse(_config.AppSettings["redis_port"]));
                client.Auth(_config.AppSettings["redis_password"]);
                return client;
            }
        }

        public string Output
        {
            get;
            set;
        }

        public string RedisOutputList
        {
            get;
            set;
        }

        public Logger Logger
        {
            get;
            private set;
        }

        public static void Init()
        {
            Logger initlogger = LogManager.GetLogger("common");
            ConfigurationOptions redisconfig = new ConfigurationOptions
            {
                EndPoints =
            {
                { _config.AppSettings["redis_host"], int.Parse(_config.AppSettings["redis_port"]) },
            },
                KeepAlive = 180,
                Password = _config.AppSettings["redis_password"]
            };
            _redis = ConnectionMultiplexer.Connect(redisconfig);
            foreach (var i in Utils.GetTypesWithServiceAttribute())
            {
                var attr = i.GetCustomAttribute(typeof(MT4ServiceAttribute)) as MT4ServiceAttribute;
                var service = i;
                if (!attr.EnableRedis)
                    continue;
                initlogger.Info(string.Format("准备启动redis监听服务:{0}", i.Name));
                new Thread(() =>
                {
                    try
                    {
                        using (var client = new RedisClient(_config.AppSettings["redis_host"], int.Parse(_config.AppSettings["redis_port"])))
                        {
                            client.Auth(_config.AppSettings["redis_password"]);
                            while (true)
                            {
                                var item = client.BRPop(1000, new string[] { attr.RedisKey });
                                if (string.IsNullOrEmpty(item))
                                    continue;
                                Task.Factory.StartNew(new Action(async () =>
                                {
                                    try
                                    {
                                        using (var server = new RedisServer())
                                        {
                                            server.RedisOutputList = attr.RedisOutputList;
                                            server.Logger = LogManager.GetLogger("common");
                                            var serviceobj = Activator.CreateInstance(service) as IService;
                                            server.Logger.Info(string.Format("Redis,recv request:{0}", item));
                                            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(item);
                                            serviceobj.OnRequest(server, dict);
                                            if (!string.IsNullOrEmpty(server.Output) && !string.IsNullOrEmpty(server.RedisOutputList))
                                            {
                                                server.Logger.Info(string.Format("Redis,response:{0}", server.Output));
                                                await FetchRedisConn.ListLeftPushAsync(server.RedisOutputList, new RedisValue[] { server.Output });
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Logger logger = LogManager.GetLogger("clr_error");
                                        logger.Error("处理单个redis请求失败", e);
                                    }
                                }));
                            }
                        }
                        
                    }
                    catch (Exception e)
                    {
                        Logger logger = LogManager.GetLogger("clr_error");
                        logger.Error("redis监听失败", e);
                        Thread.Sleep(1000);
                    }
                }).Start();
            }
        }

        bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                Poll.Release(MT4);
                MT4 = null;
                Logger = null;
            }
            disposed = true;
        }
    }
}
