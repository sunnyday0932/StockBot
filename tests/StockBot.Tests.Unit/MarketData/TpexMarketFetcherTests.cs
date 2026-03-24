using StockBot.Infrastructure.MarketData;

namespace StockBot.Tests.Unit.MarketData;

public class TpexMarketFetcherTests
{
    // 模擬 TPEX API 的真實格式片段
    private const string ValidJson = """
        [
          {
            "Date": "1150323",
            "SecuritiesCompanyCode": "6547",
            "CompanyName": "高端疫苗",
            "Open": "27.05",
            "High": "27.65",
            "Low": "26.85",
            "Close": "27.45",
            "Change": "+0.40",
            "TradingShares": "3500000",
            "TransactionAmount": "96000000",
            "TransactionNumber": "8888"
          },
          {
            "Date": "1150323",
            "SecuritiesCompanyCode": "3008",
            "CompanyName": "大立光",
            "Open": "2500.00",
            "High": "2530.00",
            "Low": "2495.00",
            "Close": "2520.00",
            "Change": "+20.00",
            "TradingShares": "120000",
            "TransactionAmount": "302000000",
            "TransactionNumber": "12345"
          }
        ]
        """;

    private const string JsonWithSuspendedRow = """
        [
          {
            "Date": "1150323",
            "SecuritiesCompanyCode": "9999",
            "CompanyName": "停牌股",
            "Open": "---",
            "High": "---",
            "Low": "---",
            "Close": "---",
            "Change": "0",
            "TradingShares": "0",
            "TransactionAmount": "0",
            "TransactionNumber": "0"
          },
          {
            "Date": "1150323",
            "SecuritiesCompanyCode": "6547",
            "CompanyName": "高端疫苗",
            "Open": "27.05",
            "High": "27.65",
            "Low": "26.85",
            "Close": "27.45",
            "Change": "+0.40",
            "TradingShares": "3500000",
            "TransactionAmount": "96000000",
            "TransactionNumber": "8888"
          }
        ]
        """;

    [Fact]
    public void Parse_valid_json_returns_correct_records()
    {
        var records = TpexMarketFetcher.Parse(ValidJson);

        Assert.Equal(2, records.Count);

        var highEnd = records.First(r => r.StockCode == "6547");
        Assert.Equal("高端疫苗", highEnd.StockName);
        Assert.Equal(27.05m, highEnd.Open);
        Assert.Equal(27.65m, highEnd.High);
        Assert.Equal(26.85m, highEnd.Low);
        Assert.Equal(27.45m, highEnd.Close);
        Assert.Equal(3_500_000L, highEnd.Volume);
        Assert.Equal(96_000_000L, highEnd.TurnoverValue);
        Assert.Equal("TPEX", highEnd.Market);
        Assert.Equal(new DateOnly(2026, 3, 23), highEnd.TradingDate);
    }

    [Fact]
    public void Parse_skips_rows_with_triple_dash_values()
    {
        var records = TpexMarketFetcher.Parse(JsonWithSuspendedRow);

        Assert.Single(records);
        Assert.Equal("6547", records[0].StockCode);
    }

    [Fact]
    public void Parse_handles_empty_array()
    {
        var records = TpexMarketFetcher.Parse("[]");

        Assert.Empty(records);
    }

    [Theory]
    [InlineData("1150323", 2026, 3, 23)]
    [InlineData("1130101", 2024, 1, 1)]
    [InlineData("1121231", 2023, 12, 31)]
    public void TryParseRocDate_converts_correctly(string rocDate, int year, int month, int day)
    {
        var success = TpexMarketFetcher.TryParseRocDate(rocDate, out var result);

        Assert.True(success);
        Assert.Equal(new DateOnly(year, month, day), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("ABCMMDD")]
    public void TryParseRocDate_returns_false_for_invalid_input(string input)
    {
        var success = TpexMarketFetcher.TryParseRocDate(input, out _);

        Assert.False(success);
    }
}
