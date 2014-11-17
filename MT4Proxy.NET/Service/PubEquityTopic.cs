using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using System.Collections.Concurrent;
using MT4CliWrapper;
using System.Text.RegularExpressions;
using Castle.Zmq;
using MT4Proxy.NET.Core;
using System.Threading;
using System.Dynamic;
using Newtonsoft.Json;


namespace MT4Proxy.NET.Service
{
    [MT4Service(EnableZMQ = true)]
    class PubEquityTopic : TaskBase, IService
    {
        private static ConcurrentDictionary<int, PubEquityTopic> _existsTask =
            new ConcurrentDictionary<int, PubEquityTopic>();

        private int MT4ID
        {
            get;
            set;
        }

        private string Topic
        {
            get;
            set;
        }

        public void OnRequest(IServer aServer, dynamic aJson)
        {
            int mt4_id = aJson["mt4UserID"];
            int pub_topic = aJson["topic"];
            bool turn_to = aJson["turn_to"];
            MT4ID = mt4_id;
            Topic = string.Format("Equity:{0}", pub_topic);
            if (turn_to)
            {
                if (!_existsTask.ContainsKey(pub_topic))
                {
                    var task = Task.Factory.StartNew(OnProc);
                    _existsTask.TryAdd(pub_topic, this);
                }
            }
            else
            {
                PubEquityTopic runing = null;
                _existsTask.TryRemove(pub_topic, out runing);
                if(runing != null)
                {
                    runing.CanRun = false;
                }
            }
            dynamic resp = new ExpandoObject();
            resp.is_succ = true;
            resp.errMsg = Utils.GetErrorMessage(0);
            resp.errCode = 0;
            aServer.Output = JsonConvert.SerializeObject(resp);
        }

        protected override void OnProc()
        {
            while (MT4Pump.EnableRestart && CanRun)
            {
                var api = Poll.Fetch(MT4ID);
                double equity = 0;
                var result = api.GetEquity(MT4ID, ref equity);
                Poll.Bringback(api);
                dynamic resp = new ExpandoObject();
                resp.is_succ = result == 0;
                resp.errMsg = Utils.GetErrorMessage(result);
                resp.errCode = (int)result;
                resp.equity = equity;
                Poll.PubMessage(Topic, JsonConvert.SerializeObject(resp));
                Sleep(2000);
            }
        }
    }
}
