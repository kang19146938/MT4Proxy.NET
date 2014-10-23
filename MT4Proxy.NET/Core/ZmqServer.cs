using NLog.Internal;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using MT4CliWrapper;
using NLog;
using Newtonsoft.Json;
using Castle.Zmq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MT4Proxy.NET.Core
{
    class ZmqServer : IServer, IDisposable
    {
        private static Context _zmqCtx = null;
        private static ConcurrentDictionary<string, Type> _apiDict = new ConcurrentDictionary<string, Type>();

        public ZmqServer()
        {
            MT4 = Poll.New();
        }
        public static void Init()
        {
            Logger initlogger = LogManager.GetLogger("common");
            _zmqCtx = new Context();
            var config = new ConfigurationManager();
            var zmqBind = config.AppSettings["zmq_bind"];
            foreach (var i in Utils.GetTypesWithServiceAttribute())
            {
                var attr = i.GetCustomAttribute(typeof(MT4ServiceAttribute)) as MT4ServiceAttribute;
                var service = i;
                if (attr.EnableZMQ)
                {
                    if (!string.IsNullOrWhiteSpace(attr.ZmqApiName))
                    {
                        initlogger.Info(string.Format("准备初始化ZMQ服务:{0}", attr.ZmqApiName));
                        _apiDict[attr.ZmqApiName] = i;
                    }
                    else
                    {
                        initlogger.Info(string.Format("准备初始化ZMQ服务:{0}", service.Name));
                        _apiDict[service.Name] = i;
                    }
                }
            }
            var repSocket = _zmqCtx.CreateSocket(SocketType.Rep);
            repSocket.Bind(zmqBind);
            var polling = new Polling(PollingEvents.RecvReady, repSocket);
            polling.RecvReady += (socket) =>
            {
                try
                {
                    var item = socket.RecvString(Encoding.UTF8);
                    dynamic json = JObject.Parse(item);
                    string api_name = json.__api;
                    if(_apiDict.ContainsKey(api_name))
                    {
                        var service = _apiDict[api_name];
                        var serviceobj = Activator.CreateInstance(service) as IService;
                        using (var server = new ZmqServer())
                        {
                            server.Logger = LogManager.GetLogger("common");
                            server.Logger.Info(string.Format("ZMQ,recv request:{0}", item));
                            serviceobj.OnRequest(server, json);
                            if (server.Output != null)
                            {
                                server.Logger.Info(string.Format("ZMQ,response:{0}", server.Output));
                                socket.Send(server.Output);
                            }
                            else
                            {
                                server.Logger.Warn(string.Format("ZMQ,response empty,source:{0}", item));
                                socket.Send(string.Empty);
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    Logger logger = LogManager.GetLogger("clr_error");
                    logger.Error("处理单个ZMQ请求失败", e);
                    socket.Send(string.Empty);
                }
                finally
                {
                    Task.Factory.StartNew(() => { polling.PollForever(); });
                }
            };
            Task.Factory.StartNew(() => { polling.PollForever(); });
        }

        public void Pub(string aChannel, string aJson)
        {

        }

        public string Output
        {
            get;
            set;
        }

        public MT4Wrapper MT4
        {
            get;
            private set;
        }
        public Logger Logger
        {
            get;
            private set;
        }
        public string RedisOutputList
        {
            get;
            set;
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
