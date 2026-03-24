using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;

namespace StockBot.Infrastructure.Ptt;

internal static partial class PttWebParser
{
    /// <summary>
    /// 解析 PTT 看板索引頁 HTML，回傳文章摘要清單。
    /// 已刪除文章（無 &lt;a&gt; 連結）會自動跳過。
    /// </summary>
    internal static IReadOnlyList<PttIndexArticle> ParseIndex(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var result = new List<PttIndexArticle>();

        foreach (var ent in doc.DocumentNode.SelectNodes("//div[@class='r-ent']")?.Cast<HtmlNode>() ?? [])
        {
            // 已刪除文章沒有 <a> 連結
            var anchor = ent.SelectSingleNode(".//div[@class='title']/a");
            if (anchor is null) continue;

            var href     = anchor.GetAttributeValue("href", "");
            var title    = HtmlEntity.DeEntitize(anchor.InnerText.Trim());
            var author   = ent.SelectSingleNode(".//div[@class='author']")?.InnerText.Trim() ?? "";

            // href 格式：/bbs/Stock/M.1234567890.A.F71.html
            var articleId = Path.GetFileNameWithoutExtension(href);
            if (string.IsNullOrEmpty(articleId)) continue;

            result.Add(new PttIndexArticle(articleId, title, author, href));
        }

        return result;
    }

    /// <summary>
    /// 解析 PTT 文章頁 HTML，回傳 SourceDocument。
    /// 失敗（無 main-content 或無法取得時間）回傳 null。
    /// </summary>
    internal static SourceDocument? ParseArticle(string html, string articleId, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var mainContent = doc.DocumentNode.SelectSingleNode("//div[@id='main-content']");
        if (mainContent is null) return null;

        // --- metadata ---
        // article-metaline 順序：作者、（看板，class=article-metaline-right）、標題、時間
        string author    = "";
        string title     = "";
        string dateStr   = "";

        foreach (var meta in mainContent.SelectNodes(".//div[contains(@class,'article-metaline')]")?.Cast<HtmlNode>() ?? [])
        {
            var tag   = meta.SelectSingleNode(".//span[@class='article-meta-tag']")?.InnerText.Trim();
            var value = meta.SelectSingleNode(".//span[@class='article-meta-value']")?.InnerText.Trim() ?? "";

            switch (tag)
            {
                case "作者": author  = value.Split(' ')[0]; break; // "username (nickname)" → 取 username
                case "標題": title   = value; break;
                case "時間": dateStr = value; break;
            }
        }

        if (!TryParseArticleDate(dateStr, out var publishedAt))
            return null;

        // --- push 統計 ---
        int upvote = 0, downvote = 0, arrow = 0;

        foreach (var push in mainContent.SelectNodes(".//div[@class='push']")?.Cast<HtmlNode>() ?? [])
        {
            var tag = push.SelectSingleNode(".//span[contains(@class,'push-tag')]")?.InnerText.Trim() ?? "";
            if (tag.StartsWith("推"))      upvote++;
            else if (tag.StartsWith("噓")) downvote++;
            else if (tag.StartsWith("→"))  arrow++;
        }

        // --- 文章內文（移除 metaline + push 節點後取文字）---
        var cloned = mainContent.CloneNode(true);
        foreach (var node in cloned.SelectNodes(
            ".//*[contains(@class,'article-metaline') or @class='push']")?.Cast<HtmlNode>() ?? [])
        {
            node.Remove();
        }
        var content = HtmlEntity.DeEntitize(cloned.InnerText).Trim();

        return new SourceDocument
        {
            DocumentId       = articleId,
            SourceType       = SourceType.PttStock,
            Author           = author,
            Title            = title,
            Content          = content,
            PublishedAt      = publishedAt,
            Url              = url,
            PttUpvoteCount   = upvote,
            PttDownvoteCount = downvote,
            PttArrowCount    = arrow,
        };
    }

    /// <summary>
    /// 解析 PTT ctime 格式日期字串。
    /// 例："Tue Mar 24 09:53:01 2026"、"Tue Mar  4 09:53:01 2026"（單位數日期有兩個空格）
    /// </summary>
    internal static bool TryParseArticleDate(string dateStr, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(dateStr)) return false;

        // 將連續空白壓縮成單一空格，讓 "Mar  4" → "Mar 4"
        var normalized = MultipleSpaces().Replace(dateStr.Trim(), " ");

        // 解析為台灣時間（UTC+8），再轉為 UTC 儲存
        if (!DateTime.TryParseExact(
                normalized,
                "ddd MMM d HH:mm:ss yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
            return false;

        result = new DateTimeOffset(local, TimeSpan.FromHours(8)).UtcDateTime;
        return true;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();
}
