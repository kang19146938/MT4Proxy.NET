// 这是主 DLL 文件。
#include "stdafx.h"
#include "MT4Wrapper.h"
using namespace MT4CliWrapper;


void MT4Wrapper::init(String^ serverAddr, int user, String^ passwd, 
	FetchCacheDele^ fetchCache, UpdateCacheDele^ updateCache,
	FetchCacheDele^ removeCache)
{
	m_MT4Server = serverAddr;
	m_MT4ManagerAccount = user;
	m_MT4ManagerPassword = passwd;
	m_ManagerFactory = new CManagerFactory("mtmanapi.dll");
	m_ManagerFactory->WinsockStartup();
	PumpDelegate^ pumpDele = gcnew PumpDelegate(PumpCallback);
	m_hPumpHandle = GCHandle::Alloc(pumpDele);
	IntPtr ip = Marshal::GetFunctionPointerForDelegate(pumpDele);
	m_pPumpCallback = static_cast<MTAPI_NOTIFY_FUNC_EX>(ip.ToPointer());
	UpdateCache = updateCache;
	FetchCache = fetchCache;
	RemoveCache = removeCache;
}

void MT4Wrapper::uninit()
{
	m_hPumpHandle.Free();
	m_pPumpCallback = nullptr;
	if (m_ManagerFactory)
	{
		delete m_ManagerFactory;
		m_ManagerFactory = nullptr;
	}
}

MT4Wrapper::MT4Wrapper(bool aPump)
{
	if (!m_ManagerFactory->IsValid())
	{
		std::string strError = "初始化MT4Manager接口组件失败";
		Log(strError);
	}
	if (!aPump && (m_pManagerDirect = m_ManagerFactory->Create(ManAPIVersion)) == nullptr)
	{
		std::string strError = "初始化MT4Direct接口组件失败";
		Log(strError);
	}
	else if (!aPump)
	{
		ConnectDirect();
	}
	if (aPump && (m_pManagerPumping = m_ManagerFactory->Create(ManAPIVersion)) == nullptr)
	{
		std::string strError = "初始化MT4Pump接口组件失败";
		Log(strError);
	}
	else if (aPump)
	{
		ConnectPump();
	}
	
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
		auto remove = RemoveCache;
		if (remove != nullptr)
			remove(IntPtr(m_pManagerPumping));
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
		
		if (nRet == RET_OK)
		{
			String^ password = m_MT4ManagerPassword;
			std::string n_MT4ManagerPassword = marshal_as<std::string, System::String^>(password);
			int nRet = m_pManagerDirect->Login(m_MT4ManagerAccount, n_MT4ManagerPassword.c_str());
			if (nRet == RET_OK)
			{
				return true;
			}
			else
			{
				Log("MT4Direct登陆返回码: " + nRet.ToString());
			}
		}
		else
		{
			Log("MT4Direct连接返回码: " + nRet.ToString());
		}
	}
	return false;
}

bool MT4Wrapper::ConnectPump()
{
	if (m_pManagerPumping)
	{
		if (m_pManagerPumping->IsConnected())
			m_pManagerPumping->Disconnect();
		String^ mt4server = m_MT4Server;
		std::string n_mt4server = marshal_as<std::string, System::String^>(mt4server);
		int nRet = m_pManagerPumping->Connect(n_mt4server.c_str());
		if (nRet == RET_OK)
		{
			String^ password = m_MT4ManagerPassword;
			std::string n_MT4ManagerPassword = marshal_as<std::string, System::String^>(password);
			int nRet = m_pManagerPumping->Login(m_MT4ManagerAccount, n_MT4ManagerPassword.c_str());
			if (nRet == RET_OK)
			{
				auto key = IntPtr(m_pManagerPumping);
				auto update = UpdateCache;
				if (update != nullptr)
					update(key, this);
				m_pManagerPumping->PumpingSwitchEx(m_pPumpCallback, 0, m_pManagerPumping);
				return true;
			}
			else
			{
				Log("MT4Pump登陆返回码: " + nRet.ToString());
			}
		}
		else
		{
			Log("MT4Pump连接返回码: " + nRet.ToString());
		}
	}
	return false;
}

bool MT4Wrapper::IsPumpAlive()
{
	if (m_pManagerPumping && m_pManagerPumping->IsConnected())
		return true;
	return false;
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

int MT4Wrapper::PumpCallback(int code, int type, void *data, void *param)
{
	static SymbolInfo pSymbolInfos[64];
	auto key = IntPtr(param);
	MT4Wrapper^ value = nullptr;
	auto fetch = FetchCache;
	if (fetch != nullptr)
	{
		value = (MT4Wrapper^)fetch(key);
		if (!value) return TRUE;
	}
	else
	{
		Log(String::Format("pump推送缓存系统未实现"));
		return TRUE;
	}
	if (code == PUMP_UPDATE_TRADES && data != NULL)
	{
		TradeRecord *trade = (TradeRecord*)data;
		if (type == TRANS_ADD || type == TRANS_DELETE)
		{
			TradeRecordResult result;
			result.FromNative(trade);
			if (value)
				value->OnPumpTrade((TRANS_TYPE)type, result);
		}
	}
	if (code == PUMP_UPDATE_BIDASK)
	{
		int total = value->m_pManagerPumping->SymbolInfoUpdated(pSymbolInfos, 64);
		auto clrItems = gcnew array<SymbolInfoResult>(total);
		for (int i = 0; i < total; i++)
		{
			auto clrItem = SymbolInfoResult();
			clrItem.FromNative(&pSymbolInfos[i]);
			clrItems[i] = clrItem;
		}
		value->OnPumpAskBid(clrItems);
	}
	return TRUE;
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
	if (ret)
		m_pManagerDirect->MemFree(ret);
	return result;
}

RET_CODE MT4Wrapper::UserRecordNew(UserRecordArgs aArgs)
{
	int ret = m_pManagerDirect->UserRecordNew(&aArgs.ToNative());
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
		m_pManagerDirect->MemFree(ret);
	return result;
}

RET_CODE MT4Wrapper::ChangePassword(const int login, String^ password)
{
	std::string passwd = marshal_as<std::string, System::String^>(password);
	int nRet = m_pManagerDirect->UserPasswordSet(login, passwd.c_str(), TRUE, FALSE);
	return (RET_CODE)nRet;
}

RET_CODE MT4Wrapper::GetEquity(int login, Double% equity)
{
	MarginLevel marginLevel;
	memset(&marginLevel, 0, sizeof(MarginLevel));
	int nRet = m_pManagerDirect->MarginLevelRequest(login, &marginLevel);
	if (nRet == RET_OK)
		equity = marginLevel.equity;
	return (RET_CODE)nRet;
}