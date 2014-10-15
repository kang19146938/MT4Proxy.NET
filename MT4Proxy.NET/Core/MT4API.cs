using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MT4CliWarpper;

namespace MT4Proxy.NET.Core
{
    internal class MT4API : MT4Wrapper
    {
        internal DateTime LastPushTime
        {
            get;
            set;
        }

        public MT4API(int aID)
            : base()
        {
            LastPushTime = DateTime.Now;
            ID = aID;
        }

        public int ID
        {
            get;
            private set;
        }
    }
}
