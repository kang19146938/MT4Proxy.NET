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
            var logger = LogManager.GetLogger("common");
            logger.Warn("警告现在使用一般启动方式，建议使用Windows服务模式启动");
            var result = Boot(args);
            if (result)
            {
                logger.Info("启动完成，按任意键退出");
                Console.Read();
            }
            logger.Info("终止系统");
            ServerContainer.StopAll();
        }

        public static void Start(string[] args)
        {
            var logger = LogManager.GetLogger("common");
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
                var logger = LogManager.GetLogger("common");
                MT4CliWrapper.MT4Wrapper.OnLog += (a) => { logger.Info(a); };
                if (Environment.Is64BitProcess)
                    logger.Warn("警告，在64位环境启动");
                else
                    logger.Info("启动环境是32位");
                logger.Info("准备启动MT4池");
                Poll.StartPoll();
                if (sync)
                {
                    var syncer = new MysqlServer();
                    syncer.SyncEquity();
                    syncer.SyncMaster();
                    ServerContainer.StopAll();
                    return false;
                }
                logger.Info("准备启动Zmq监听服务");
                ServerContainer.ForkServer<ZmqServer>();
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
                logger.Error(string.Format("SyncError:{0}\n{1}", e.Message, e.StackTrace));
            }
            return false;
        }

        public static void Stop()
        {
            var logger = LogManager.GetLogger("common");
            logger.Info("收到windows服务停止信号");
            ServerContainer.StopAll();
        }
    }
}
