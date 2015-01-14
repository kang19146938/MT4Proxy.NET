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
        FileSystemWatcher _fileWather = null;
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
            ConfigObjects();
            _fileWather = new FileSystemWatcher();
            _fileWather.Path = Path.GetDirectoryName(configPath);
            _fileWather.Filter = Path.GetFileName(configPath);
            _fileWather.EnableRaisingEvents = true;
            _fileWather.NotifyFilter = NotifyFilters.LastWrite;
            _fileWather.Changed += OnChanged;
        }

        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            var logger = Utils.CommonLog;
            logger.Info("重新读取配置信息");
            System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            ConfigObjects();
        }

        public void Stop()
        {
            if(_fileWather != null)
            {
                _fileWather.Changed -= OnChanged;
                _fileWather.Dispose();
                _fileWather = null;
            }
            ServerContainer.FinishStop();
        }

        private static void ConfigObjects()
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
