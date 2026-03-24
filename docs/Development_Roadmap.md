# StockBot 開發路線圖 (Development Roadmap)

記錄各開發階段的任務清單與完成狀態。

---

## 階段一：地基（Domain + 資料庫）✅ 已完成

### 任務清單

- [x] 建立 .NET 10 方案結構（Domain / Infrastructure / Workers）
- [x] 實作所有 Domain Entities
  - [x] `TrackedEntity` + `EntityAlias`（白名單字典）
  - [x] `SourceDocument`（爬蟲原始文本，含 PTT 推噓欄位）
  - [x] `AnalysisResult` + `EntityMatch`（TopDownMatcher 中間結果）
  - [x] `DiscoveredConcept`（BottomUpProbe 候選新概念）
  - [x] `AlertSignal`（SignalAnalyzer 推播訊號）
- [x] 實作所有 Domain Enums（`EntityType` / `SourceType` / `SignalType`）
- [x] 建立 `StockBotDbContext`（EF Core + pgvector）
- [x] 建立 `DocumentEmbedding` 資料表（pgvector 1536 維向量）
- [x] 產生 EF Core InitialCreate Migration
- [x] Docker Compose 設定（PostgreSQL 16 + pgvector / InfluxDB 2.7）
- [x] 套用 Migration 至本地 Docker PostgreSQL
- [x] 建立單元測試專案（20 個測試全通過）
- [x] 建立整合測試專案（11 個測試全通過，連接真實 Docker DB）

---

## 階段二：資料收集（Ingestion）✅ 已完成

### 任務清單

- [x] **TWSE 行情 Fetcher**（免費 REST API）
  - [x] 實作 `IPollingMarketDataFetcher` 介面隔離（Pull vs Push 分離設計）
  - [x] 呼叫 `openapi.twse.com.tw` 取得上市股票日線 OHLCV（1338 筆）
  - [x] 解析民國年日期格式（`TryParseRocDate`）、千分位數字、停牌股跳過
  - [x] 寫入 InfluxDB `stock_ohlcv` Measurement（驗證通過）
  - [x] API URL 抽至 `appsettings.json TwseApi:StockDayAllUrl` 管理
  - [x] 單元測試：8 個，含 Parse / TryParseRocDate Theory 測試

- [x] **TPEX 行情 Fetcher**
  - [x] 呼叫 `tpex.org.tw/openapi` 取得上櫃股票日線 OHLCV（4966 筆）
  - [x] 解析 `"---"` 停牌格式（與 TWSE `"--"` 不同）
  - [x] 寫入 InfluxDB `stock_ohlcv` Measurement（驗證通過）
  - [x] API URL 抽至 `appsettings.json TpexApi:DailyCloseUrl` 管理
  - [x] 單元測試：9 個

- [x] **白名單初始化**
  - [x] `WhitelistInitializerWorker`：啟動時從 TWSE + TPEX 拉取完整股票清單
  - [x] 寫入 `TrackedEntity`（Stock 類型）+ 自動建立兩個 `EntityAlias`（代號 + 名稱）
  - [x] Idempotent upsert（重複啟動安全）
  - [x] 驗證：6304 筆股票、12608 個關鍵字寫入成功

- [x] **PTT 爬蟲 Worker**
  - [x] HTTP Polling 每 60 秒拉取 PTT 股板最新文章（V1；WebSocket 版列入 V2）
  - [x] `PttWebParser`：解析 index 頁文章列表（跳過已刪除文章）
  - [x] `PttWebParser`：解析文章全文、推/噓/→ 數量、ctime 日期（UTC+8 → UTC 轉換）
  - [x] 記憶體 HashSet 去重 + 啟動時從 DB 載入近 7 天已知 ID
  - [x] 寫入 PostgreSQL `SourceDocuments`（驗證通過，6 篇含推噓數）
  - [x] 單元測試：12 個（ParseIndex / ParseArticle / TryParseArticleDate Theory）

