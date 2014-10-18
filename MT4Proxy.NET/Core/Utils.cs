using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    public static class Utils
    {
        private static DateTime origin = new DateTime(1970, 1, 1);
        public static int ToTime32(this DateTime aDatetime)
        {
            return (int)(aDatetime - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static DateTime FromTime32(this int aTime32)
        {
            return origin.AddSeconds(aTime32);
        }
    }
}
