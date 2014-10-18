using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using MT4Proxy.NET.Core;
using System.Diagnostics;
using MT4CliWrapper;

namespace MT4Proxy.NET.Test
{
    class monkey
    {
        public static volatile int des_times = 0;
        public static volatile int create_times = 0;
        public static void test()
        {
            MT4Proxy.NET.Core.Poll.init();
            var rnd = new Random();
            var newlst = new List<MT4Wrapper>();
            while(true)
            {
                Thread.Sleep(4000);
                var reqs = rnd.Next(24);
                for(int i =0;i<reqs;i++)
                {
                    var j = rnd.Next(2);
                    MT4CliWrapper.MT4Wrapper fetch = null;
                    if(j==1)
                    {
                        var k = rnd.Next(80);
                        fetch = MT4Proxy.NET.Core.Poll.Fetch(k);
                    }
                    else
                    {
                        fetch = MT4Proxy.NET.Core.Poll.New();
                        newlst.Add(fetch);
                    }
                    fetch.UserRecordsRequest(500003994, new DateTime(2014, 1, 1).ToTime32(), DateTime.Now.ToTime32());
                    if(j==1)
                    {
                        MT4Proxy.NET.Core.Poll.Bringback(fetch);
                    }
                }
                foreach (var i in newlst)
                    Poll.Release(i);
                newlst.Clear();
            }
        }
    }
}
