MT4Proxy.NET
============

运行在dotNET CLR环境的MT4托管代理服务，使用ZMQ和Redis。
项目.NET默认运行环境版本是4.5，最低编译环境版本不应低于4.0。

安装
====

若使用一般控制台启动方式，请设置MT4Proxy.NET.exe.config配置文件；

若使用Windows服务启动方式，请设置MT4Proxy.Server.exe.config配置文件，再使用命令：

    InstallUtil.exe MT4Proxy.Server.exe

安装服务，InstallUtil.exe位于.NET安装目录（比如C:\Windows\Microsoft.NET\Framework64\v4.0.30319中）。

启动
====

若使用一般控制台启动方式，运行MT4Proxy.NET.exe；

若使用Windows服务启动方式，请在"服务"面板找到MT4Proxy.NET并启动。

再开发
======

一个关于入金的例子:

    [MT4Service(EnableZMQ = true)]
    class Pay : IService
    {
        public void OnRequest(IServer aServer, dynamic aJson)
        {
            var dict = aJson;
            var args = new TradeTransInfoArgs
            {
                type = TradeTransInfoTypes.TT_BR_BALANCE,
                cmd = Convert.ToInt16(dict.cmd),
                orderby = Convert.ToInt32(dict.mt4UserID),
                price = Convert.ToDouble(dict.price)
            };
            var result = aServer.MT4.TradeTransaction(args);
            dynamic resp = new ExpandoObject();
            resp.is_succ = result == 0;
            resp.errMsg = Utils.GetErrorMessage(result);
            resp.errCode = (int)result;
            aServer.Output = JsonConvert.SerializeObject(resp);
        }
    }
    
`MT4Service`特性可以设置service运行细节。`IServer`接口提供了返回内容和结果保存位置。
