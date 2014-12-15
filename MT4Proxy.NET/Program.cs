using System;
using MT4Proxy.NET.Core;
using NLog;
using NDesk.Options;
using System.Collections.Generic;

namespace MT4Proxy.NET
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Logger logger = LogManager.GetLogger("common");
            logger.Warn("警告现在使用一般启动方式，建议使用Windows服务模式启动");
            var result = Boot(args);
            if (result)
            {
                logger.Info("启动完成，按任意键退出");
                Console.Read();
            }
            Poll.uninit();
        }

        public static void Start(string[] args)
        {
            Logger logger = LogManager.GetLogger("common");
            logger.Info("检测到使用Windows服务模式启动");
            Boot(args);
        }

        private static bool Boot(string[] args)
        {
            Environment.CurrentDirectory = System.AppDomain.CurrentDomain.BaseDirectory;

            bool help = false;
            bool sync = false;
            var p = new OptionSet() {
                { "sync|s",  v => sync = v != null  },
                { "h|?|help",   v => help = v != null },
            };
            try
            {
                List<string> extra = p.Parse(args);
                if(help)
                {
                    Console.WriteLine(
                        string.Format("无参数:执行MT4Proxy服务\n" + 
                        "s|sync执行当天数据聚合\nh|?|help:显示帮助信息"));
                    return false;
                }
                Logger logger = LogManager.GetLogger("common");
                MT4CliWrapper.MT4Wrapper.OnLog += (a) => { logger.Info(a); };
                if (Environment.Is64BitProcess)
                    logger.Warn("警告，在64位环境启动");
                else
                    logger.Info("启动环境是32位");
                logger.Info("准备启动MT4池");
                Poll.init();
                if (sync)
                {
                    var syncer = new MysqlSyncer();
                    syncer.SyncEquity();
                    syncer.SyncMaster();
                    MT4Pump.EnableRestart = false;
                    System.Threading.Thread.Sleep(10000);
                    return false;
                }
                logger.Info("准备启动Redis监听服务");
                RedisServer.Init();
                logger.Info("准备启动Zmq监听服务");
                ZmqServer.Init();
                logger.Info("初始工作已完成");
                return true;
            }
            catch (OptionException e)
            {
                Console.Write("参数格式不正确: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("请使用 --help 命令获取更多信息.");
                return false;
            }
            catch(Exception e)
            {
                var logger = LogManager.GetLogger("common");
                logger.Error(e.StackTrace);
            }
            return false;
        }

        public static void Stop()
        {
            MT4Pump.EnableRestart = false;
            System.Threading.Thread.Sleep(10000);
            MT4API.uninit();
        }
    }
}
