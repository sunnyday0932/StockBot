using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;

namespace StockBot.Infrastructure.News;

internal static partial class CnyesRssParser
{
    [GeneratedRegex(@"/id/(\d+)")]
    private static partial Regex IdFromUrlRegex();

    /// <summary>
    /// 解析鉅亨網 RSS XML，回傳尚未去重的 SourceDocument 列表。
    /// </summary>
    internal static List<SourceDocument> Parse(string xml)
    {
        var xdoc    = XDocument.Parse(xml);
        var results = new List<SourceDocument>();

        foreach (var item in xdoc.Descendants("item"))
        {
            var title      = item.Element("title")?.Value.Trim();
            var link       = item.Element("link")?.Value.Trim();
            var description = item.Element("description")?.Value ?? "";
            var pubDateRaw  = item.Element("pubDate")?.Value;
            var guid        = item.Element("guid")?.Value.Trim();

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link)) continue;
            if (!TryParsePubDate(pubDateRaw, out var publishedAt)) continue;

            var documentId = BuildDocumentId(guid ?? link);
            if (string.IsNullOrEmpty(documentId)) continue;

            results.Add(new SourceDocument
            {
                DocumentId  = documentId,
                SourceType  = SourceType.NewsCnyes,
                Title       = title,
                Content     = StripHtml(description),
                Url         = link,
                PublishedAt = publishedAt,
            });
        }

        return results;
    }

    /// <summary>
    /// 解析 RFC 822 格式日期（鉅亨網 RSS pubDate），轉為 UTC DateTime。
    /// 手動解析 timezone offset（+0800 格式，無冒號），避免 DateTimeOffset zzz 格式的相容性問題。
    /// </summary>
    internal static bool TryParsePubDate(string? raw, out DateTime result)
    {
        result = default;
        if (string.IsNullOrEmpty(raw)) return false;

        var s = raw.Trim();

        // 去除可選的星期前綴："Mon, " / "Fri, " 等
        var commaIdx = s.IndexOf(',');
        if (commaIdx is >= 2 and <= 4)
            s = s[(commaIdx + 1)..].TrimStart();

        // 分離日期部分與 timezone offset
        // 格式："24 Mar 2026 10:00:00 +0800"
        var lastSpace = s.LastIndexOf(' ');
        if (lastSpace < 0) return false;

        var tzStr   = s[(lastSpace + 1)..]; // "+0800"
        var datePart = s[..lastSpace];       // "24 Mar 2026 10:00:00"

        // 解析 timezone offset（+HHMM 或 -HHMM）
        if (tzStr.Length != 5) return false;
        var sign = tzStr[0];
        if (sign is not '+' and not '-') return false;
        if (!int.TryParse(tzStr[1..3], out var tzHours))   return false;
        if (!int.TryParse(tzStr[3..5], out var tzMinutes)) return false;
        var offset = TimeSpan.FromHours(tzHours) + TimeSpan.FromMinutes(tzMinutes);
        if (sign == '-') offset = -offset;

        // 解析日期時間部分
        string[] dateFormats = ["dd MMM yyyy HH:mm:ss", "d MMM yyyy HH:mm:ss"];
        if (!DateTime.TryParseExact(datePart, dateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return false;

        result = new DateTimeOffset(dt, offset).UtcDateTime;
        return true;
    }

    /// <summary>
    /// 從 URL 中取出數字 ID 組成穩定的 DocumentId；無法解析時退回完整 URL。
    /// </summary>
    private static string BuildDocumentId(string url)
    {
        var m = IdFromUrlRegex().Match(url);
        return m.Success ? $"cnyes_{m.Groups[1].Value}" : url;
    }

    /// <summary>用 HtmlAgilityPack 去除 description 中的 HTML tag。</summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.InnerText.Trim();
    }
}
