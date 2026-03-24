using StockBot.Domain.Enums;
using StockBot.Infrastructure.News;

namespace StockBot.Tests.Unit.News;

public class CnyesRssParserTests
{
    private const string ValidRss = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0">
          <channel>
            <title>鉅亨網 - 台股新聞</title>
            <item>
              <title>台積電法說會：下季營收指引優於預期</title>
              <link>https://news.cnyes.com/news/id/5812345</link>
              <description><![CDATA[<p>台積電（2330）今日召開法說會，董事長魏哲家表示...</p>]]></description>
              <pubDate>Mon, 24 Mar 2026 10:00:00 +0800</pubDate>
              <guid>https://news.cnyes.com/news/id/5812345</guid>
            </item>
            <item>
              <title>聯發科第一季營收創新高</title>
              <link>https://news.cnyes.com/news/id/5812346</link>
              <description><![CDATA[聯發科（2454）公布第一季合併營收...]]></description>
              <pubDate>Mon, 24 Mar 2026 09:30:00 +0800</pubDate>
              <guid>https://news.cnyes.com/news/id/5812346</guid>
            </item>
          </channel>
        </rss>
        """;

    private const string RssWithSingleDigitDay = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0">
          <channel>
            <item>
              <title>測試單位數日期</title>
              <link>https://news.cnyes.com/news/id/9999999</link>
              <description>測試內容</description>
              <pubDate>Mon, 3 Mar 2026 08:00:00 +0800</pubDate>
              <guid>https://news.cnyes.com/news/id/9999999</guid>
            </item>
          </channel>
        </rss>
        """;

    private const string RssWithHtmlDescription = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0">
          <channel>
            <item>
              <title>HTML 內容測試</title>
              <link>https://news.cnyes.com/news/id/1234567</link>
              <description><![CDATA[<p><strong>重點：</strong>這是一段<a href="#">帶連結</a>的<em>HTML</em>內容。</p>]]></description>
              <pubDate>Mon, 24 Mar 2026 10:00:00 +0800</pubDate>
              <guid>https://news.cnyes.com/news/id/1234567</guid>
            </item>
          </channel>
        </rss>
        """;

    [Fact]
    public void Parse_valid_rss_returns_correct_documents()
    {
        var docs = CnyesRssParser.Parse(ValidRss);

        Assert.Equal(2, docs.Count);

        var first = docs.First(d => d.DocumentId == "cnyes_5812345");
        Assert.Equal("台積電法說會：下季營收指引優於預期", first.Title);
        Assert.Equal("https://news.cnyes.com/news/id/5812345", first.Url);
        Assert.Equal(SourceType.NewsCnyes, first.SourceType);
        // pubDate +0800 → UTC: 10:00 - 8h = 02:00
        Assert.Equal(new DateTime(2026, 3, 24, 2, 0, 0, DateTimeKind.Utc), first.PublishedAt);
    }

    [Fact]
    public void Parse_strips_html_from_description()
    {
        var docs = CnyesRssParser.Parse(RssWithHtmlDescription);

        Assert.Single(docs);
        var content = docs[0].Content;
        Assert.DoesNotContain("<p>", content);
        Assert.DoesNotContain("<strong>", content);
        Assert.Contains("重點：", content);
        Assert.Contains("HTML", content);
    }

    [Fact]
    public void Parse_empty_channel_returns_empty_list()
    {
        const string emptyRss = """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0"><channel></channel></rss>
            """;

        var docs = CnyesRssParser.Parse(emptyRss);

        Assert.Empty(docs);
    }

    [Fact]
    public void Parse_skips_item_missing_title_or_link()
    {
        const string rss = """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0">
              <channel>
                <item>
                  <link>https://news.cnyes.com/news/id/1111111</link>
                  <pubDate>Mon, 24 Mar 2026 10:00:00 +0800</pubDate>
                </item>
                <item>
                  <title>有標題但沒連結</title>
                  <pubDate>Mon, 24 Mar 2026 10:00:00 +0800</pubDate>
                </item>
              </channel>
            </rss>
            """;

        var docs = CnyesRssParser.Parse(rss);

        Assert.Empty(docs);
    }

    [Theory]
    [InlineData("Mon, 24 Mar 2026 10:00:00 +0800", 2026, 3, 24, 2, 0, 0)]  // +0800 → UTC
    [InlineData("Mon, 3 Mar 2026 08:00:00 +0800",  2026, 3,  3, 0, 0, 0)]  // single-digit day
    [InlineData("Fri, 01 Jan 2027 00:00:00 +0000", 2027, 1,  1, 0, 0, 0)]  // UTC
    public void TryParsePubDate_converts_to_utc_correctly(
        string raw, int year, int month, int day, int hour, int min, int sec)
    {
        var success = CnyesRssParser.TryParsePubDate(raw, out var result);

        Assert.True(success);
        Assert.Equal(new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("2026-03-24")]
    public void TryParsePubDate_returns_false_for_invalid_input(string? input)
    {
        var success = CnyesRssParser.TryParsePubDate(input, out _);

        Assert.False(success);
    }

    [Fact]
    public void Parse_single_digit_day_in_pubdate_is_handled()
    {
        var docs = CnyesRssParser.Parse(RssWithSingleDigitDay);

        Assert.Single(docs);
        Assert.Equal(new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc), docs[0].PublishedAt);
    }
}
