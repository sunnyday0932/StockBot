namespace StockBot.Infrastructure.Processing;

/// <summary>
/// Aho-Corasick 多關鍵字同時比對演算法。
/// 時間複雜度：建構 O(Σ|keywords|)，搜尋 O(|text| + |matches|)。
/// </summary>
internal sealed class AhoCorasickTrie
{
    private sealed class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = [];
        public TrieNode?                  Failure  { get; set; }
        // 合併自 failure chain 的所有命中輸出，避免 Search 時逐層回溯
        public List<(int EntityId, string Keyword)> Outputs { get; } = [];
    }

    private readonly TrieNode _root = new();
    private bool _built;

    /// <summary>
    /// 從 (keyword, entityId) 對建立 Trie 並計算 failure links。
    /// 空白或空字串 keyword 會被略過。
    /// </summary>
    public void Build(IEnumerable<(string keyword, int entityId)> keywords)
    {
        // Phase 1：插入所有 keyword
        foreach (var (kw, eid) in keywords)
        {
            if (string.IsNullOrEmpty(kw)) continue;

            var node = _root;
            foreach (var c in kw)
            {
                if (!node.Children.TryGetValue(c, out var child))
                {
                    child = new TrieNode();
                    node.Children[c] = child;
                }
                node = child;
            }
            node.Outputs.Add((eid, kw));
        }

        // Phase 2：BFS 設定 failure links，並合併 suffix 輸出
        var queue = new Queue<TrieNode>();

        foreach (var child in _root.Children.Values)
        {
            child.Failure = _root;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();

            foreach (var (c, child) in curr.Children)
            {
                // 找到 child 的 failure link
                var fail = curr.Failure;
                while (fail is not null && !fail.Children.ContainsKey(c))
                    fail = fail.Failure;

                child.Failure = fail?.Children.GetValueOrDefault(c) ?? _root;

                // 避免自指（root 的直接 child failure 應指向 root）
                if (child.Failure == child)
                    child.Failure = _root;

                // 合併 failure node 的輸出：讓 Search 不需逐層追 suffix link
                child.Outputs.AddRange(child.Failure.Outputs);

                queue.Enqueue(child);
            }
        }

        _built = true;
    }

    /// <summary>
    /// 在 text 中搜尋所有 keyword 出現位置，以 (entityId, keyword) yield 回傳。
    /// 同一位置可能命中多個 keyword（如「台積電」同時命中「台積電」與「台積」）。
    /// </summary>
    public IEnumerable<(int EntityId, string Keyword)> Search(string text)
    {
        if (!_built || string.IsNullOrEmpty(text))
            yield break;

        var curr = _root;

        foreach (var c in text)
        {
            // 沿 failure links 找到能接受字元 c 的節點
            while (curr != _root && !curr.Children.ContainsKey(c))
                curr = curr.Failure!;

            if (curr.Children.TryGetValue(c, out var next))
                curr = next;
            // 若 root 也沒有 c 的子節點，curr 維持在 root

            foreach (var output in curr.Outputs)
                yield return output;
        }
    }

    /// <summary>目前 Trie 內含的 keyword 數（用於 logging）。</summary>
    public int KeywordCount
    {
        get
        {
            var count = 0;
            var queue = new Queue<TrieNode>();
            queue.Enqueue(_root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                // 只計算 Build 時直接加入的 output，排除 failure 合併的重複部分
                // 此處簡化：直接計算 root outputs 與每個 leaf 的原始 outputs
                foreach (var child in node.Children.Values)
                    queue.Enqueue(child);
                count += node.Children.Count == 0 ? node.Outputs.Count : 0;
            }
            return count;
        }
    }
}
