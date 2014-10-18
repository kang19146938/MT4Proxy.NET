using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MT4CliWrapper;

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
            Test.monkey.create_times++;
        }

        public int ID
        {
            get;
            private set;
        }

        public bool IsOutOfDate
        {
            get
            {
                return (DateTime.Now - LastPushTime) > new TimeSpan(0, 0, 10);
            }
        }

        ~MT4API()
        {
            Test.monkey.des_times++;
        }
    }
}
