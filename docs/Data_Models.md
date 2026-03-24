# StockBot 核心資料結構定義 (Data Models)

在進入開發前，我們需要先定義清楚系統流轉的幾個核心物件（Entity）。以下以 C# Domain Model 的邏輯進行初步設計，分為「靜態字典」、「原始文本」、「中間處理結果」、「時序指標」、「訊號警報」五個區塊。

---

## 1. 靜態關聯字典 (Knowledge Dictionary)
用於「由上而下 (Top-Down)」比對的白名單（存於 PostgreSQL 中）。

```csharp
// 監控的實體 (可以是股票、概念標籤、甚至是特定人物)
public class TrackedEntity
{
    public int Id { get; set; }
    public EntityType Type { get; set; } // Enum: Stock(股票), Concept(概念), Person(人物)
    public string PrimaryName { get; set; } // 主名稱 (例: 奇鋐, 散熱, 黃仁勳)
    public string StockCode { get; set; }   // 如果是股票才有代號 (例: 3017)，Concept/Person 為 null

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

public enum EntityType
{
    Stock,    // 個股
    Concept,  // 概念/題材 (如 AI、電力、矽光子)
    Person    // 特定人物 (如 黃仁勳)
}
```

---

## 2. 來源文本 (SourceDocument)
所有爬蟲抓下來的原始資料，經過清洗後的第一手結構（存於 PostgreSQL 供追溯）。

```csharp
public class SourceDocument
{
    public string DocumentId { get; set; }    // 唯一識別碼 (如 PTT 文章代碼)
    public SourceType SourceType { get; set; } // Enum，見下方定義
    public string Author { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }       // 完整內文文本
    public DateTime PublishedAt { get; set; } // 發布時間（精準到秒）
    public string Url { get; set; }

    // PTT 專屬欄位（非 PTT 來源時為 null）
    public int? PttUpvoteCount { get; set; }   // 推文數（情緒正向訊號）
    public int? PttDownvoteCount { get; set; } // 噓文數（情緒負向訊號）
    public int? PttArrowCount { get; set; }    // 箭頭數（中立）
}

public enum SourceType
{
    PttStock,    // PTT 股板
    NewsYahoo,   // Yahoo 財經新聞
    NewsCnyes,   // 鉅亨網
    NewsUdn,     // 經濟日報
    Mops,        // 公開資訊觀測站
    Threads      // Threads 社群（進階）
}
```

---

## 3. 中間分析結果 (AnalysisResult)
`SourceDocument` 經過 TopDownMatcher 處理後產生的中間物件，承載所有命中資訊，再分流寫入 InfluxDB 與 PostgreSQL。

```csharp
public class AnalysisResult
{
    public string DocumentId { get; set; }      // 對應 SourceDocument.DocumentId
    public DateTime ProcessedAt { get; set; }   // 處理完成時間

    // TopDownMatcher 命中的實體列表
    public List<EntityMatch> MatchedEntities { get; set; }

    // LLM 計算的全文情緒分數 (區間 -1 ~ 1)
    // 若未呼叫 LLM 則為 null（依成本決策）
    public float? SentimentScore { get; set; }

    // 文本向量 Embedding（存入 pgvector，供語意搜尋用）
    // 若未計算則為 null
    public float[]? VectorEmbedding { get; set; }
}

public class EntityMatch
{
    public int EntityId { get; set; }           // 對應 TrackedEntity.Id
    public string StockCode { get; set; }       // 快取，避免多次 JOIN
    public string MatchedConcept { get; set; }  // 同時命中的概念標籤
    public int MentionCount { get; set; }       // 在這篇文章中出現幾次
}
```

---

## 4. 時序熱度指標 (Time-Series Metric)
當 `AnalysisResult` 確認命中後，轉換為量化點 (DataPoint) 直接打入 **InfluxDB**。
向量 Embedding 不在此儲存，改由 PostgreSQL + pgvector 處理。

```text
// InfluxDB Line Protocol 概念

// --- 聲量熱度 (輿情) ---
Measurement: "stock_mentions"

Tags (用於 Group By 與快速篩選的索引):
  - StockCode: "2330"
  - Source: "PttStock"
  - MatchedConcept: "AI"  // 文章同時提到的概念（可選）

Fields (實際記錄的數值):
  - MentionCount: 1         // 這篇文章提到該股幾次
  - SentimentScore: 0.85    // LLM 情緒分數，區間 -1 ~ 1（若無則不寫入）
  - PttHeatScore: 42.0      // PTT 專屬：推文數 - 噓文數（非 PTT 來源不寫入）

Timestamp: 文章精確發佈時間 (Unix nanoseconds)

// --- 股票量價行情 ---
Measurement: "stock_ohlcv"

Tags:
  - StockCode: "2330"
  - Market: "TWSE"          // TWSE 上市 / TPEX 上櫃
  - DataSource: "TwseApi"   // TwseApi / TpexApi / Fugle

Fields:
  - Open: 950.0
  - High: 965.0
  - Low: 948.0
  - Close: 960.0
  - Volume: 38500000        // 成交股數
  - TurnoverValue: 36960000000.0  // 成交金額

Timestamp: 該根 K 棒的時間（日線為當日開盤時間，盤中 tick 為精確成交時間）
```

---

## 5. 探針發現的新概念 (DiscoveredConcept)
用於「由下而上 (Bottom-Up)」路徑，記錄 LLM 分析出來的未知概念（存於 PostgreSQL）。

```csharp
public class DiscoveredConcept
{
    public int Id { get; set; }
    public string SourceDocumentId { get; set; }    // 從哪篇文章發現的
    public string AssociatedStockCode { get; set; } // 發現時，文章主要在討論哪檔股票
    public string Keyword { get; set; }             // LLM 找出的新關鍵字 (例: "玻璃基板")
    public int AppearanceCount { get; set; }        // 累計出現次數
    public DateTime FirstDiscoveredAt { get; set; } // 首次發現時間
    public DateTime LastSeenAt { get; set; }        // 最近一次出現時間（用於判斷是否仍在發酵）

    // 審核流程
    public bool IsApprovedAndPromoted { get; set; } // 預設 false
    // 使用者透過 Telegram /approve 指令審核通過後，升級成 TrackedEntity 中的 Concept
}
```

---

## 6. 警報訊號 (AlertSignal)
由 SignalAnalyzer 產生，定義推播到 Telegram 的最小資料單位。

```csharp
public class AlertSignal
{
    public Guid Id { get; set; }
    public DateTime TriggeredAt { get; set; }
    public SignalType Type { get; set; }         // Enum，見下方
    public string StockCode { get; set; }
    public string StockName { get; set; }

    // 觸發依據的量化數據快照
    public float MentionCountDelta { get; set; }  // 過去 15 分鐘聲量變化率 (%)
    public float VolumeDelta { get; set; }         // 成交量相對均量變化率 (%)
    public float PriceDelta { get; set; }          // 價格變化率 (%)
    public float? SentimentAvg { get; set; }       // 近期平均情緒分數

    public string Summary { get; set; }            // 推播給使用者的摘要說明文字
}

public enum SignalType
{
    Resonance,        // 共振：量價齊揚 + 聲量激增
    BearishDivergence, // 背離出貨：高聲量 + 股價不漲或反跌
    StealthStrength,  // 潛力抗跌：恐慌聲量 + 股價異常強勢
    SectorRotation    // 族群輪動：二線補漲股偵測
}
```
