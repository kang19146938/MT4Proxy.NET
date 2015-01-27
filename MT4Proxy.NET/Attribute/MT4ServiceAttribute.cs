using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    public class MT4ServiceAttribute : Attribute
    {
        public MT4ServiceAttribute()
        {
            ShowRequest = ShowResponse = true;
            EnableZMQ = true;
        }

        public bool DisableMT4
        {
            get;
            set;
        }
        public bool EnableZMQ
        {
            get;
            set;
        }

        public string ZmqApiName
        {
            get;
            set;
        }

        public bool ShowRequest
        {
            get;
            set;
        }

        public bool ShowResponse
        {
            set;
            get;
        }
    }
}
