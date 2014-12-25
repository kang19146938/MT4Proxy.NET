using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;
using NLog.Internal;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// ZMQ服务基础接口
    /// </summary>
    public interface IService
    {
        void OnRequest(IInputOutput aServer, dynamic aArgs);
    }
}
