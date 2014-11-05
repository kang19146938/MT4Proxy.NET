using System;
using MT4Proxy.NET.Core;
using NLog;

namespace MT4Proxy.NET
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Logger logger = LogManager.GetLogger("common");
            logger.Warn("警告现在使用一般启动方式，建议使用Windows服务模式启动");
            Boot();
            logger.Info("启动完成，按任意键退出");
            Console.Read();
            Poll.uninit();
        }

        public static void Start(string[] args)
        {
            Logger logger = LogManager.GetLogger("common");
            logger.Info("检测到使用Windows服务模式启动");
            Boot();
        }

        private static void Boot()
        {
            Logger logger = LogManager.GetLogger("common");
            MT4CliWrapper.MT4Wrapper.OnLog += (a) => { logger.Info(a); };
            if (Environment.Is64BitProcess)
                logger.Warn("警告，在64位环境启动");
            else
                logger.Info("启动环境是32位");
            logger.Info("准备启动MT4池");
            Poll.init();
            MT4Pump.StartPump();
            return;
            logger.Info("准备启动redis监听服务");
            RedisServer.Init();
            logger.Info("准备启动zmq监听服务");
            ZmqServer.Init();
            logger.Info("初始工作已完成");
        }

        public static void Stop()
        {

        }
    }
}
