# StockBot 核心資料結構定義 (Data Models)

在進入開發前，我們需要先定義清楚系統流轉的幾個核心物件（Entity）。以下以 C# Domain Model 的邏輯進行初步設計，分為「靜態字典」、「原始文本」與「時序指標」。

## 1. 靜態關聯字典 (Knowledge Dictionary)
用於「由上而下 (Top-Down)」比對的白名單（存於 PostgreSQL 中）。

```csharp
// 監控的實體 (可以是股票、概念標籤、甚至是特定人物)
public class TrackedEntity
{
    public int Id { get; set; }
    public EntityType Type { get; set; } // Enum: Stock(股票), Concept(概念), Person(人物)
    public string PrimaryName { get; set; } // 主名稱 (例: 奇鋐, 散熱, 黃仁勳)
    public string StockCode { get; set; } // 如果是股票才有代號 (例: 3017)
    
    // 一對多關聯：同義詞/別名 (Aho-Corasick 演算法會用這個清單去搜尋)
    public List<EntityAlias> Aliases { get; set; } 
    // 例: TrackedEntity = "台積電" -> Aliases = ["2330", "神山", "GG"]
}

public class EntityAlias
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public string Keyword { get; set; } // 實際搜尋用的關鍵字
}
```

## 2. 來源文本 (SourceDocument)
所有爬蟲抓下來的原始資料，經過清洗後的第一手結構。

```csharp
public class SourceDocument
{
    public string DocumentId { get; set; } // 唯一識別碼 (如 PTT 文章代碼)
    public string SourceType { get; set; } // PTT_Stock, News_Yahoo, MOPS
    public string Author { get; set; }
    public string Title { get; set; }
    public string Content { get; set; } // 完整內文文本
    public DateTime PublishedAt { get; set; } // 發布時間（精準到秒）
    public string Url { get; set; }
}
```

## 3. 時序熱度指標 (Time-Series Metric)
當 `SourceDocument` 經過處理器，文章內文成功命中 `TrackedEntity` 的同義詞時，會轉換成量化點 (DataPoint) 直接打入 **InfluxDB**。

```text
// InfluxDB Line Protocol 概念
Measurement: "stock_mentions"

// Tags (用於 Group By 與快速篩選的索引)
Tags: 
  - StockCode: "2330"
  - Source: "PTT_Stock"
  - MatchedConcept: "AI" (如果文章同時提到概念)

// Fields (實際記錄的數值)
Fields:
  - MentionCount: 1 (代表這篇文章有提到該股)
  - SentimentScore: 0.85 (透過 LLM 算出的正負向情緒分數，區間 -1 ~ 1)
  - VectorEmbedding: [0.12, -0.05, ...] (文本的 Embedding，為了之後做語意分析)

// Timestamp
Time: 1678886400000000000 (文章精確發佈時間)
```

## 4. 探針發現的新概念 (DiscoveredConcept)
用於「由下而上 (Bottom-Up)」這條路徑，記錄 LLM 分析出來的未知概念。

```csharp
public class DiscoveredConcept
{
    public int Id { get; set; }
    public string SourceDocumentId { get; set; } // 從哪篇文章發現的
    public string AssociatedStockCode { get; set; } // 發現時，這篇文章主要在討論哪檔股票
    public string Keyword { get; set; } // LLM 找出的新關鍵字 (例: "玻璃基板")
    public int AppearanceCount { get; set; } // 累計熱度
    public bool IsApprovedAndPromoted { get; set; } // 預設 false，使用者人工審核通過後，會升級成 TrackedEntity 中的 Concept
}
```
