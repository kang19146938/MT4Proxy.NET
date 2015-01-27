using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;


namespace MT4Proxy.NET.Core
{
    public static class ForexMath
    {
        private static Dictionary<string, double> _dictSpecialPOVs =
            null;

        private static Dictionary<string, int> _dictCfdLeverages =
            null;

        static ForexMath()
        {
            _dictSpecialPOVs = new Dictionary<string, double>();
            _dictSpecialPOVs["WTOil"] = 450000;
            _dictSpecialPOVs["USDX"] = 450000;
            _dictSpecialPOVs["DAX"] = 150000;
            _dictSpecialPOVs["FFI"] = 150000;
            _dictSpecialPOVs["NK"] = 20000;
            _dictSpecialPOVs["HSI"] = 30000;
            _dictSpecialPOVs["SFC"] = 100000;
            _dictSpecialPOVs["mDJ"] = 200000;
            _dictSpecialPOVs["mND"] = 400000;
            _dictSpecialPOVs["mSP"] = 200000;
            _dictCfdLeverages = new Dictionary<string, int>();
            _dictCfdLeverages["WTOil"] = 100;
            _dictCfdLeverages["USDX"] = 100;
            _dictCfdLeverages["DAX"] = 100;
            _dictCfdLeverages["FFI"] = 100;
            _dictCfdLeverages["NK"] = 100;
            _dictCfdLeverages["HSI"] = 100;
            _dictCfdLeverages["SFC"] = 100;
            _dictCfdLeverages["mDJ"] = 100;
            _dictCfdLeverages["mND"] = 100;
            _dictCfdLeverages["mSP"] = 100;
        }

        /// <summary>
        /// 获取一手商品的价格
        /// </summary>
        /// <param name="aSymbol">商品名称</param>
        /// <returns></returns>
        public static double GetPOV(string aSymbol)
        {
            if(_dictSpecialPOVs.ContainsKey(aSymbol))
                return _dictSpecialPOVs[aSymbol];
            return 100000;
        }

        public static int Cash2Volume(double aCash, string aSymbol, int aLeverage)
        {
            return (int)(aCash * aLeverage * 100 / GetPOV(aSymbol));
        }

        public static double Volume2Cash(int aVolume, string aSymbol, int aLeverage)
        {
            return GetPOV(aSymbol) * aVolume * 0.01 / aLeverage;
        }

        public static int CopyTransform(double aFromUserBalance,
            int aFromVolume, double aCopyAmount)
        {
            //Pa : Pb = Va : Vb
            return (int)(aFromVolume * aCopyAmount / aFromUserBalance);
        }
    }
}
