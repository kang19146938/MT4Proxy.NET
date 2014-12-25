using MT4CliWrapper;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 给ZMQ请求提供信息和结果存储的接口
    /// </summary>
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

        Logger Logger
        {
            get;
        }
    }
}
