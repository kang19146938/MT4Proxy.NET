#include "Stdafx.h"
#include "MT4ManagerAPI.h"
namespace MT4CliWrapper {
	public enum class RET_CODE
	{
		//--- common errors
		RET_OK = 0,        // all OK
		RET_OK_NONE,                     // all OK-no operation
		RET_ERROR,                       // general error
		RET_INVALID_DATA,                // invalid data
		RET_TECH_PROBLEM,                // server technical problem
		RET_OLD_VERSION,                 // old client terminal
		RET_NO_CONNECT,                  // no connection
		RET_NOT_ENOUGH_RIGHTS,           // no enough rights
		RET_TOO_FREQUENT,                // too frequently access to server
		RET_MALFUNCTION,                 // mulfunctional operation
		RET_GENERATE_KEY,                // need to send public key
		RET_SECURITY_SESSION,            // security session start
		//--- account status
		RET_ACCOUNT_DISABLED = 64,       // account blocked
		RET_BAD_ACCOUNT_INFO,            // bad account info
		RET_PUBLIC_KEY_MISSING,          // отсутствуе?ключ
		//--- trade
		RET_TRADE_TIMEOUT = 128,      // trade transatcion timeou expired
		RET_TRADE_BAD_PRICES,            // order has wrong prices
		RET_TRADE_BAD_STOPS,             // wrong stops level
		RET_TRADE_BAD_VOLUME,            // wrong lot size
		RET_TRADE_MARKET_CLOSED,         // market closed
		RET_TRADE_DISABLE,               // trade disabled
		RET_TRADE_NO_MONEY,              // no enough money for order execution
		RET_TRADE_PRICE_CHANGED,         // price changed
		RET_TRADE_OFFQUOTES,             // no quotes
		RET_TRADE_BROKER_BUSY,           // broker is busy
		RET_TRADE_REQUOTE,               // requote
		RET_TRADE_ORDER_LOCKED,          // order is proceed by dealer and cannot be changed
		RET_TRADE_LONG_ONLY,             // allowed only BUY orders
		RET_TRADE_TOO_MANY_REQ,          // too many requests from one client
		//--- order status notification
		RET_TRADE_ACCEPTED,              // trade request accepted by server and placed in request queue
		RET_TRADE_PROCESS,               // trade request accepted by dealerd
		RET_TRADE_USER_CANCEL,           // trade request canceled by client
		//--- additional return codes
		RET_TRADE_MODIFY_DENIED,         // trade modification denied
		RET_TRADE_CONTEXT_BUSY,          // trade context is busy (used in client terminal)
		RET_TRADE_EXPIRATION_DENIED,     // using expiration date denied
		RET_TRADE_TOO_MANY_ORDERS,       // too many orders
		RET_TRADE_HEDGE_PROHIBITED,      // hedge is prohibited
		RET_TRADE_PROHIBITED_BY_FIFO     // prohibited by fifo rule
	};
}