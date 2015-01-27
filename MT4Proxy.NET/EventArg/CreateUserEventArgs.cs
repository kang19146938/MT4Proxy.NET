using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.EventArg
{
    class CreateUserEventArgs : EventArgs
    {
        public int MT4ID
        { get; set; }

        public int Usercode
        {
            get;
            set;
        }

        public bool IsLiveAccount
        { get; set; }
    }
}
