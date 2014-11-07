// FTS.Cpp.Warpper.h

#pragma once
#pragma comment(lib,"ws2_32.lib")
#include "MT4ManagerAPI.h"
using namespace System;
using namespace System::Collections::Concurrent;
#include <set>
#include "MT4Args.h"
#include "ReturnCode.h"

namespace MT4CliWrapper {

	public delegate void LogResponse(String^);
	public delegate Object^ FetchCacheDele(IntPtr);
	public delegate void UpdateCacheDele(IntPtr, Object^);
	public enum class TRANS_TYPE : int { TRANS_ADD, TRANS_DELETE, TRANS_UPDATE, TRANS_CHANGE_GRP };

	private delegate int PumpDelegate(int code, int type, void *data, void *param);

	public ref class MT4Wrapper
	{
	public:
		static void init(String^ serverAddr, int user, String^ passwd,
			FetchCacheDele^ fetchCache, UpdateCacheDele^ updateCache,
			FetchCacheDele^ removeCache);
		static void uninit();
		MT4Wrapper(bool);
		~MT4Wrapper();
		!MT4Wrapper();
		void Release();

		bool ConnectDirect();
		bool ConnectPump();

		bool IsPumpAlive();
	public: //APIs
		RET_CODE TradeTransaction(TradeTransInfoArgs);
		RET_CODE MarginLevelRequest(const int login, MarginLevelArgs% level);
		array<TradeRecordResult>^ UserRecordsRequest(const int logins, int from, int to);
		RET_CODE UserRecordNew(UserRecordArgs aArgs);
		TradeRecordResult AdmTradesRequest(int orderID, bool open_only);
		RET_CODE ChangePassword(const int login, String^ password);
		RET_CODE GetEquity(int login, Double%);
	public: //Events & callbacks
		static event LogResponse^ OnLog;
	protected: //Pump callback
		virtual void OnPumpTrade(TRANS_TYPE, TradeRecordResult){ }
		virtual void OnPumpAskBid(array<SymbolInfoResult>^){ }
	private:
		static UpdateCacheDele^ UpdateCache;
		static FetchCacheDele^ FetchCache;
		static FetchCacheDele^ RemoveCache;
		static int PumpCallback(int code, int type, void *data, void *param);
		static void Log(String^);
		static void Log(std::string);
	private:
		static CManagerFactory*   m_ManagerFactory;
		CManagerInterface* m_pManagerDirect;
		CManagerInterface* m_pManagerPumping;

		static String^ m_MT4Server;
		static int m_MT4ManagerAccount;
		static String^ m_MT4ManagerPassword;

		static GCHandle m_hPumpHandle;
		static MTAPI_NOTIFY_FUNC_EX m_pPumpCallback;
	};
}
