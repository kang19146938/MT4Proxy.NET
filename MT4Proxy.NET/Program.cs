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
            Boot(args);
            logger.Info("启动完成，按任意键退出");
            Console.Read();
            Poll.uninit();
        }

        public static void Start(string[] args)
        {
            Logger logger = LogManager.GetLogger("common");
            logger.Info("检测到使用Windows服务模式启动");
            Boot(args);
        }

        private static void Boot(string[] args)
        {
            Environment.CurrentDirectory = System.AppDomain.CurrentDomain.BaseDirectory;

            bool help = false;
            DateTime? fromTime = null;
            TimeSpan? period = null;
            var p = new OptionSet() {
                { "sync|s=",  (int v) => fromTime = DateTime.Now.AddMinutes(-v)  },
                { "period|p=",  (int v) => period = TimeSpan.FromMinutes(v)  },
                { "h|?|help",   v => help = v != null },
            };
            try
            {
                List<string> extra = p.Parse(args);
                if (fromTime == null ^ period == null)
                    throw new Exception("sync时间或周期设置不正确");
                if(help)
                {
                    Console.WriteLine(
                        string.Format("无参数:执行MT4Proxy服务\nsync 时间 period 时间:从sync分钟前到前移period分钟内数据同步\nh|?|help:显示帮助信息"));
                    return;
                }
                if(fromTime != null && period != null)
                {
                    //do sync
                    return;
                }
                Logger logger = LogManager.GetLogger("common");
                MT4CliWrapper.MT4Wrapper.OnLog += (a) => { logger.Info(a); };
                if (Environment.Is64BitProcess)
                    logger.Warn("警告，在64位环境启动");
                else
                    logger.Info("启动环境是32位");
                logger.Info("准备启动MT4池");
                Poll.init();
                return;
                logger.Info("准备启动redis监听服务");
                RedisServer.Init();
                logger.Info("准备启动zmq监听服务");
                ZmqServer.Init();
                logger.Info("初始工作已完成");
            }
            catch (OptionException e)
            {
                Console.Write("参数格式不正确: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("请使用 --help 命令获取更多信息.");
                return;
            }
        }

        public static void Stop()
        {
            MT4Pump.EnableRestart = false;
            System.Threading.Thread.Sleep(10000);
        }
    }
}
