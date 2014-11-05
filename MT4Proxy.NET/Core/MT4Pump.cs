using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using NLog;

namespace MT4Proxy.NET.Core
{
    class MT4Pump
    {
        public MT4Pump()
        {

        }

        private MT4API MT4
        {
            get;
            set;
        }

        private Timer Timer
        {
            get;
            set;
        }

        public static volatile bool EnableRestart
        {
            get;
            set;
        }

        public static void StartPump()
        {
            EnableRestart = true;
            for (int i = 0; i < MT4API.PumperCount; i++)
            {
                var timer = new Timer(10000);
                timer.Interval = 10000;
                var pump = new MT4Pump() { Timer = timer };
                timer.Elapsed += (sender, e) =>
                {
                    pump.RestartPump(pump);
                };
                timer.Start();
                pump.RestartPump(pump);
            }
        }

        private void RestartPump(MT4Pump aPump)
        {
            aPump.Timer.Stop();
            if (!EnableRestart)
                return;
            int retryTimes = 3;
            var mt4 = new MT4API(true);
            while(retryTimes-- > 0)
            {
                if (!mt4.IsPumpAlive())
                {
                    Logger logger = LogManager.GetLogger("common");
                    logger.Warn(
                        string.Format("MT4推送接收连接建立失败，一秒之后重试，剩余机会{0}", 
                        retryTimes + 1));
                    System.Threading.Thread.Sleep(1000);
                    mt4.ConnectPump();
                    continue;
                }
                break;
            }
            if(retryTimes == -1)
            {
                Logger logger = LogManager.GetLogger("common");
                logger.Error("MT4推送接收连接建立失败，请立即采取措施保障丢失的数据！");
            }
            else
            {
                Console.WriteLine("ping");
                if (aPump.MT4 != null)
                    aPump.MT4.Dispose();
                aPump.MT4 = mt4;
                aPump.Timer.Start();
            }
        }
    }
}