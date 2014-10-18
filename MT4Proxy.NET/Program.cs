using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MT4CliWrapper;
using System.Threading;
using MT4Proxy.NET.Core;
using NLog.Internal;
using NLog;


namespace MT4Proxy.NET
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Logger logger = LogManager.GetLogger("common");
            MT4CliWrapper.MT4Wrapper.OnLog += (a) => { logger.Info(a); };
            if (Environment.Is64BitProcess)
                logger.Warn("警告，在64位环境启动");
            else
                logger.Info("启动环境是32位");
            logger.Info("准备启动MT4池");
            Poll.init();
            logger.Info("准备启动redis监听服务");
            RedisServer.Init();
            
            logger.Info("启动完成，按任意键退出");
            Console.Read();
            Poll.uninit();
        }

        public static void Stop()
        {

        }
    }
}
