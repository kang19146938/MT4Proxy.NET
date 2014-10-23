// 这是主 DLL 文件。
#include "stdafx.h"
#include "MT4Wrapper.h"
using namespace MT4CliWrapper;


void MT4Wrapper::init(String^ serverAddr, int user, String^ passwd)
{
	m_MT4Server = serverAddr;
	m_MT4ManagerAccount = user;
	m_MT4ManagerPassword = passwd;
	m_ManagerFactory = new CManagerFactory("mtmanapi.dll");
	m_ManagerFactory->WinsockStartup();
}

void MT4Wrapper::uninit()
{
	if (m_ManagerFactory)
	{
		delete m_ManagerFactory;
		m_ManagerFactory = nullptr;
	}
}

MT4Wrapper::MT4Wrapper()
{
	if (!m_ManagerFactory->IsValid()
		|| (m_pManagerDirect = m_ManagerFactory->Create(ManAPIVersion)) == nullptr
		|| (m_pManagerPumping = m_ManagerFactory->Create(ManAPIVersion)) == nullptr)
	{
		std::string strError = "Failed to create MetaTrader 4 Manager API interface.";
		Log(strError);
	}
	ConnectDirect(m_MT4Server, m_MT4ManagerAccount, m_MT4ManagerPassword);
}

MT4Wrapper::~MT4Wrapper()
{
	Release();
}

MT4Wrapper::!MT4Wrapper()
{
	Release();
}

void MT4Wrapper::Release()
{
	if (m_pManagerDirect != nullptr)
	{
		m_pManagerDirect->Release();
		m_pManagerDirect = nullptr;
	}
	if (m_pManagerPumping != nullptr)
	{
		m_pManagerPumping->Release();
		m_pManagerPumping = nullptr;
	}
}

bool MT4Wrapper::ConnectDirect()
{
	if (m_pManagerDirect)
	{
		if (m_pManagerDirect->IsConnected())
			m_pManagerDirect->Disconnect();
		String^ mt4server = m_MT4Server;
		std::string n_mt4server = marshal_as<std::string, System::String^>(mt4server);
		int nRet = m_pManagerDirect->Connect(n_mt4server.c_str());
		Log("ConnectDirect code: " + nRet.ToString());
		if (nRet == RET_OK)
		{
			String^ password = m_MT4ManagerPassword;
			std::string n_MT4ManagerPassword = marshal_as<std::string, System::String^>(password);
			int nRet = m_pManagerDirect->Login(m_MT4ManagerAccount, n_MT4ManagerPassword.c_str());
			Log("ConnectDirect login code: " + nRet.ToString());
			if (nRet == RET_OK)
			{
				return true;
			}
		}
	}
	return false;
}

bool MT4Wrapper::ConnectDirect(String^ server, int managerAccount, String^ password)
{
	m_MT4Server = server;
	m_MT4ManagerAccount = managerAccount;
	m_MT4ManagerPassword = password;
	return ConnectDirect();
}

void MT4Wrapper::Log(String^ aLog)
{
	OnLog(aLog);
}
void MT4Wrapper::Log(std::string aLog)
{
	String^ log = marshal_as<System::String^, std::string>(aLog);
	Log(log);
}

RET_CODE MT4Wrapper::TradeTransaction(TradeTransInfoArgs aArgs)
{
	return (RET_CODE)m_pManagerDirect->TradeTransaction(&aArgs.ToNative());
}

RET_CODE MT4Wrapper::MarginLevelRequest(const int login, MarginLevelArgs% level)
{
	MarginLevel n_level = MarginLevel();
	memset(&n_level, 0, sizeof(n_level));
	int ret = m_pManagerDirect->MarginLevelRequest(login, &n_level);
	if (ret == RET_OK)
	{
		level.FromNative(n_level);
	}
	return (RET_CODE)ret;
}

array<TradeRecordResult>^ MT4Wrapper::UserRecordsRequest(const int logins, int from, int to)
{
	int count = 0;
	TradeRecord* ret = m_pManagerDirect->TradesUserHistory(logins, from, to, &count);
	if (!count) return gcnew array<TradeRecordResult>(0);
	array<TradeRecordResult>^ result = gcnew  array<TradeRecordResult>(count);
	for (int i = 0; i < count; i++)
	{
		result[i].cmd = ret[i].cmd;
		result[i].digits = ret[i].digits;
		result[i].login = ret[i].login;
		result[i].order = ret[i].order;
		result[i].symbol = marshal_as<System::String^, std::string>(ret[i].symbol);
		result[i].volume = ret[i].volume;
	}
	m_pManagerDirect->MemFree(ret);
	return result;
}

RET_CODE MT4Wrapper::UserRecordNew(UserRecordArgs aArgs)
{
	int ret = m_pManagerDirect->UserRecordNew(&aArgs.ToNative());
	if (ret != RET_OK)
	{
		printf("%s\n", m_pManagerDirect->ErrorDescription(ret));
	}
	return (RET_CODE)ret;
}

TradeRecordResult MT4Wrapper::AdmTradesRequest(int orderID, bool open_only)
{
	String^ order = String::Format("#{0}", orderID);
	const char* orderName = marshal_as<std::string, System::String^>(order).c_str();
	int resultTotal = 0;
	TradeRecord* ret = m_pManagerDirect->AdmTradesRequest(orderName, open_only ? 1 : 0, &resultTotal);
	TradeRecordResult result = TradeRecordResult();
	if (resultTotal == 1)
		result.FromNative(ret);
	if (ret)
	{
		m_pManagerDirect->MemFree(ret);
		ret = nullptr;
	}
	return result;
}