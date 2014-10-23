using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;

namespace MT4Proxy.NET.Core
{
    public interface IService
    {
        void OnRequest(IServer aServer, dynamic aArgs);
    }
}
