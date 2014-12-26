using MT4CliWrapper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;
using System.Threading;
using NLog;

namespace MT4Proxy.NET.Core
{
    public static class Utils
    {
        public static Logger CommonLog
        {
            get
            {
                return LogManager.GetLogger("common");
            }
        }
        public static bool SignalWait(ref bool aCrond, Semaphore aSignal, int aTimeoutMS=1000)
        {
            while (true)
            {
                if (!aCrond) return false;
                var result = aSignal.WaitOne(aTimeoutMS);
                if (result) break;
            }
            return true;
        }

        public static bool SignalWait(Semaphore aSignal, int aTimeoutMS = 1000)
        {
            while (true)
            {
                var result = aSignal.WaitOne(aTimeoutMS);
                if (result) break;
            }
            return true;
        }

        public static dynamic MakeResponseObject(bool is_succ, int errCode, string errMsg)
        {
            dynamic ret = new ExpandoObject();
            ret.is_succ = is_succ;
            ret.errCode = errCode;
            ret.errMsg = errMsg;
            return ret;
        }

        public static IEnumerable<Type> GetTypesWithServiceAttribute()
        {
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttributes(typeof(MT4ServiceAttribute), true).Length > 0)
                {
                    yield return type;
                }
            }
        }

        private static readonly DateTime origin = new DateTime(1970, 1, 1);
        public static int ToTime32(this DateTime aDatetime)
        {
            return (int)(aDatetime - origin).TotalSeconds;
        }

        public static DateTime FromTime32(this int aTime32)
        {
            return origin.AddSeconds(aTime32);
        }

        public static string GetErrorMessage(RET_CODE errCode)
        {
            string errMsg;
            switch (errCode)
            {
                case RET_CODE.RET_OK:
                    errMsg = "正确";
                    break;
                case RET_CODE.RET_OK_NONE:
                    errMsg = "正确，无操作";
                    break;
                case RET_CODE.RET_ERROR:
                    errMsg = "常规错误";
                    break;
                case RET_CODE.RET_INVALID_DATA:
                    errMsg = "数据无效";
                    break;
                case RET_CODE.RET_TECH_PROBLEM:
                    errMsg = "服务器遇到技术问题";
                    break;
                //		case RET_CODE.RET_OLD_VERSION:
                //			errMsg = L"客户端版本过旧";
                //			break;
                //		case RET_CODE.RET_NO_CONNECT:
                //			errMsg = L"无连接";
                //			break;
                //		case RET_CODE.RET_NOT_ENOUGH_RIGHTS:
                //			errMsg = L"无所需权限";
                //			break;
                case RET_CODE.RET_TOO_FREQUENT:
                    errMsg = "访问过于频繁";
                    break;
                case RET_CODE.RET_MALFUNCTION:
                    errMsg = "非法操作";
                    break;
                //		case RET_CODE.RET_GENERATE_KEY :
                //			errMsg = L"需要发送公钥信息";
                //			break;
                //		case RET_CODE.RET_SECURITY_SESSION:
                //			errMsg = L"安全会话启动";
                //			break;
                case RET_CODE.RET_ACCOUNT_DISABLED:
                    errMsg = "账户已被封禁";
                    break;
                case RET_CODE.RET_BAD_ACCOUNT_INFO:
                    errMsg = "账户信息错误";
                    break;
                //		case RET_CODE.RET_PUBLIC_KEY_MISSING:
                //			errMsg = L"公钥信息缺失";
                //			break;
                case RET_CODE.RET_TRADE_TIMEOUT:
                    errMsg = "交易超时";
                    break;
                case RET_CODE.RET_TRADE_BAD_PRICES:
                    errMsg = "无效的下单价格";
                    break;
                case RET_CODE.RET_TRADE_BAD_STOPS:
                    errMsg = "无效的止损止盈";
                    break;
                case RET_CODE.RET_TRADE_BAD_VOLUME:
                    errMsg = "无效的下单量";
                    break;
                case RET_CODE.RET_TRADE_MARKET_CLOSED:
                    errMsg = "市场已关闭";
                    break;
                case RET_CODE.RET_TRADE_DISABLE:
                    errMsg = "禁止交易";
                    break;
                case RET_CODE.RET_TRADE_NO_MONEY:
                    errMsg = "资金不足";
                    break;
                case RET_CODE.RET_TRADE_PRICE_CHANGED:
                    errMsg = "价格已经变化";
                    break;
                //		case RET_CODE.RET_TRADE_OFFQUOTES:
                //			errMsg = L"没有报价信息";
                //			break;
                case RET_CODE.RET_TRADE_BROKER_BUSY:
                    errMsg = "经纪商繁忙";
                    break;
                //		case RET_CODE.RET_TRADE_REQUOTE:
                //			errMsg = L"重新要价";
                //			break;
                case RET_CODE.RET_TRADE_ORDER_LOCKED:
                    errMsg = "订单已被锁定，无法修改";
                    break;
                case RET_CODE.RET_TRADE_LONG_ONLY:
                    errMsg = "仅允许买单";
                    break;
                case RET_CODE.RET_TRADE_TOO_MANY_REQ:
                    errMsg = "同一客户端请求次数过多";
                    break;
                case RET_CODE.RET_TRADE_ACCEPTED:
                    errMsg = "交易请求已被接受";
                    break;
                case RET_CODE.RET_TRADE_PROCESS:
                    errMsg = "交易请求正在处理中";
                    break;
                case RET_CODE.RET_TRADE_USER_CANCEL:
                    errMsg = "交易请求被客户取消";
                    break;
                case RET_CODE.RET_TRADE_MODIFY_DENIED:
                    errMsg = "无法修改订单";
                    break;
                //		case RET_CODE.RET_TRADE_CONTEXT_BUSY:
                //			errMsg = L"交易上下文繁忙";
                //			break;
                case RET_CODE.RET_TRADE_EXPIRATION_DENIED:
                    errMsg = "不能使用订单有效期限";
                    break;
                case RET_CODE.RET_TRADE_TOO_MANY_ORDERS:
                    errMsg = "订单数量过多";
                    break;
                case RET_CODE.RET_TRADE_HEDGE_PROHIBITED:
                    errMsg = "禁止对冲交易操作";
                    break;
                case RET_CODE.RET_TRADE_PROHIBITED_BY_FIFO:
                    errMsg = "由于FIFO规则，无法进行交易";
                    break;
                default:
                    errMsg = "服务器其他问题";
                    break;
            }
            return errMsg;
        }
    }
}
