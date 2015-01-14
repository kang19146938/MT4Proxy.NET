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
using System.Threading;
using System.Web.Script.Serialization;
using System.Diagnostics;

namespace MT4Proxy.NET.Core
{
    class ZmqServer : IInputOutput, IDisposable, IServer
    {
        public void Initialize()
        {
            EnableRunning = true;
            Init();
        }

        public void Stop()
        {
            EnableRunning = false;
            ServerContainer.FinishStop();
        }

        private static bool EnableRunning = false;
        private Context _zmqCtx = null;
        private ConcurrentDictionary<string, Type> _apiDict = 
            new ConcurrentDictionary<string, Type>();
        private ConcurrentDictionary<string, MT4ServiceAttribute> _attrDict =
            new ConcurrentDictionary<string, MT4ServiceAttribute>();
        private static IZmqSocket _publisher = null;
        private static Semaphore _pubSignal = new Semaphore(0, 20000);
        private static ConcurrentQueue<Tuple<string, string>>
            _queMessages = new ConcurrentQueue<Tuple<string, string>>();

        private int _mt4ID = 0;

        public ZmqServer()
        {

        }

        public ZmqServer(int aMT4ID, bool aEnableMT4Object = true)
        {
            if (!aEnableMT4Object) return;
            if (aMT4ID > 0)
                MT4 = Poll.Fetch(aMT4ID);
            else
                MT4 = Poll.New();
            _mt4ID = aMT4ID;
        }

        internal static void PubMessage(string aTopic, string aMessage)
        {
            var socket = _publisher;
            if(socket != null)
            {
                _queMessages.Enqueue(new Tuple<string, string>(aTopic, aMessage));
                _pubSignal.Release();
            }
        }

        private static void PubProc(object aArg)
        {
            while(EnableRunning)
            {
                _pubSignal.WaitOne();
                Tuple<string, string> item = null;
                _queMessages.TryDequeue(out item);
                if (item == null) continue;
                var topic = item.Item1;
                var message = item.Item2;
                var socket = _publisher;
                if (socket != null)
                {
                    socket.Send(topic, null, hasMoreToSend: true);
                    socket.Send(message);
                }
            }
        }

        public void Init()
        {
            var initlogger = LogManager.GetLogger("common");
            var config = new ConfigurationManager();
            if(!bool.Parse(config.AppSettings["enable_zmq"]))
            {
                initlogger.Info("ZMQ服务被禁用，跳过ZMQ初始化");
                return;
            }
            _zmqCtx = new Context();
            var zmqBind = config.AppSettings["zmq_bind"];
            foreach (var i in Utils.GetTypesWithServiceAttribute())
            {
                var attr = i.GetCustomAttribute(typeof(MT4ServiceAttribute)) as MT4ServiceAttribute;
                var service = i;
                if (attr.EnableZMQ)
                {
                    var serviceName = string.Empty;
                    if (!string.IsNullOrWhiteSpace(attr.ZmqApiName))
                        serviceName = attr.ZmqApiName;
                    else
                        serviceName = service.Name;
                    initlogger.Info(string.Format("准备初始化ZMQ服务:{0}", serviceName));
                    _apiDict[serviceName] = i;
                    _attrDict[serviceName] = attr;
                }
            }
            var repSocket = _zmqCtx.CreateSocket(SocketType.Rep);
            var pubSocket = _zmqCtx.CreateSocket(SocketType.Pub);
            repSocket.Bind(zmqBind);
            pubSocket.Bind(config.AppSettings["zmq_pub_bind"]);
            _publisher = pubSocket;
            var th_pub = new Thread(PubProc);
            th_pub.IsBackground = true;
            th_pub.Start();
            var jss = new JavaScriptSerializer();
            var polling = new Polling(PollingEvents.RecvReady, repSocket);
            var watch = new Stopwatch();
            polling.RecvReady += (socket) =>
            {
                try
                {
                    var item = socket.RecvString(Encoding.UTF8);
                    var dict = jss.Deserialize<dynamic>(item);
                    string api_name = dict["__api"];
                    var mt4_id = 0;
                    if (dict.ContainsKey("mt4UserID"))
                        mt4_id = Convert.ToInt32(dict["mt4UserID"]);
                    if(_apiDict.ContainsKey(api_name))
                    {
                        var service = _apiDict[api_name];
                        var serviceobj = Activator.CreateInstance(service) as IService;
                        using (var server = new ZmqServer(mt4_id, !_attrDict[api_name].DisableMT4))
                        {
                            server.Logger = LogManager.GetLogger("common");
                            if(_attrDict[api_name].ShowRequest)
                                server.Logger.Info(string.Format("ZMQ,recv request:{0}", item));
                            watch.Restart();
                            serviceobj.OnRequest(server, dict);
                            if (server.Output != null)
                            {
                                socket.Send(server.Output);
                                watch.Stop();
                                var elsp = watch.ElapsedMilliseconds;
                                if (_attrDict[api_name].ShowResponse)
                                    server.Logger.Info(string.Format("ZMQ[{0}ms] response:{1}",
                                        elsp, server.Output));
                            }
                            else
                            {
                                socket.Send(string.Empty);
                                watch.Stop();
                                var elsp = watch.ElapsedMilliseconds;
                                if (_attrDict[api_name].ShowResponse)
                                    server.Logger.Warn(string.Format("ZMQ[{0}ms] response empty,source:{1}", 
                                        elsp, item));                            
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    var logger = LogManager.GetLogger("clr_error");
                    logger.Error("处理单个ZMQ请求失败,{0}", e.StackTrace);
                    socket.Send(string.Empty);
                }
                finally
                {
                    if (EnableRunning)
                        Task.Factory.StartNew(() => { polling.PollForever(); });
                }
            };
            if (EnableRunning)
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
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (MT4 != null)
                    if (_mt4ID > 0)
                        Poll.Bringback(MT4);
                    else
                        Poll.Release(MT4);
                MT4 = null;
                Logger = null;
            }
            disposed = true;
        }
    }
}
