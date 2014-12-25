using MT4CliWrapper;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    public interface IInputOutput
    {
        string Output
        {
            get;
            set;
        }

        MT4Wrapper MT4
        {
            get;
        }

        string RedisOutputList
        {
            get;
            set;
        }

        Logger Logger
        {
            get;
        }
    }
}
