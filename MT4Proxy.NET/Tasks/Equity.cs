using MT4Proxy.NET.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Tasks
{
    class Equity: Core.TaskBase
    {
        private int MT4ID
        {
            get;
            set;
        }

        public Equity(int aMT4ID)
        {
            MT4ID = aMT4ID;
        }
        protected override void OnProc()
        {
            while (MT4Pump.EnableRestart)
            {
                var api = Poll.Fetch(MT4ID);
                double equity = 0;
                api.GetEquity(MT4ID, ref equity);
                Sleep(() => MT4Pump.EnableRestart, 5000);
            }
        }
    }
}
