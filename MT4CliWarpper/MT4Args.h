#include "Stdafx.h"
#include "MT4ManagerAPI.h"
using namespace System;
using namespace System::Runtime::InteropServices;

[StructLayoutAttribute(LayoutKind::Sequential)]
public value struct TradeTransInfoArgs
{
	TradeTransInfo ToNative()
	{
		TradeTransInfo ret = TradeTransInfo();
		ret.type = type;
		ret.reserved = reserved;
		ret.cmd = cmd;
		ret.order = order;
		ret.orderby = orderby;
		String^ symbol = this->symbol;
		memcpy(ret.symbol, marshal_as<std::string , System::String^>(symbol).c_str(), sizeof(ret.symbol));
		ret.volume = volume;
		ret.price = price;
		ret.sl = sl;
		ret.tp = tp;
		ret.ie_deviation = ie_deviation;
		String^ comment = this->comment;
		memcpy(ret.comment, marshal_as<std::string, System::String^>(comment).c_str(), sizeof(ret.comment));
		ret.expiration = expiration;
		ret.crc = crc;
		return ret;
	}
	
	UCHAR             type;             // trade transaction type
	char              reserved;         // reserved
	short             cmd;              // trade command
	int               order, orderby;    // order, order by
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 12)]
	String^           symbol;       // trade symbol
	int               volume;           // trade volume
	double            price;            // trade price
	double            sl, tp;            // stoploss, takeprofit
	int               ie_deviation;     // deviation on IE
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 32)]
	String^              comment;      // comment
	__time32_t        expiration;       // pending order expiration time
	int               crc;              // crc
};

[StructLayoutAttribute(LayoutKind::Sequential)]
public value struct MarginLevelArgs
{
	MarginLevel ToNative()
	{
		MarginLevel ret = MarginLevel();
		ret.login = login;
		String^ group = this->group;
		memcpy(ret.group, marshal_as<std::string, System::String^>(group).c_str(), sizeof(ret.group));
		ret.leverage = leverage;
		ret.updated = updated;
		ret.balance = balance;
		ret.equity = equity;
		ret.volume = volume;
		ret.margin = margin;
		ret.margin_free = margin_free;
		ret.margin_level = margin_level;
		ret.margin_type = margin_type;
		ret.level_type = level_type;
		return ret;
	}
	void FromNative(MarginLevel &aStruct)
	{
		login = aStruct.login;
		group = marshal_as<String^, char*>(aStruct.group);
		leverage = aStruct.leverage;
		updated = aStruct.updated;
		balance = aStruct.balance;
		equity = aStruct.equity;
	}
	int               login;            // user login
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 16)]
	String^           group;        // user group
	int               leverage;         // user leverage
	int               updated;          // (internal)
	double            balance;          // balance+credit
	double            equity;           // equity
	int               volume;           // lots
	double            margin;           // margin requirements
	double            margin_free;      // free margin
	double            margin_level;     // margin level
	int               margin_type;      // margin controlling type (percent/currency)
	int               level_type;       // level type(ok/margincall/stopout)
};

public value struct TradeRecordResult
{
	int               order;            // order ticket
	int               login;            // owner's login
	String^           symbol;      // security
	int               digits;           // security precision
	int               cmd;              // trade command
	int               volume;           // volume
};