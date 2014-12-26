using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    class DockItem
    {
        public DockItem()
        {
            CreateTime = DateTime.MinValue;
            MaxRetryTimes = 5;
            RetryPeriodMS = 1000;
            RetryTimes = MaxRetryTimes;
        }

        public string Name
        {
            get;
            set;
        }

        public Action<object> Shutdown
        {
            get;
            set;
        }

        public Func<object> Create
        {
            get;
            set;
        }

        public DateTime CreateTime
        {
            get;
            set;
        }

        public int RetryTimes
        {
            get;
            set;
        }

        public object Connection
        {
            get;
            set;
        }

        public int MaxRetryTimes
        {
            get;
            set;
        }

        public int RetryPeriodMS
        {
            get;
            set;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
