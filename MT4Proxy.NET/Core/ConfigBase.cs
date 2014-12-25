using NLog.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    [Config]
    public abstract class ConfigBase
    {
        internal virtual void LoadConfig(ConfigurationManager aConfig)
        { }
    }
}
