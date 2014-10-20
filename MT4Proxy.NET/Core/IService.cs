using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    public interface IService
    {
        void OnRequest(IServer aServer, Dictionary<string, string> aArgs);
    }
}
