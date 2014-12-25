using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    public class MT4ServiceAttribute : Attribute
    {
        public string RedisKey
        {
            get;
            set;
        }

        public string RedisOutputList
        {
            get;
            set;
        }

        public bool EnableRedis
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
    }
}
