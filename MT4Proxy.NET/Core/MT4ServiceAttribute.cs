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
    }
}
