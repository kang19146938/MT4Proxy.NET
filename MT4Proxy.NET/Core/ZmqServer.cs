using MT4CliWrapper;
using NLog;
using NLog.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZMQ;

namespace MT4Proxy.NET.Core
{
    class ZmqServer:IServer
    {
        private static Context zmqCtx = new Context(8);
        public static void Init()
        {
            var config = new ConfigurationManager();
            var zmqBind = config.AppSettings["zmq_bind"];
            using (var repSocket = zmqCtx.Socket(SocketType.REP))
            {
                repSocket.Bind(zmqBind);
                while (true)
                {
                    
                    var msg = repSocket.Recv();
                    var msgStr = Encoding.UTF8.GetString(msg);
                    new ZmqServer { ZmqSocket = repSocket }.Work(msgStr);
                }
            }
        }

        public void Work(string aJson)
        {

        }

        public Socket ZmqSocket
        {
            get;
            private set;
        }
        public void Response(string aJson)
        {
            ZmqSocket.Send(aJson, Encoding.UTF8);
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
    }
}
