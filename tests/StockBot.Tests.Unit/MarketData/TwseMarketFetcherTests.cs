using StockBot.Infrastructure.MarketData;

namespace StockBot.Tests.Unit.MarketData;

public class TwseMarketFetcherTests
{
    // 模擬 TWSE API 的真實格式片段
    private const string ValidJson = """
        [
          {
            "Date": "1150323",
            "Code": "2330",
            "Name": "台積電",
            "TradeVolume": "38,500,000",
            "TradeValue": "36,960,000,000",
            "OpeningPrice": "950.00",
            "HighestPrice": "965.00",
            "LowestPrice": "948.00",
            "ClosingPrice": "960.00",
            "Change": "+10.00",
            "Transaction": "123456"
          },
          {
            "Date": "1150323",
            "Code": "0050",
            "Name": "元大台灣50",
            "TradeVolume": "5,000,000",
            "TradeValue": "370,000,000",
            "OpeningPrice": "73.50",
            "HighestPrice": "74.45",
            "LowestPrice": "73.30",
            "ClosingPrice": "74.25",
            "Change": "-1.65",
            "Transaction": "99999"
          }
        ]
        """;

    private const string JsonWithInvalidRow = """
        [
          {
            "Date": "1150323",
            "Code": "9999",
            "Name": "停牌股",
            "TradeVolume": "--",
            "TradeValue": "--",
            "OpeningPrice": "--",
            "HighestPrice": "--",
            "LowestPrice": "--",
            "ClosingPrice": "--",
            "Change": "0",
            "Transaction": "--"
          },
          {
            "Date": "1150323",
            "Code": "2454",
            "Name": "聯發科",
            "TradeVolume": "2,000,000",
            "TradeValue": "1,200,000,000",
            "OpeningPrice": "590.00",
            "HighestPrice": "600.00",
            "LowestPrice": "588.00",
            "ClosingPrice": "598.00",
            "Change": "+8.00",
            "Transaction": "55555"
          }
        ]
        """;

    [Fact]
    public void Parse_valid_json_returns_correct_records()
    {
        var records = TwseMarketFetcher.Parse(ValidJson);

        Assert.Equal(2, records.Count);

        var tsmc = records.First(r => r.StockCode == "2330");
        Assert.Equal("台積電", tsmc.StockName);
        Assert.Equal(950.00m, tsmc.Open);
        Assert.Equal(965.00m, tsmc.High);
        Assert.Equal(948.00m, tsmc.Low);
        Assert.Equal(960.00m, tsmc.Close);
        Assert.Equal(38_500_000L, tsmc.Volume);
        Assert.Equal(36_960_000_000L, tsmc.TurnoverValue);
        Assert.Equal("TWSE", tsmc.Market);
        Assert.Equal(new DateOnly(2026, 3, 23), tsmc.TradingDate);
    }

    [Fact]
    public void Parse_skips_rows_with_double_dash_values()
    {
        var records = TwseMarketFetcher.Parse(JsonWithInvalidRow);

        Assert.Single(records);
        Assert.Equal("2454", records[0].StockCode);
    }

    [Fact]
    public void Parse_handles_empty_array()
    {
        var records = TwseMarketFetcher.Parse("[]");

        Assert.Empty(records);
    }

    [Fact]
    public void Parse_handles_numbers_with_thousand_separators()
    {
        var records = TwseMarketFetcher.Parse(ValidJson);

        // TradeVolume "38,500,000" 應正確解析
        var tsmc = records.First(r => r.StockCode == "2330");
        Assert.Equal(38_500_000L, tsmc.Volume);
        Assert.Equal(36_960_000_000L, tsmc.TurnoverValue);
    }

    [Theory]
    [InlineData("1150323", 2026, 3, 23)]
    [InlineData("1130101", 2024, 1, 1)]
    [InlineData("1121231", 2023, 12, 31)]
    public void TryParseRocDate_converts_correctly(string rocDate, int year, int month, int day)
    {
        var success = MarketDataParser.TryParseRocDate(rocDate, out var result);

        Assert.True(success);
        Assert.Equal(new DateOnly(year, month, day), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]       // 長度不對
    [InlineData("ABCMMDD")]     // 非數字
    public void TryParseRocDate_returns_false_for_invalid_input(string input)
    {
        var success = MarketDataParser.TryParseRocDate(input, out _);

        Assert.False(success);
    }
}
