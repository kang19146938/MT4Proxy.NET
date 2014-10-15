using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MT4CliWarpper;
using NetMQ;
using System.Threading;
using MT4Proxy.NET.Core;
using ServiceStack.Redis;

namespace MT4Proxy.NET
{
    class Program
    {
        static void Main(string[] args)
        {
            var clientsManager = new PooledRedisClientManager(new string[] { "tiger@www.tigerwit.com:6379" }, new string[] { }, new RedisClientManagerConfig() { DefaultDb = 1 });
            
            var redisPubSub = new RedisPubSubServer(clientsManager, "channel-1", "channel-2")
            {
                OnMessage = (channel, msg) => Console.WriteLine("Received '{0}' from '{1}'", msg, channel)
            }.Start();
            /*
            using (NetMQContext ctx = NetMQContext.Create())
            {
                using (var server = ctx.CreatePublisherSocket())
                {
                    server.Bind("tcp://127.0.0.1:5556");
                    using (var client = ctx.CreateRequestSocket())
                    {
                        client.Connect("tcp://127.0.0.1:5556");
                        var th = new Thread(() => {
                            var msg = client.ReceiveMessage();
                            foreach(var i in msg)
                            {
                                Console.WriteLine(i.ConvertToString());
                            }
                        });
                        var x = server.ReceiveMessage();
                        th.Start();
                        
                        while(true)
                        {
                            Thread.Sleep(1000);
                            foreach(var i in x)
                            {
                                var resp = new NetMQMessage();
                                resp.Append("hoho");
                                server.SendMessage(resp);
                            }
                            
                        }
                        using (var client2 = ctx.CreateRequestSocket())
                        {
                            
                            client.Send("Hello");

                            client2.Connect("tcp://127.0.0.1:5556");
                            client2.Send("Hello");

                            string m1 = server.ReceiveString();
                            Console.WriteLine("From Client: {0}", m1);
                            server.Send("Hi Back");
                            Thread.Sleep(500);
                            server.Send("Hi Back");
                            string m2 = client2.ReceiveString();
                            
                            Console.WriteLine("From Server: {0}", m2);
                            Console.ReadLine();
                        }
                    }
                }
            }

            MT4Wrapper mt4 = new MT4Wrapper();
            mt4.OnLog += (s) => Console.WriteLine(s);
            mt4.init();
            
            Console.WriteLine(mt4.ConnectDirect("202.65.221.52:443", 55800, "elex1234"));
            var xxx = new MarginLevelArgs();
            mt4.MarginLevelRequest(500003994, ref xxx);
            */
            Poll.init();
            MT4API a = new MT4API(1);
            Poll.Bringback(a);
            MT4API b = new MT4API(1);
            int y = 1;
            b.UserRecordsRequest(500003994, DateTime.Now.ToTime32(), (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds);
            Poll.Bringback(b);
            var x = Poll.Fetch(5);
            Console.WriteLine(",,,");
            Console.Read();
            Poll.uninit();
        }
    }
}
