using MT4CliWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MT4Proxy.NET.EventArg
{
    internal class TradeInfoEventArgs: EventArgs
    {
        public TradeInfoEventArgs(TRANS_TYPE aType, TradeRecordResult aTrade)
        {
            FromUsercode = null;
            ToUsercode = null;
            TradeType = aType;
            Trade = aTrade;
        }

        public TRANS_TYPE TradeType
        { get; private set; }

        public TradeRecordResult Trade
        { get; private set; }

        public string FromUsercode
        {
            get;
            set;
        }

        public string ToUsercode
        {
            get;
            set;
        }
    }
}
