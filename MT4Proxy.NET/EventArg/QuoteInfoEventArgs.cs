using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MT4Proxy.NET.Core;

namespace MT4Proxy.NET.EventArg
{
    class QuoteInfoEventArgs: EventArgs
    {
        public QuoteInfoEventArgs(string aSymbol, double aAsk, double aBid,
            int aTimestmp)
        {
            Symbol = aSymbol;
            Ask = aAsk;
            Bid = aBid;
            Timestamp = aTimestmp.FromTime32();
        }

        public string Symbol
        {
            get;
            private set;
        }

        public DateTime Timestamp
        {
            get;
            private set;
        }

        public double Ask
        {
            get;
            private set;
        }

        public double Bid
        {
            get;
            private set;
        }
    }
}
