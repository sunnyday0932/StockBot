# StockBot 系統架構圖 (System Architecture)

這份文件定義了 StockBot 各個元件的職責與資料流向 (Data Flow)。系統採模組化與微服務的概念設計，分為四個主要端點。

## 系統流向圖

```mermaid
graph TD
    %% Data Sources
    subgraph DataSource [1. 資料來源]
        PTT[PTT 股板]
        News[Yahoo/鉅亨網新聞]
        MOPS[公開資訊觀測站]
        MarketAPI[行情 API<br/>TWSE / TPEX / Fugle]
    end

    %% Ingestion Layer
    subgraph IngestionLayer [2. 資料收集層 (Ingestion)]
        CrawlerWorker[排程爬蟲 Worker<br/>新聞 / MOPS]
        WSCrawler[即時 WebSocket 監聽<br/>PTT]
        MarketFetcher[行情定時拉取 Worker<br/>OHLCV / Tick]
        WhitelistInit[白名單初始化 Worker<br/>TWSE+TPEX → TrackedEntity]
    end

    %% BackOffice
    subgraph BackOffice [2.5 管理介面 (BackOffice)]
        BOWhitelist[白名單管理<br/>新增 / 編輯 EntityAlias]
        BOArticles[PTT 文章瀏覽<br/>搜尋 / 篩選 / 全文]
    end

    %% Processing Layer
    subgraph ProcessingLayer [3. 處理與關聯層 (Processing)]
        Normalizer[格式正則化 轉為 SourceDocument]
        TopDownMatcher[由上而下：白名單字典比對<br/>Aho-Corasick 高效演算法]
        BottomUpProbe[由下而上：潛力概念探針<br/>只針對命中白名單的熱門文章<br/>LLM / 語意萃取 API]
        ResultBuilder[分析結果組裝 AnalysisResult]
    end

    %% Storage Layer
    subgraph StorageLayer [4. 儲存與量化層 (Storage)]
        InfluxDB[(InfluxDB 時序資料庫<br/>聲量熱度 / 情緒分 / 行情 OHLCV)]
        LocalDB[(PostgreSQL 關聯資料庫<br/>靜態字典 / 新發現詞彙<br/>+ pgvector 文本 Embedding)]
    end

    %% Alerting Layer
    subgraph AlertingLayer [5. 警報輸出層 (Alerting)]
        SignalAnalyzer[量價背離決策引擎<br/>產生 AlertSignal]
        TG_Bot[Telegram 推播服務<br/>共振 / 背離 / 新概念審核]
    end

    %% 關聯線
    PTT --> WSCrawler
    News --> CrawlerWorker
    MOPS --> CrawlerWorker
    MarketAPI --> MarketFetcher
    MarketAPI --> WhitelistInit

    CrawlerWorker --> Normalizer
    WSCrawler --> Normalizer
    MarketFetcher --> InfluxDB
    WhitelistInit --> LocalDB

    Normalizer --> TopDownMatcher

    TopDownMatcher -- 命中白名單 --> ResultBuilder
    TopDownMatcher -- 命中白名單的熱門文章 --> BottomUpProbe

    BottomUpProbe -- 發現高度關聯新字詞 --> LocalDB

    ResultBuilder -- MentionCount / SentimentScore --> InfluxDB
    ResultBuilder -- VectorEmbedding --> LocalDB

    LocalDB -.->|提供已知白名單| TopDownMatcher

    InfluxDB -- 定期 Query 熱度+行情 --> SignalAnalyzer
    SignalAnalyzer -- 觸發條件 共振/背離 --> TG_Bot
    LocalDB -.->|候選新概念推送| TG_Bot

    %% BackOffice 雙向讀寫
    BOWhitelist <-->|CRUD| LocalDB
    BOArticles -->|讀取| LocalDB
```

## 各層職責說明

### 1. 資料收集層 (Ingestion)
負責與外界互動，將非結構化的網頁或 API 回傳資料抓取下來。
*   對於如 PTT 這類更新極快的論壇，優先考慮使用 WebSocket 即時監聽。
*   對於新聞網站，則採用定時排程 (如每分鐘) 爬取 RSS 或 HTML。
*   **行情資料 (MarketFetcher)**：定時呼叫 TWSE/TPEX 官方 OpenAPI 或 Fugle API，拉取 OHLCV 日線或盤中 tick 資料，直接寫入 InfluxDB。V1 使用免費的 TWSE/TPEX API (延遲 5 分鐘)，進階版升級至 Fugle WebSocket 實現即時行情。
*   **白名單初始化 (WhitelistInit)**：啟動時從 TWSE/TPEX 拉取完整股票清單，idempotent upsert 至 `TrackedEntity` + `EntityAlias`（6304 筆 × 2 = 12608 個關鍵字）。

### 2.5 管理介面 (BackOffice)
Blazor Server 管理介面（port 5001），提供人工干預的入口，讓系統不再是黑盒子：
*   **白名單管理**：搜尋 / 新增 / 編輯 `TrackedEntity` 及其 `EntityAlias`，支援手動加入縮寫、同義詞等 Aho-Corasick 比對用關鍵字。
*   **PTT 文章瀏覽**：查看已爬取的 `SourceDocument`，支援篩選、排序與全文檢視，確認爬蟲資料品質。
*   直接讀寫 `StockBotDbContext`（Stage 3 加入 Application layer 後改為 interface）。

### 2. 處理與關聯層 (Processing)
整個大腦的核心。所有的 `SourceDocument` 都會流經這裡。
*   **主流路徑 (TopDownMatcher)**：利用高效的字串多重比對演算法，瞬間標記該文章提到了哪些股票與概念。
*   **探針路徑 (BottomUpProbe)**：為了節省 API 費用與算力，**只挑選已命中白名單的熱門文章**，丟給 LLM 進行深度文本萃取，找出潛在的「新題材關鍵字」。注意：探針在 TopDownMatcher 之後觸發，而非在 Normalizer 之後。
*   **結果組裝 (ResultBuilder)**：將 TopDownMatcher 的命中結果轉為 `AnalysisResult`，包含 MentionCount、SentimentScore、VectorEmbedding，分別打入對應的儲存層。

### 3. 儲存與量化層 (Storage)
分為「時序型資料庫」與「關聯型資料庫」，各司其職：
*   **InfluxDB**：負責消化龐大的資料流，儲存「聲量熱度」、「情緒分數」與「行情 OHLCV」等時序指標。每個命中事件與每筆行情資料都成為 InfluxDB 的一個時間切片點 (Data Point)，供 SignalAnalyzer 查詢。
*   **PostgreSQL + pgvector**：用來儲存相對靜態的股票清單、概念名詞、`DiscoveredConcept` 候選詞彙，以及文本的 **VectorEmbedding**（透過 pgvector 延伸套件實現向量相似度搜尋）。向量資料不應存入 InfluxDB。

### 4. 警報輸出層 (Alerting)
*   **SignalAnalyzer**：定時監控 InfluxDB 的數據變化率，同時結合行情資料做交叉比對。當某檔股票在過去 15 分鐘的累積 mention count 突破均線，且成交量同步放大，判斷為「共振」；若聲量激增但股價反跌，判斷為「背離出貨」。觸發後產生 `AlertSignal` 物件。
*   **TG_Bot**：接收 `AlertSignal` 並格式化推播至 Telegram 頻道。另外也負責將 `DiscoveredConcept` 候選名詞推送給使用者審核（支援 `/approve` / `/reject` 指令互動）。