- [x] **財經新聞爬蟲 Worker**
  - [x] 定時爬取鉅亨網 RSS（每分鐘）
  - [x] `CnyesRssParser`：解析 RSS XML，手動解析 RFC 822 timezone offset（+0800 格式）
  - [x] 正規化為 `SourceDocument`（SourceType.NewsCnyes）
  - [x] 記憶體 HashSet 去重 + 啟動時從 DB 載入近 7 天已知 ID
  - [x] 單元測試：12 個（Parse / TryParsePubDate Theory 測試）

---

## 階段 2.5：BackOffice 管理介面 ✅ 已完成

> 插入於資料收集與比對引擎之間，讓資料可被觀察與管理，避免黑盒子操作。

### 技術選型

| 項目 | 選擇 |
|------|------|
| 框架 | ASP.NET Core + Blazor Web App (`--interactivity Server`) |
| 連線埠 | 5001（獨立於 Workers） |
| 資料存取 | 直接注入 `StockBotDbContext`（無 Application layer，Stage 3 前可接受） |
| 樣式 | Bootstrap 5.3（CDN） |
| 已知陷阱 | Blazor Server 的 DbContext 生命週期為 SignalR circuit，EF Core relationship fixup 會自動更新導覽屬性，勿手動再 `.Add()` / `.Remove()` |

### 任務清單

- [x] **專案建立**
  - [x] 新增 `StockBot.BackOffice` Blazor Server 專案（`dotnet new blazor --interactivity Server`）
  - [x] 加入 Solution，引用 `StockBot.Infrastructure` / `StockBot.Domain`
  - [x] 設定 `appsettings.json`（共用 PostgreSQL 連線字串）
  - [x] 加入 Solution 至 `StockBot.slnx`
  - [x] 設定 port 5001，Bootstrap CDN

- [x] **白名單管理（Whitelist）**
  - [x] `WhitelistIndex` 頁：搜尋 / 分頁瀏覽 `TrackedEntity`（by 代號 / 名稱）
  - [x] `WhitelistIndex` 頁：新增股票（代號 + 名稱，自動建兩個 Alias）/ Concept / Person
  - [x] `WhitelistDetail` 頁：查看 / 新增 / 刪除個別股票的 `EntityAlias`
  - [x] 支援新增非股票類型（`Concept` / `Person`）

- [x] **PTT 文章瀏覽（Articles）**
  - [x] `ArticleIndex` 頁：分頁列表，欄位：標題、作者、時間、推/噓/→ 數
  - [x] `ArticleIndex` 頁：篩選（日期範圍、標題關鍵字）與排序（最新 / 推文數）
  - [x] `ArticleDetail` 頁：文章全文、完整 Metadata

---

## 階段三：核心比對引擎（Processing）✅ 已完成

### 任務清單

- [x] **Aho-Corasick TopDownMatcher**
  - [x] `AhoCorasickTrie`：純演算法，O(|text| + |matches|) 搜尋複雜度
  - [x] 從 PostgreSQL 讀取所有 `EntityAlias`（12608 個關鍵字）建立 Trie
  - [x] 對 `SourceDocument.Title + Content` 做多關鍵字比對
  - [x] 輸出 `AnalysisResult`（含每個 EntityMatch 的 MentionCount）
  - [x] `SourceDocument.ProcessedAt`（nullable）追蹤處理狀態 + EF migration
  - [x] 單元測試：12 個（命中 / 未命中 / 邊界 / 壓力場景）

- [x] **ResultBuilder（內嵌於 ProcessingWorker）**
  - [x] `ProcessingWorker`：每 30 秒批次（50 篇）掃描 `ProcessedAt IS NULL` 文件
  - [x] 將 `MatchedEntities` 的 MentionCount 寫入 InfluxDB `stock_mentions`
  - [x] 寫完 InfluxDB 後 commit `ProcessedAt` 至 PostgreSQL

- [x] **VectorEmbedding（ResultBuilder 進階）**
  - [x] `IEmbeddingService` 介面 + `StubEmbeddingService`（零向量，TODO: 替換為 OpenAI/Azure）
  - [x] 命中文章才計算 Embedding（節省 API 費用）
  - [x] 寫入 PostgreSQL `DocumentEmbedding`（pgvector）

