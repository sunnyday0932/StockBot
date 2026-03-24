using System.Globalization;

namespace StockBot.Infrastructure.MarketData;

/// <summary>
/// TWSE / TPEX 行情資料解析共用工具。
/// 集中處理民國年日期、千分位數字等台股 API 特有格式，消除各 Fetcher 之間的重複邏輯。
/// </summary>
internal static class MarketDataParser
{
    /// <summary>民國年格式 YYYMMDD → DateOnly。例：「1150323」→ DateOnly(2026, 3, 23)</summary>
    internal static bool TryParseRocDate(string rocDate, out DateOnly result)
    {
        result = default;

        if (rocDate.Length != 7)
            return false;

        if (!int.TryParse(rocDate[..3], out var rocYear) ||
            !int.TryParse(rocDate[3..5], out var month)  ||
            !int.TryParse(rocDate[5..],  out var day))
            return false;

        try
        {
            result = new DateOnly(rocYear + 1911, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>移除千分位逗號後解析為 decimal。例：「38,500,000」→ 38500000</summary>
    internal static bool TryParseDecimal(string raw, out decimal value)
    {
        var cleaned = raw.Replace(",", "");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>移除千分位逗號後解析為 long。例：「36,960,000,000」→ 36960000000</summary>
    internal static bool TryParseLong(string raw, out long value)
    {
        var cleaned = raw.Replace(",", "");
        return long.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
