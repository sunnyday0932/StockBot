namespace StockBot.Infrastructure.MarketData;

/// <summary>
/// 一筆股票 OHLCV 日線紀錄，由 TWSE / TPEX API 解析後產生。
/// </summary>
public record StockOhlcvRecord(
    string StockCode,
    string StockName,
    DateOnly TradingDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    long TurnoverValue,
    string Market         // "TWSE" or "TPEX"
);
