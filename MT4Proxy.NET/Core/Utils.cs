using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    public static class Utils
    {
        public static int ToTime32(this DateTime aDatetime)
        {
            return (int)(aDatetime - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
