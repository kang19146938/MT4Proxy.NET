using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MT4Proxy.NET.Core
{
    /// <summary>
    /// 推送ZMQ任务协程基础实现
    /// </summary>
    abstract class TaskBase
    {
        protected volatile bool CanRun = true;
        protected void Sleep(int aMillisecond)
        {
            SpinWait.SpinUntil(() => false, aMillisecond);
        }

        protected void Sleep(Func<bool> aCond, int aMillisecond)
        {
            SpinWait.SpinUntil(aCond, aMillisecond);
        }

        protected abstract void OnProc();
    }
}
