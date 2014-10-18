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
            MT4Proxy.NET.Program.Main(args);
        }

        protected override void OnStop()
        {
            MT4Proxy.NET.Program.Stop();
        }
    }
}
