# StockBot：台股輿情熱度雷達

針對台灣股市的即時輿情監控系統，透過捕捉社群與新聞討論熱度，結合市場量價變化，發掘潛在交易機會與出貨陷阱。

---

## 核心功能

| 功能 | 說明 |
|------|------|
| **族群輪動偵測** | 監控 AI、電力、矽光子等主流題材資金流向，提示二線補漲股 |
| **共振訊號** | 量價齊揚 + 聲量激增 → 判定為真突破 |
| **背離出貨警示** | 高聲量 + 股價不漲或反跌 → 標記為危險區 |
| **潛力抗跌偵測** | 恐慌聲量 + 股價異常強勢 → 發掘主力控盤股 |
| **黑馬股發掘** | 監控「冷門股 + 熱門新科技關鍵字」異常配對 |
| **新概念探針** | LLM 萃取未知新名詞，推送 Telegram 讓使用者人工審核 |

---

## 系統架構

```
資料來源                  收集層                   處理層
─────────                ──────                   ──────
PTT 股板        →   WebSocket 監聽   →   Normalizer (→ SourceDocument)
財經新聞         →   排程爬蟲 Worker  →   TopDownMatcher (Aho-Corasick)
MOPS           →   排程爬蟲 Worker       ├─ 命中白名單 → ResultBuilder
行情 API        →   行情定時拉取              └─ 熱門文章 → BottomUpProbe (LLM)
(TWSE/TPEX)

儲存層                              警報層
──────                              ──────
InfluxDB  ← 聲量熱度 / 行情 OHLCV   → SignalAnalyzer → Telegram Bot
PostgreSQL ← 靜態字典 / DiscoveredConcept / VectorEmbedding (pgvector)
```

詳細架構說明見 [docs/System_Architecture.md](docs/System_Architecture.md)。

---

## 專案結構

```
StockBot/
├── docs/                              # 專案文件
│   ├── Project_Direction.md           # 需求分析、資料來源評估、技術藍圖
│   ├── System_Architecture.md         # 系統架構圖與各層職責說明
│   ├── Data_Models.md                 # 核心資料結構定義 (C# Domain Models)
│   └── Development_Roadmap.md         # 開發階段規劃與進度追蹤
│
├── src/
│   ├── StockBot.Domain/               # Domain 層：實體、枚舉，不依賴任何框架
│   │   ├── Entities/
│   │   │   ├── TrackedEntity.cs       # 監控實體（個股/概念/人物）+ 同義詞白名單
│   │   │   ├── EntityAlias.cs         # TrackedEntity 的別名/同義詞
│   │   │   ├── SourceDocument.cs      # 爬蟲原始文本（含 PTT 推噓欄位）
│   │   │   ├── AnalysisResult.cs      # TopDownMatcher 處理後的中間結果
│   │   │   ├── EntityMatch.cs         # 單一命中紀錄（EntityId + MentionCount）
│   │   │   ├── DiscoveredConcept.cs   # BottomUpProbe 發現的候選新概念
│   │   │   └── AlertSignal.cs         # SignalAnalyzer 產生的推播警報
│   │   └── Enums/
│   │       ├── EntityType.cs          # Stock / Concept / Person
│   │       ├── SourceType.cs          # PttStock / NewsYahoo / NewsCnyes / ...
│   │       └── SignalType.cs          # Resonance / BearishDivergence / ...
│   │
│   ├── StockBot.Infrastructure/       # Infrastructure 層：DB、外部 API
│   │   ├── MarketData/
│   │   │   ├── IPollingMarketDataFetcher.cs  # REST Pull 型來源的統一介面
│   │   │   ├── StockOhlcvRecord.cs           # 跨來源統一的 OHLCV record
│   │   │   ├── TwseMarketFetcher.cs          # TWSE OpenAPI OHLCV 拉取（1338 筆上市）
│   │   │   ├── TwseMarketFetcherOptions.cs   # TWSE API URL（appsettings 注入）
│   │   │   ├── TwseStockDailyDto.cs          # TWSE JSON 反序列化 DTO
│   │   │   ├── TpexMarketFetcher.cs          # TPEX OpenAPI OHLCV 拉取（4966 筆上櫃）
│   │   │   ├── TpexMarketFetcherOptions.cs   # TPEX API URL（appsettings 注入）
│   │   │   └── TpexStockDailyDto.cs          # TPEX JSON 反序列化 DTO
│   │   ├── InfluxDb/
│   │   │   ├── IInfluxDbWriter.cs     # InfluxDB 寫入介面
│   │   │   ├── InfluxDbWriter.cs      # 實作：stock_ohlcv Measurement 寫入
│   │   │   └── InfluxDbOptions.cs     # InfluxDB 連線設定
│   │   └── Persistence/
│   │       ├── StockBotDbContext.cs   # EF Core DbContext + pgvector 設定
│   │       └── Migrations/            # EF Core 自動產生的 PostgreSQL Migrations
│   │
│   └── StockBot.Workers/              # 主程式：Worker Service 進入點
│       ├── Workers/
│       │   └── MarketDataWorker.cs    # 定時拉取行情並寫入 InfluxDB
│       ├── Program.cs                 # DI 註冊、Host 設定
│       └── appsettings.json           # 連線字串、InfluxDB / TWSE API 設定
│
├── tests/
│   ├── StockBot.Tests.Unit/           # 單元測試：純邏輯，不需 DB / HTTP
│   │   ├── Entities/
│   │   │   ├── TrackedEntityTests.cs
│   │   │   ├── SourceDocumentTests.cs
│   │   │   ├── AlertSignalTests.cs
│   │   │   └── DiscoveredConceptTests.cs
│   │   └── MarketData/
│   │       └── TwseMarketFetcherTests.cs  # Parse / TryParseRocDate 單元測試
│   │
│   └── StockBot.Tests.Integration/    # 整合測試：連接真實 Docker PostgreSQL
│       ├── Fixtures/
│       │   └── DatabaseFixture.cs     # 共用 DB Fixture，每次測試後清資料
│       ├── TrackedEntityIntegrationTests.cs
│       ├── SourceDocumentIntegrationTests.cs
│       ├── PgvectorIntegrationTests.cs
│       └── DiscoveredConceptIntegrationTests.cs
│
├── docker-compose.yml                 # 本地開發環境：PostgreSQL + InfluxDB
└── StockBot.slnx                      # .NET 10 Solution 檔
```

