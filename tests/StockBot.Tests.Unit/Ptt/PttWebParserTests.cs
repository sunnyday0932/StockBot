using StockBot.Infrastructure.Ptt;

namespace StockBot.Tests.Unit.Ptt;

public class PttWebParserTests
{
    // ── Index page ──────────────────────────────────────────────────────────

    private const string IndexHtmlWithTwoArticles = """
        <html><body>
        <div class="r-list-container action-bar-margin bbs-screen">
          <div class="r-ent">
            <div class="nrec"><span class="hl f3">81</span></div>
            <div class="title">
              <a href="/bbs/Stock/M.1774317183.A.F71.html">[新聞] 美德州瓦萊羅煉油廠爆炸</a>
            </div>
            <div class="meta">
              <div class="author">richer23</div>
              <div class="date"> 3/24</div>
            </div>
          </div>
          <div class="r-ent">
            <div class="nrec"><span class="hl f3">55</span></div>
            <div class="title">
              <a href="/bbs/Stock/M.1774318445.A.54D.html">[新聞] 華爾街潑冷水</a>
            </div>
            <div class="meta">
              <div class="author">Lime5566</div>
              <div class="date"> 3/24</div>
            </div>
          </div>
        </div>
        </body></html>
        """;

    private const string IndexHtmlWithDeletedArticle = """
        <html><body>
        <div class="r-list-container action-bar-margin bbs-screen">
          <div class="r-ent">
            <div class="nrec"><span class="hl f3">17</span></div>
            <div class="title">
              (已被刪除) &lt;sagat666&gt;
            </div>
            <div class="meta">
              <div class="author">-</div>
              <div class="date"> 3/24</div>
            </div>
          </div>
          <div class="r-ent">
            <div class="nrec"></div>
            <div class="title">
              <a href="/bbs/Stock/M.1774322898.A.1F1.html">[討論] 台積電</a>
            </div>
            <div class="meta">
              <div class="author">ginwer</div>
              <div class="date"> 3/24</div>
            </div>
          </div>
        </div>
        </body></html>
        """;

    [Fact]
    public void ParseIndex_returns_all_valid_articles()
    {
        var articles = PttWebParser.ParseIndex(IndexHtmlWithTwoArticles);

        Assert.Equal(2, articles.Count);

        Assert.Equal("M.1774317183.A.F71", articles[0].ArticleId);
        Assert.Equal("[新聞] 美德州瓦萊羅煉油廠爆炸", articles[0].Title);
        Assert.Equal("richer23", articles[0].Author);

        Assert.Equal("M.1774318445.A.54D", articles[1].ArticleId);
    }

    [Fact]
    public void ParseIndex_skips_deleted_articles()
    {
        var articles = PttWebParser.ParseIndex(IndexHtmlWithDeletedArticle);

        Assert.Single(articles);
        Assert.Equal("M.1774322898.A.1F1", articles[0].ArticleId);
    }

    [Fact]
    public void ParseIndex_returns_empty_for_empty_html()
    {
        var articles = PttWebParser.ParseIndex("<html><body></body></html>");

        Assert.Empty(articles);
    }

    // ── Article page ─────────────────────────────────────────────────────────

    private const string ArticleHtml = """
        <html><body>
        <div id="main-content" class="bbs-screen bbs-content">
          <div class="article-metaline">
            <span class="article-meta-tag">作者</span>
            <span class="article-meta-value">richer23 (暱稱)</span>
          </div>
          <div class="article-metaline-right">
            <span class="article-meta-tag">看板</span>
            <span class="article-meta-value">Stock</span>
          </div>
          <div class="article-metaline">
            <span class="article-meta-tag">標題</span>
            <span class="article-meta-value">[新聞] 美德州爆炸</span>
          </div>
          <div class="article-metaline">
            <span class="article-meta-tag">時間</span>
            <span class="article-meta-value">Tue Mar 24 09:53:01 2026</span>
          </div>
          這是文章內文。
          <div class="push"><span class="hl push-tag">推 </span><span class="f3 hl push-userid">user1</span><span class="f3 push-content">: 推</span><span class="push-ipdatetime"> 03/24 10:00</span></div>
          <div class="push"><span class="f1 hl push-tag">→ </span><span class="f3 hl push-userid">user2</span><span class="f3 push-content">: 中立</span><span class="push-ipdatetime"> 03/24 10:01</span></div>
          <div class="push"><span class="f1 hl push-tag">噓 </span><span class="f3 hl push-userid">user3</span><span class="f3 push-content">: 噓</span><span class="push-ipdatetime"> 03/24 10:02</span></div>
          <div class="push"><span class="hl push-tag">推 </span><span class="f3 hl push-userid">user4</span><span class="f3 push-content">: 推2</span><span class="push-ipdatetime"> 03/24 10:03</span></div>
        </div>
        </body></html>
        """;

    [Fact]
    public void ParseArticle_returns_correct_metadata()
    {
        var doc = PttWebParser.ParseArticle(ArticleHtml, "M.1774317183.A.F71",
            "https://www.ptt.cc/bbs/Stock/M.1774317183.A.F71.html");

        Assert.NotNull(doc);
        Assert.Equal("M.1774317183.A.F71", doc.DocumentId);
        Assert.Equal("richer23", doc.Author);
        Assert.Equal("[新聞] 美德州爆炸", doc.Title);
        Assert.Equal(new DateTime(2026, 3, 24, 1, 53, 1, DateTimeKind.Utc), doc.PublishedAt);
    }

    [Fact]
    public void ParseArticle_counts_pushes_correctly()
    {
        var doc = PttWebParser.ParseArticle(ArticleHtml, "M.1774317183.A.F71",
            "https://www.ptt.cc/bbs/Stock/M.1774317183.A.F71.html");

        Assert.NotNull(doc);
        Assert.Equal(2, doc.PttUpvoteCount);   // 2 個推
        Assert.Equal(1, doc.PttDownvoteCount); // 1 個噓
        Assert.Equal(1, doc.PttArrowCount);    // 1 個 →
    }

    [Fact]
    public void ParseArticle_returns_null_for_missing_main_content()
    {
        var doc = PttWebParser.ParseArticle("<html><body></body></html>",
            "any-id", "https://www.ptt.cc/");

        Assert.Null(doc);
    }

    // ── Date parsing ──────────────────────────────────────────────────────────

    [Theory]
    // PTT 時間為 UTC+8，預期回傳值已轉為 UTC（-8h）
    [InlineData("Tue Mar 24 09:53:01 2026", 2026, 3, 24, 1, 53, 1)]
    [InlineData("Mon Mar  3 08:00:00 2025", 2025, 3, 3, 0, 0, 0)]   // 單位數日期（兩個空格）
    [InlineData("Fri Jan  1 08:00:00 2021", 2021, 1, 1, 0, 0, 0)]
    public void TryParseArticleDate_parses_valid_dates_and_converts_to_utc(
        string input, int y, int mo, int d, int h, int mi, int s)
    {
        var ok = PttWebParser.TryParseArticleDate(input, out var result);

        Assert.True(ok);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("2026-03-24")]
    public void TryParseArticleDate_returns_false_for_invalid_input(string input)
    {
        var ok = PttWebParser.TryParseArticleDate(input, out _);

        Assert.False(ok);
    }
}
