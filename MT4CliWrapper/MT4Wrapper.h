// FTS.Cpp.Warpper.h

#pragma once
#pragma comment(lib,"ws2_32.lib")
#include "MT4ManagerAPI.h"
using namespace System;
#include <set>
#include "MT4Args.h"
#include "ReturnCode.h"

namespace MT4CliWrapper {

	public delegate void LogResponse(String^);

	public ref class MT4Wrapper
	{
	public:
		static void init(String^ serverAddr, int user, String^ passwd);
		static void uninit();
		MT4Wrapper();
		~MT4Wrapper();
		!MT4Wrapper();
		void Release();

		bool ConnectDirect();
		bool ConnectDirect(String^ server, int managerAccount, String^ password);
	public: //APIs
		RET_CODE TradeTransaction(TradeTransInfoArgs);
		RET_CODE MarginLevelRequest(const int login, MarginLevelArgs% level);
		array<TradeRecordResult>^ UserRecordsRequest(const int logins, int from, int to);
		RET_CODE UserRecordNew(UserRecordArgs aArgs);
		TradeRecordResult AdmTradesRequest(int orderID, bool open_only);
	public: //Utils
		static event LogResponse^ OnLog;
	private:
		void Log(String^);
		void Log(std::string);
	private:
		static CManagerFactory*   m_ManagerFactory;
		// use Enum as key instead
		//	std::map<const std::string, CManagerInterface*> m_mapManagerDirect;
		CManagerInterface* m_pManagerDirect;
		CManagerInterface* m_pManagerPumping;

		static String^ m_MT4Server;
		static int m_MT4ManagerAccount;
		static String^ m_MT4ManagerPassword;
	};
}
