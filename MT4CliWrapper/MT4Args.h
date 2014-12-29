#include "Stdafx.h"
#include "MT4ManagerAPI.h"
using namespace System;
using namespace System::Runtime::InteropServices;

#define TO_CHARS(SOURCE,TARGET) if(SOURCE) {String^ SOURCE = this->SOURCE;memcpy(TARGET.SOURCE, marshal_as<std::string, String^>(SOURCE).c_str(), sizeof(TARGET.SOURCE));}
#define FIELD_COPY(SOURCE,TARGET) TARGET = SOURCE->TARGET;

[StructLayoutAttribute(LayoutKind::Sequential)]
public value struct MarginLevelArgs
{
	MarginLevel ToNative()
	{
		MarginLevel ret = MarginLevel();
		ret.login = login;
		TO_CHARS(group, ret);
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

[StructLayoutAttribute(LayoutKind::Sequential)]
public value struct TradeRecordResult
{
	void FromNative(TradeRecord* aRecord)
	{
		FIELD_COPY(aRecord, order);
		FIELD_COPY(aRecord, login);
		symbol = marshal_as<String^, char*>(aRecord->symbol);
		FIELD_COPY(aRecord, digits);
		FIELD_COPY(aRecord, cmd);
		FIELD_COPY(aRecord, volume);
		FIELD_COPY(aRecord, open_time);
		FIELD_COPY(aRecord, state);
		FIELD_COPY(aRecord, open_price);
		FIELD_COPY(aRecord, sl);
		FIELD_COPY(aRecord, tp);
		FIELD_COPY(aRecord, close_time);
		FIELD_COPY(aRecord, value_date);
		FIELD_COPY(aRecord, expiration);
		FIELD_COPY(aRecord, reason);
		FIELD_COPY(aRecord, commission);
		FIELD_COPY(aRecord, commission_agent);
		FIELD_COPY(aRecord, storage);
		FIELD_COPY(aRecord, close_price);
		FIELD_COPY(aRecord, profit);
		FIELD_COPY(aRecord, taxes);
		FIELD_COPY(aRecord, magic);
		comment = marshal_as<String^, char*>(aRecord->comment);
		FIELD_COPY(aRecord, internal_id);
		FIELD_COPY(aRecord, activation);
		FIELD_COPY(aRecord, spread);
		FIELD_COPY(aRecord, margin_rate);
		FIELD_COPY(aRecord, timestamp);
	}
	int               order;            // order ticket
	int               login;            // owner's login
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 12)]
	String^           symbol;           // security
	int               digits;           // security precision
	int               cmd;              // trade command
	int               volume;           // volume

	__time32_t        open_time;        // open time
	int               state;            // reserved
	double            open_price;       // open price
	double            sl, tp;            // stop loss & take profit
	__time32_t        close_time;       // close time
	__time32_t        value_date;       // value date
	__time32_t        expiration;       // pending order's expiration time
	char              reason;           // trade reason
	// (first element-for open time, second element-for close time)
	double            commission;       // commission
	double            commission_agent; // agent commission
	double            storage;          // order swaps
	double            close_price;      // close price
	double            profit;           // profit
	double            taxes;            // taxes
	int               magic;            // special value used by client experts
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 32)]
	String^           comment;      // comment
	int               internal_id;      // trade order ticket on master server in STP
	int               activation;       // used by MT Manager
	int               spread;           // spread
	double            margin_rate;      // margin convertation rate (rate of convertation from margin currency to deposit one)
	__time32_t        timestamp;        // timestamp
};

[StructLayoutAttribute(LayoutKind::Sequential)]
public value struct UserRecordArgs
{
	UserRecord ToNative()
	{
		UserRecord ret = UserRecord();
		ret.login = login;
		ret.enable = TRUE;
		ret.send_reports = TRUE;
		ret.user_color = USER_COLOR_NONE;
		ret.leverage = leverage;
		TO_CHARS(group, ret);
		TO_CHARS(password, ret);
		TO_CHARS(name, ret);
		TO_CHARS(email, ret);
		return ret;
	}
	int login;  
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 16)]
	String^ group;
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 16)]
	String^ password;  
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 128)]
	String^ name;
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 48)]
	String^ email;
	int leverage;
};

