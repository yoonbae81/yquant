using yQuant.Core.Models;

namespace yQuant.Core.Extensions
{
    public static class CurrencyTypeExtensions
    {
        public static string GetCountryCode(this CurrencyType currency)
        {
            return currency switch
            {
                CurrencyType.KRW => "KR",
                CurrencyType.USD => "US",
                CurrencyType.JPY => "JP",
                CurrencyType.CNY => "CN",
                CurrencyType.HKD => "HK",
                CurrencyType.VND => "VN",
                _ => "Unknown"
            };
        }
    }
}