---

## 技術棧

| 分類 | 技術 |
|------|------|
| 語言 / 框架 | C# 13 / .NET 10 Worker Service |
| 關聯式資料庫 | PostgreSQL 16 + pgvector 0.8.2 |
| 時序資料庫 | InfluxDB 2.7 |
| ORM | Entity Framework Core 10 + Npgsql |
| 字串比對 | Aho-Corasick（待實作） |
| NLP / LLM | Semantic Kernel + LLM API（待實作） |
| 推播 | Telegram Bot API（待實作） |
| 測試 | xUnit 2.9 |
| 容器 | Docker Compose |

---

## 本地開發環境設置

### 前置需求
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 啟動資料庫

```bash
docker compose up -d
```

啟動後會有：
- **PostgreSQL**：`localhost:5432`，Database `stockbot`，含 pgvector extension
- **InfluxDB**：`localhost:8086`，Web UI 可直接開啟

### 套用資料庫 Migration

```bash
dotnet ef database update \
  --project src/StockBot.Infrastructure \
  --startup-project src/StockBot.Workers
```

### 執行應用程式

```bash
dotnet run --project src/StockBot.Workers
```

---

## 連線設定

`src/StockBot.Workers/appsettings.json` 預設本地開發值：

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=stockbot;Username=stockbot;Password=stockbot_pass"
  },
  "InfluxDb": {
    "Url": "http://localhost:8086",
    "Token": "stockbot-super-secret-token",
    "Org": "stockbot-org",
    "Bucket": "stockbot"
  },
  "MarketData": {
    "FetchIntervalMinutes": 5
  },
  "TwseApi": {
    "StockDayAllUrl": "https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL"
  },
  "TpexApi": {
    "DailyCloseUrl": "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes"
  }
}
```

> 正式環境請使用環境變數或 Secret Manager 覆寫這些值。

---

## 測試

### 執行所有測試

```bash
dotnet test StockBot.slnx
```

> 整合測試需要 Docker 容器在執行中（`docker compose up -d`）。

### 測試覆蓋範圍

#### 單元測試（39 個，`StockBot.Tests.Unit`）

| 測試類別 | 涵蓋的 Use Case |
|----------|----------------|
| `TrackedEntityTests` | Stock 有 StockCode、Concept 的 StockCode 為 null、Aliases 初始為空、多個 Alias 正確儲存 |
| `SourceDocumentTests` | PTT 文章可帶推/噓/箭頭數、非 PTT 文章這些欄位為 null、六種 SourceType 皆合法 |
| `AlertSignalTests` | 每次建立 AlertSignal 自動產生不重複 Guid、四種 SignalType 皆可賦值、SentimentAvg 允許為 null |
| `DiscoveredConceptTests` | 新概念預設為未審核狀態、FirstDiscoveredAt 與 LastSeenAt 可獨立更新 |
| `TwseMarketFetcherTests` | 解析有效 JSON、跳過停牌股（`--`）、空陣列、千分位數字、TryParseRocDate Theory（民國年轉西元年）|
| `TpexMarketFetcherTests` | 解析有效 JSON、跳過停牌股（`---`）、空陣列、TryParseRocDate Theory |

#### 整合測試（11 個，`StockBot.Tests.Integration`）

| 測試類別 | 涵蓋的 Use Case |
|----------|----------------|
| `TrackedEntityIntegrationTests` | 儲存個股實體含 Alias 並讀回、儲存 Concept 無 StockCode、刪除實體時 Alias 級聯刪除 |
| `SourceDocumentIntegrationTests` | 儲存 PTT 文章含推噓數並讀回、新聞文章推噓欄位確實為 null、依 SourceType 查詢篩選 |
| `PgvectorIntegrationTests` | 寫入 1536 維向量 Embedding 並讀回完整維度、SentimentScore 允許為 null |
| `DiscoveredConceptIntegrationTests` | 儲存候選概念含時間戳記、查詢所有待審核概念、將概念標記為已審核 |

---

## 文件索引

| 文件 | 說明 |
|------|------|
| [docs/Project_Direction.md](docs/Project_Direction.md) | 核心目標、四大分析維度、資料來源評估（含行情 API 比較表）、技術藍圖 |
| [docs/System_Architecture.md](docs/System_Architecture.md) | 完整 Mermaid 架構圖、各層元件職責說明 |
| [docs/Data_Models.md](docs/Data_Models.md) | 所有 C# Domain Model 定義、InfluxDB Line Protocol Schema |
| [docs/Development_Roadmap.md](docs/Development_Roadmap.md) | 開發階段規劃、各階段任務清單與完成狀態 |