- [x] **BottomUpProbe**
  - [x] `ILlmConceptExtractor` 介面 + `StubLlmConceptExtractor`（空回傳，TODO: 替換為 Semantic Kernel）
  - [x] `BottomUpProbeWorker`：每 2 分鐘掃一次熱門文章（PTT 推文 ≥ 10 或非 PTT）
  - [x] 寫入 `DiscoveredConcept`（upsert：重複 keyword 更新 AppearanceCount）
  - [x] `SourceDocument.EntityMatchCount` + `ProbedAt` + EF migration

  > **TODO（填洞）**：
  > - `StubEmbeddingService` → 替換為 `OpenAI text-embedding-3-small`
  > - `StubLlmConceptExtractor` → 替換為 `Semantic Kernel + GPT-4o-mini`
  > - 只需在 `Program.cs` 改 `AddSingleton<IEmbeddingService, ...>` 與 `AddSingleton<ILlmConceptExtractor, ...>`

---

## 階段四：訊號分析與推播（Alerting）✅ 已完成

### 任務清單

- [x] **SignalAnalyzer**
  - [x] `IInfluxDbReader` 介面 + `InfluxDbReader` 實作（Flux 查詢 stock_mentions / stock_ohlcv）
  - [x] `ISignalAnalyzer` 介面 + `SignalAnalyzer` 實作
  - [x] 定時查詢 InfluxDB，計算當前窗口 vs 前一窗口聲量變化率
  - [x] 與行情資料交叉比對（量比 vs 聲量 vs 股價）
  - [x] 共振（Resonance）：聲量 Δ>50% AND 量比 Δ>50% AND 股價 Δ>2%
  - [x] 背離出貨（BearishDivergence）：聲量 Δ>50% AND 股價 Δ<-1%
  - [x] `SignalAnalyzerWorker`：每 15 分鐘執行，觸發時呼叫 `ITelegramNotifier`
  - [x] `SignalAnalyzerOptions`：所有閾值可由 `appsettings.json` 設定

- [x] **Telegram Bot**
  - [x] `ITelegramNotifier` 介面 + `TelegramNotifier` 實作（Telegram.Bot v22.9.5.3）
  - [x] 格式化推播 `AlertSignal`（含 emoji、聲量/量比/股價數字）
  - [x] 推送 `DiscoveredConcept` 候選名詞供審核（每 30 分鐘，最多 5 筆）
  - [x] `TelegramBotWorker`：Long-polling 接收 `/approve ID` / `/reject ID`
  - [x] `/approve`：升級為 `TrackedEntity`（Concept 類型）+ 新增 `EntityAlias`
  - [x] `/reject`：從 DB 刪除 `DiscoveredConcept`
  - [x] `TelegramOptions`：BotToken / ChatId / PollingTimeoutSeconds

  > **TODO（填洞）**：
  > - `appsettings.json` 中 `Telegram:BotToken` 填入真實 Bot Token（由 @BotFather 取得）
  > - `Telegram:ChatId` 填入推播目標頻道 / 群組 ID

---

## 進階功能（V2）🔲 規劃中

- [ ] 升級至 Fugle WebSocket 實現盤中即時行情
- [ ] 新增 Threads 社群爬蟲
- [ ] 族群輪動自動推薦（SectorRotation 訊號）
- [ ] 跨平台資訊時間差分析（新聞 → PTT → 社群）
- [ ] Grafana Dashboard（連接 InfluxDB 視覺化）

---

## 環境依賴版本

| 依賴 | 版本 |
|------|------|
| .NET SDK | 10.0.103 |
| EF Core | 10.0.5 |
| Npgsql.EF | 10.0.1 |
| Pgvector.EF | 0.3.0 |
| InfluxDB.Client | 5.0.0 |
| HtmlAgilityPack | 1.12.4 |
| Bootstrap | 5.3（CDN） |
| PostgreSQL Image | pgvector/pgvector:pg16 |
| InfluxDB Image | influxdb:2.7 |
| xUnit | 2.9.3 |
