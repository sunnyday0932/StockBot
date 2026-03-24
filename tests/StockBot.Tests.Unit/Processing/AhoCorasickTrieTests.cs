using StockBot.Infrastructure.Processing;

namespace StockBot.Tests.Unit.Processing;

public class AhoCorasickTrieTests
{
    private static AhoCorasickTrie Build(params (string kw, int eid)[] keywords)
    {
        var trie = new AhoCorasickTrie();
        trie.Build(keywords.Select(k => (k.kw, k.eid)));
        return trie;
    }

    // ──────────────── 基本命中 ────────────────

    [Fact]
    public void Single_keyword_found_in_text()
    {
        var trie = Build(("台積電", 1));
        var matches = trie.Search("今天台積電大漲").ToList();

        Assert.Single(matches);
        Assert.Equal(1, matches[0].EntityId);
    }

    [Fact]
    public void Multiple_keywords_found_in_text()
    {
        var trie = Build(("台積電", 1), ("聯發科", 2));
        var matches = trie.Search("台積電與聯發科都創高").ToList();

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.EntityId == 1);
        Assert.Contains(matches, m => m.EntityId == 2);
    }

    [Fact]
    public void Keyword_appearing_multiple_times_is_counted_correctly()
    {
        var trie = Build(("台積", 1));
        var matches = trie.Search("台積電台積台積").ToList();

        // "台積電" 內包含「台積」→ 共 3 次
        Assert.Equal(3, matches.Count);
        Assert.All(matches, m => Assert.Equal(1, m.EntityId));
    }

    [Fact]
    public void Overlapping_keywords_both_reported()
    {
        // 「台積電」與「台積」是前綴關係
        var trie = Build(("台積電", 1), ("台積", 1));
        var matches = trie.Search("台積電").ToList();

        // 位置 3 命中「台積電」，同時 suffix 命中「台積」
        Assert.Equal(2, matches.Count);
    }

    // ──────────────── 未命中 ────────────────

    [Fact]
    public void No_match_returns_empty()
    {
        var trie = Build(("台積電", 1));
        var matches = trie.Search("今天天氣很好").ToList();

        Assert.Empty(matches);
    }

    [Fact]
    public void Empty_text_returns_empty()
    {
        var trie = Build(("台積電", 1));
        var matches = trie.Search("").ToList();

        Assert.Empty(matches);
    }

    [Fact]
    public void Search_before_build_returns_empty()
    {
        var trie = new AhoCorasickTrie();
        var matches = trie.Search("台積電").ToList();

        Assert.Empty(matches);
    }

    // ──────────────── 邊界 ────────────────

    [Fact]
    public void Empty_keyword_is_skipped()
    {
        var trie = new AhoCorasickTrie();
        trie.Build([("", 1), ("台積電", 2)]);

        var matches = trie.Search("台積電").ToList();
        Assert.Single(matches);
        Assert.Equal(2, matches[0].EntityId);
    }

    [Fact]
    public void Keyword_at_text_boundary_is_found()
    {
        var trie = Build(("台積電", 1), ("漲停", 2));
        var matches = trie.Search("台積電漲停").ToList();

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Stock_code_numeric_keyword_found()
    {
        var trie = Build(("2330", 1), ("2454", 2));
        var matches = trie.Search("2330 今天漲，2454 也漲").ToList();

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.EntityId == 1);
        Assert.Contains(matches, m => m.EntityId == 2);
    }

    [Fact]
    public void Multiple_entities_same_keyword_all_reported()
    {
        // 同一關鍵字對應兩個 entity（不常見但需支援）
        var trie = Build(("台積", 1), ("台積", 2));
        var matches = trie.Search("台積").ToList();

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.EntityId == 1);
        Assert.Contains(matches, m => m.EntityId == 2);
    }

    [Fact]
    public void Large_keyword_set_all_found()
    {
        // 模擬 12608 筆關鍵字的壓力場景（簡化版）
        var keywords = Enumerable.Range(1, 100)
            .Select(i => ($"股票{i:D4}", i))
            .ToArray();
        var trie = Build(keywords);

        var text = string.Join("、", keywords.Select(k => k.Item1));
        var matches = trie.Search(text).ToList();

        Assert.Equal(100, matches.Count);
    }
}
