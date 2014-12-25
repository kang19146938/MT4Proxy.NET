using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 服务对象接口，用于ServerContainer控制
    /// </summary>
    interface IServer
    {
        void Initialize();

        void Stop();
    }
}
