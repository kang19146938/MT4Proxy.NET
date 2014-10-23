MT4Proxy.NET
============

运行在dotNET CLR环境的MT4托管代理服务，兼容redis和zmq。
项目.NET默认运行环境版本是4.5，最低编译环境版本不应低于4.0。

安装
====

若使用一般控制台启动方式，请设置MT4Proxy.NET.exe.config配置文件
若使用Windows服务启动方式，请设置MT4Proxy.Server.exe.config配置文件，再使用命令：

    InstallUtil.exe MT4Proxy.Server.exe

安装服务。InstallUtil.exe位于.NET安装目录（比如C:\Windows\Microsoft.NET\Framework64\v4.0.30319中）

启动
====

若使用一般控制台启动方式，运行MT4Proxy.NET.exe
若使用Windows服务启动方式，请在"服务"面板找到MT4Proxy.NET并启动
