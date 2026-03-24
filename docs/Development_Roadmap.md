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

## 階段二：資料收集（Ingestion）🔲 進行中

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

- [ ] **財經新聞爬蟲 Worker**
  - [ ] 定時爬取鉅亨網 RSS（每分鐘）
  - [ ] 正規化為 `SourceDocument`

---

## 階段三：核心比對引擎（Processing）🔲 未開始

### 任務清單

- [ ] **Aho-Corasick TopDownMatcher**
  - [ ] 從 PostgreSQL 讀取 `EntityAlias` 白名單建立 Trie
  - [ ] 對 `SourceDocument.Content` 做多關鍵字比對
  - [ ] 輸出 `AnalysisResult`

- [ ] **ResultBuilder**
  - [ ] 將 `AnalysisResult.MatchedEntities` 寫入 InfluxDB `stock_mentions`
  - [ ] 將 `VectorEmbedding` 寫入 PostgreSQL `DocumentEmbedding`（pgvector）

- [ ] **BottomUpProbe**
  - [ ] 篩選已命中白名單且熱度高的文章
  - [ ] 呼叫 LLM API（Semantic Kernel）萃取新關鍵字
  - [ ] 寫入 `DiscoveredConcept`（待審核）

---

## 階段四：訊號分析與推播（Alerting）🔲 未開始

### 任務清單

- [ ] **SignalAnalyzer**
  - [ ] 定時查詢 InfluxDB，計算過去 15 分鐘聲量變化率
  - [ ] 與行情資料交叉比對（量價 vs 聲量）
  - [ ] 實作共振（Resonance）判斷邏輯
  - [ ] 實作背離出貨（BearishDivergence）判斷邏輯
  - [ ] 產生 `AlertSignal` 物件

- [ ] **Telegram Bot**
  - [ ] 建立 Bot 並設定 Webhook
  - [ ] 格式化推播 `AlertSignal`（共振 / 背離 / 潛力股）
  - [ ] 推送 `DiscoveredConcept` 候選名詞供審核
  - [ ] 支援 `/approve` / `/reject` 指令互動

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
| PostgreSQL Image | pgvector/pgvector:pg16 |
| InfluxDB Image | influxdb:2.7 |
| xUnit | 2.9.3 |
