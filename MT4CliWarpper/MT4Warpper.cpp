// 这是主 DLL 文件。
#include "stdafx.h"
#include "MT4Warpper.h"
using namespace MT4CliWarpper;

void MT4Wrapper::init()
{
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
	ConnectDirect("202.65.221.52:443", 55800, "elex1234");
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

void MT4Wrapper::FreeNodes(void* aHead, size_t aNodeSize)
{
	if (!aHead) return;
	char* ptr = (char*)aHead;
	do
	{
		void* node = ptr;
		ptr = (char*)*(ptr + aNodeSize - sizeof(void*));
		free(node);
	} while (ptr);
}

int MT4Wrapper::TradeTransaction(TradeTransInfoArgs aArgs)
{
	return m_pManagerDirect->TradeTransaction(&aArgs.ToNative());
}

int MT4Wrapper::MarginLevelRequest(const int login, MarginLevelArgs% level)
{
	MarginLevel n_level = MarginLevel();
	memset(&n_level, 0, sizeof(n_level));
	int ret = m_pManagerDirect->MarginLevelRequest(login, &n_level);
	if (ret == RET_OK)
	{
		level.FromNative(n_level);
	}
	return ret;
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