public enum class TradeTransInfoTypes : UCHAR
{
	//---
	TT_PRICES_GET,                      // prices requets
	TT_PRICES_REQUOTE,                  // requote
	//--- client trade transaction
	TT_ORDER_IE_OPEN = 64,                // open order (Instant Execution)
	TT_ORDER_REQ_OPEN,                  // open order (Request Execution)
	TT_ORDER_MK_OPEN,                   // open order (Market Execution)
	TT_ORDER_PENDING_OPEN,              // open pending order
	//---
	TT_ORDER_IE_CLOSE,                  // close order (Instant Execution)
	TT_ORDER_REQ_CLOSE,                 // close order (Request Execution)
	TT_ORDER_MK_CLOSE,                  // close order (Market Execution)
	//---
	TT_ORDER_MODIFY,                    // modify pending order
	TT_ORDER_DELETE,                    // delete pending order
	TT_ORDER_CLOSE_BY,                  // close order by order
	TT_ORDER_CLOSE_ALL,                 // close all orders by symbol
	//--- broker trade transactions
	TT_BR_ORDER_OPEN,                   // open order
	TT_BR_ORDER_CLOSE,                  // close order
	TT_BR_ORDER_DELETE,                 // delete order (ANY OPEN ORDER!!!)
	TT_BR_ORDER_CLOSE_BY,               // close order by order
	TT_BR_ORDER_CLOSE_ALL,              // close all orders by symbol
	TT_BR_ORDER_MODIFY,                 // modify open price, stoploss, takeprofit etc. of order
	TT_BR_ORDER_ACTIVATE,               // activate pending order
	TT_BR_ORDER_COMMENT,                // modify comment of order
	TT_BR_BALANCE                       // balance/credit
};

[StructLayoutAttribute(LayoutKind::Sequential)]
public value struct TradeTransInfoArgsResult
{
	TradeTransInfo ToNative()
	{
		TradeTransInfo ret = TradeTransInfo();
		ret.type = (UCHAR)type;
		ret.reserved = reserved;
		ret.cmd = cmd;
		ret.order = order;
		ret.orderby = orderby;
		TO_CHARS(symbol, ret);
		ret.volume = volume;
		ret.price = price;
		ret.sl = sl;
		ret.tp = tp;
		ret.ie_deviation = ie_deviation;
		TO_CHARS(comment, ret);
		ret.expiration = expiration;
		ret.crc = crc;
		return ret;
	}

	void FromNative(TradeTransInfo* aRecord)
	{
		type = (TradeTransInfoTypes)aRecord->type;
		FIELD_COPY(aRecord, reserved);
		FIELD_COPY(aRecord, cmd);
		FIELD_COPY(aRecord, order);
		FIELD_COPY(aRecord, orderby);
		symbol = marshal_as<String^, char*>(aRecord->symbol);
		FIELD_COPY(aRecord, volume);
		FIELD_COPY(aRecord, price);
		FIELD_COPY(aRecord, sl);
		FIELD_COPY(aRecord, tp);
		FIELD_COPY(aRecord, ie_deviation);
		comment = marshal_as<String^, char*>(aRecord->comment);
		FIELD_COPY(aRecord, expiration);
		FIELD_COPY(aRecord, crc);
	}

	TradeTransInfoTypes  type;             // trade transaction type
	char                 reserved;         // reserved
	short                cmd;              // trade command
	int                  order, orderby;    // order, order by
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 12)]
	String^              symbol;
	int                  volume;           // trade volume
	double               price;            // trade price
	double               sl, tp;            // stoploss, takeprofit
	int                  ie_deviation;     // deviation on IE
	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 32)]
	String^              comment;      // comment
	__time32_t           expiration;       // pending order expiration time
	int                  crc;              // crc
};

[StructLayoutAttribute(LayoutKind::Sequential)]
public value struct SymbolInfoResult
{
	void FromNative(SymbolInfo* aRecord)
	{
		symbol = marshal_as<String^, char*>(aRecord->symbol);
		FIELD_COPY(aRecord,digits);
		FIELD_COPY(aRecord,count);
		FIELD_COPY(aRecord,visible);
		FIELD_COPY(aRecord,type);
		FIELD_COPY(aRecord,point);
		FIELD_COPY(aRecord,spread);
		FIELD_COPY(aRecord,spread_balance);
		FIELD_COPY(aRecord,direction);
		FIELD_COPY(aRecord,updateflag);
		FIELD_COPY(aRecord,lasttime);
		FIELD_COPY(aRecord,bid);
		FIELD_COPY(aRecord,ask);
		FIELD_COPY(aRecord,high);
		FIELD_COPY(aRecord,low);
		FIELD_COPY(aRecord,commission);
		FIELD_COPY(aRecord,comm_type);
	}

	[MarshalAs(UnmanagedType::ByValTStr, SizeConst = 12)]
	String^           symbol;
	int               digits;
	int               count;
	int               visible;
	int               type;
	double            point;
	int               spread;
	int               spread_balance;
	int               direction;
	int               updateflag;
	__time32_t        lasttime;
	double            bid, ask;
	double            high, low;
	double            commission;
	int               comm_type;
};