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
    end

    %% Ingestion Layer
    subgraph IngestionLayer [2. 資料收集層 (Ingestion)]
        CrawlerWorker[排程爬蟲 Worker]
        WSCrawler[即時 WebSocket 監聽]
    end

    %% Processing Layer
    subgraph ProcessingLayer [3. 處理與關聯層 (Processing)]
        Normalizer[格式正則化 (轉為 SourceDocument)]
        TopDownMatcher[📌 由上而下：白名單字典比對<br/>(Aho-Corasick 高效演算法)]
        BottomUpProbe[🚀 由下而上：潛力概念探針<br/>(LLM / 語意萃取 API)]
    end

    %% Storage Layer
    subgraph StorageLayer [4. 儲存與量化層 (Storage)]
        InfluxDB[(InfluxDB 時序/向量資料庫)<br/>儲存時序熱度與特徵]
        LocalDB[(PostgreSQL 關聯資料庫)<br/>儲存字典與新發現詞彙]
    end

    %% Alerting Layer
    subgraph AlertingLayer [5. 警報輸出層 (Alerting)]
        SignalAnalyzer[量價背離決策引擎]
        TG_Bot[Telegram 推播服務]
    end

    %% 關聯線
    PTT --> WSCrawler
    News --> CrawlerWorker
    MOPS --> CrawlerWorker

    CrawlerWorker --> Normalizer
    WSCrawler --> Normalizer

    Normalizer --> TopDownMatcher
    Normalizer -.->|隨機抽樣熱門文章| BottomUpProbe

    TopDownMatcher -- 1. 命中白名單 (Stock/Concept) --> InfluxDB
    BottomUpProbe -- 2. 發現高度關聯新字詞 --> LocalDB
    LocalDB -.->|提供已知白名單| TopDownMatcher

    InfluxDB -- 定期 Query 熱度/情緒分 --> SignalAnalyzer
    SignalAnalyzer -- 觸發條件 (共振/背離) --> TG_Bot
```

## 各層職責說明

### 1. 資料收集層 (Ingestion)
負責與外界互動，將非結構化的網頁或 API 回傳資料抓取下來。
*   對於如 PTT 這類更新極快的論壇，優先考慮使用 WebSocket 即時監聽。
*   對於新聞網站，則採用定時排程 (如每分鐘) 爬取 RSS 或 HTML。

### 2. 處理與關聯層 (Processing)
整個大腦的核心。所有的 `SourceDocument` 都會流經這裡。
*   **主流路徑 (TopDownMatcher)**：利用高效的字串多重比對演算法，瞬間標記該文章提到了哪些股票與概念。
*   **探針路徑 (BottomUpProbe)**：為了節省 API 費用與算力，只挑選「熱度高且命中白名單」的文章，丟給 LLM 進行深度文本萃取，找出潛在的「新題材關鍵字」。

### 3. 儲存與量化層 (Storage)
分為「定義型資料庫」與「時序型資料庫」。
*   **PostgreSQL**：用來儲存相對靜態的股票清單、概念名詞，以及需要我們人工審核的「候選新概念」。
*   **InfluxDB**：負責消化龐大的資料流，每一篇文章命中幾次關鍵字、附帶多少情緒分數（Sentiment Score），都會成為 InfluxDB 的一個時間切片點 (Data Point)。

### 4. 警報輸出層 (Alerting)
定時監控 InfluxDB 的數據變化率。當某檔股票在過去 15 分鐘的累積 mention count 突破均線，系統判斷為「異常激增」，就會觸發 Telegram Bot 推播警報至使用者的頻道中。
