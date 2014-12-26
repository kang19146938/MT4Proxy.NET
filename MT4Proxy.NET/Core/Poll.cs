using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using MT4CliWrapper;
using System.Threading;
using System.Diagnostics;
using NLog;
using NLog.Internal;
using System.Reflection;
using System.IO;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// MT4池系统，管理并发的MT4连接
    /// </summary>
    internal class Poll : ConfigBase
    {
        private static ConcurrentDictionary<int, MT4API> _poll = new ConcurrentDictionary<int, MT4API>();
        private static ConcurrentQueue<MT4API> _idel = new ConcurrentQueue<MT4API>();
        static int _taskid = 0;

        internal override void LoadConfig(ConfigurationManager aConfig)
        {
            MT4Host = aConfig.AppSettings["mt4_host"];
            MT4AdminID = int.Parse(aConfig.AppSettings["mt4_user"]);
            MT4Passwd = aConfig.AppSettings["mt4_passwd"];
            MT4Group = aConfig.AppSettings["mt4_group"];
            MT4API.init(MT4Host, MT4AdminID, MT4Passwd);
        }
        public static MT4Wrapper Fetch(int id)
        {
            MT4API fetch = null;
            _poll.TryRemove(id, out fetch);
            if (fetch == null || fetch.IsOutOfDate)
            {
                fetch = new MT4API(id);
            }
            else
            {
                fetch.LastPushTime = DateTime.Now;
            }
            return fetch;
        }
        public static void PubMessage(string aTopic, string aMessage)
        {
            ZmqServer.PubMessage(aTopic, aMessage);
        }

        public static MT4Wrapper New()
        {
            while(true)
            {
                MT4API fetch = null;
                do
                {
                    _idel.TryDequeue(out fetch);
                    if (fetch != null && !fetch.IsOutOfDate)
                        return fetch;
                } while (fetch != null);
                var i = Interlocked.Increment(ref _taskid);
                return new MT4API(i);
            }
        }

        /// <summary>
        /// 回收new产生的对象
        /// </summary>
        /// <param name="aWrapper"></param>
        public static void Release(MT4Wrapper aWrapper)
        {
            _idel.Enqueue(aWrapper as MT4API);
        }

        /// <summary>
        /// 回收Fetch产生的对象
        /// </summary>
        /// <param name="aWrapper"></param>
        public static void Bringback(MT4Wrapper aWrapper)
        {
            var dtwrapper = (MT4API)aWrapper;
            _poll.TryAdd(dtwrapper.ID, dtwrapper);
        }

        public static IEnumerable<int> Keys
        {
            get
            {
                return _poll.Keys.ToList();
            }
        }

        private static void DogProc()
        {
            try
            {
                var logger = LogManager.GetLogger("beat");
                while (true)
                {
                    logger.Trace(string.Format("临时MT4池闲置:{0},会话MT4池闲置:{1}", _idel.Count, Poll.Keys.Count()));
                    Process proc = Process.GetCurrentProcess();
                    MT4API fetch = null;
                    var templst = new List<MT4API>();
                    var count = _idel.Count;
                    do
                    {
                        _idel.TryDequeue(out fetch);
                        if (fetch != null && !fetch.IsOutOfDate)
                            templst.Add(fetch);
                        else if (fetch != null)
                            fetch.Dispose();
                        count--;
                    } while (count > 0 && fetch != null);
                    foreach(var i in templst)
                    {
                        _idel.Enqueue(i);
                    }
                    
                    templst.Clear();
                    foreach (var i in Poll.Keys)
                    {
                        fetch = null;
                        Poll._poll.TryRemove(i, out fetch);
                        if (fetch != null)
                            if (fetch.IsOutOfDate)
                                fetch.Dispose();
                            else
                                Poll._poll.TryAdd(i, fetch);
                    }
                    Thread.Sleep(1000);
                }
            }
            catch(Exception e)
            {
                Logger errlogger = LogManager.GetLogger("clr_error");
                errlogger.Error("Dog线程遇到问题", e);
                Thread.Sleep(1000);
            }
        }

        public static void StartPoll()
        {
            var logger = Utils.CommonLog;
            var configPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            logger.Info(string.Format("配置文件位置:{0}",
                configPath));
            Console.WriteLine(string.Format("Config file path:{0}",
                configPath));
            if(!File.Exists(configPath))
            {
                throw new Exception("配置文件不存在！");
            }
            ConfigObject();
            var thDog = new Thread(DogProc);
            thDog.IsBackground = true;
            thDog.Start();
        }

        private static void ConfigObject()
        {
            var config = new ConfigurationManager();
            Assembly.GetExecutingAssembly().GetTypes()
                .Where(i => i.GetCustomAttribute<ConfigAttribute>(true) != null)
                .Where(i => i.FullName != typeof(ConfigBase).FullName)
                .All(i =>
                {
                    var item = Activator.CreateInstance(i) as ConfigBase;
                    item.LoadConfig(config);
                    return true;
                });
        }

        public static void StopPoll()
        {
            ServerContainer.StopAll();
            MT4Wrapper.uninit();
        }

        public static string MT4Host
        { get; private set; }

        public static int MT4AdminID
        { get; private set; }

        public static string MT4Passwd
        { get; private set; }

        public static string MT4Group
        { get; private set; }
    }
}
