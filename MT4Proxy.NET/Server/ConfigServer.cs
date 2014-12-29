using NLog.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    class ConfigServer : IServer
    {
        public void Initialize()
        {
            var logger = Utils.CommonLog;
            logger.Info("配置服务已启动");
            var configPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            logger.Info(string.Format("配置文件位置:{0}",
                configPath));
            Console.WriteLine(string.Format("Config file path:{0}",
                configPath));
            if (!File.Exists(configPath))
                throw new Exception("配置文件不存在！");
            ConfigObject();
            var watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(configPath);
            watcher.Filter = Path.GetFileName(configPath);  
            watcher.EnableRaisingEvents = true;//开启提交事件  
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += OnChanged;
        }

        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            var logger = Utils.CommonLog;
            logger.Info("重新读取配置信息");
            ConfigObject();
        }

        public void Stop()
        {
            ServerContainer.FinishStop();
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
    }
}
