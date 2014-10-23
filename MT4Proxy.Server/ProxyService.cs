using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using MT4Proxy.NET;

namespace MT4Proxy.Server
{
    public partial class ProxyService : ServiceBase
    {
        public ProxyService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                Environment.CurrentDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                MT4Proxy.NET.Program.Start(args);
            }
            catch(Exception e)
            {
                EventLog evt = new EventLog("MT4Proxy.NET");
                evt.Source = "MT4Proxy.NET";
                evt.WriteEntry(e.StackTrace, EventLogEntryType.Error);
            }
        }

        protected override void OnStop()
        {
            MT4Proxy.NET.Program.Stop();
        }
    }
}
