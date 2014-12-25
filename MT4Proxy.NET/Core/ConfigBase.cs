using NLog.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 对于需要从配置文件中读取信息的类，可以从继承该抽象类
    /// 之所以使用特性加抽象类的方式实现这个功能，首先是考虑到对子类
    /// 表达有一定约束，但是接口不能继承特性，所以选择抽象类做这种事情
    /// 用特性是因为，筛出需要配置的类得靠它
    /// </summary>
    [Config]
    public abstract class ConfigBase
    {
        internal virtual void LoadConfig(ConfigurationManager aConfig)
        { }
    }
}
