# Code Review Fixes (Critical + Important) Implementation Plan

**Goal:** Fix all Critical (C-1~C-4) and Important (I-1~I-11) issues from the 2026-03-25 code review, one commit per fix.

**Tech Stack:** C# 12, .NET 8, EF Core 8, Telegram.Bot 21.x, InfluxDB.Client, pgvector

---

## Task 1 — C-1: Fix TopDownMatcher Thread-Safety
Pack `_trie` and `_entityCodeMap` into a single immutable `record`, use one `volatile` reference swap in `RebuildAsync` instead of two separate field assignments.
- **Files:** `TopDownMatcher.cs`

## Task 2 — C-2: Add Authorization Check in TelegramBotWorker
Reject messages whose `Chat.Id` does not match the configured `ChatId` at the top of `HandleUpdateAsync`.
- **Files:** `TelegramBotWorker.cs`

## Task 3 — C-3: Remove Secrets from appsettings.json
Clear passwords and tokens from `appsettings.json`; add `docs/configuration.md` explaining how to supply them via env vars or .NET user-secrets.
- **Files:** `appsettings.json`, `docs/configuration.md` (new)

## Task 4 — C-4: Consolidate to Single ITelegramBotClient Instance
Register `ITelegramBotClient` as singleton in `Program.cs` with startup token validation; inject it into both `TelegramNotifier` and `TelegramBotWorker` instead of constructing separately.
- **Files:** `Program.cs`, `TelegramNotifier.cs`, `TelegramBotWorker.cs`

## Task 5 — I-1: Fix AhoCorasickTrie.KeywordCount
Track count with a `_keywordCount` field incremented during Build phase 1; remove the incorrect leaf-only BFS traversal.
- **Files:** `AhoCorasickTrie.cs`

## Task 6 — I-2: Validate InfluxDB Bucket/Org at Startup
Add a `Validate()` method to `InfluxDbOptions` using an allowlist regex; call it in `Program.cs` before the host starts.
- **Files:** `InfluxDbOptions.cs`, `Program.cs`

## Task 7 — I-3: Fix ProcessingWorker Write Order
Move `SaveChangesAsync` (ProcessedAt + embeddings) before `WriteMentionsAsync` so a DB failure doesn't leave InfluxDB written with unprocessed documents.
- **Files:** `ProcessingWorker.cs`

## Task 8 — I-4a: Fix N+1 in CnyesNewsCrawlerWorker
Batch-query all candidate `DocumentId`s with `WHERE IN` before the loop instead of calling `AnyAsync` per article.
- **Files:** `CnyesNewsCrawlerWorker.cs`

## Task 9 — I-4b: Fix N+1 in PttCrawlerWorker
Same pattern: pre-query existing IDs as a batch before the article-processing loop; pass the result into `ProcessArticleAsync`.
- **Files:** `PttCrawlerWorker.cs`

## Task 10 — I-5: Fix N+1 in BottomUpProbeWorker
Collect all concept keywords from the full batch first, then fetch all existing `DiscoveredConcept` rows in one `WHERE IN` query, then upsert in-memory.
- **Files:** `BottomUpProbeWorker.cs`

## Task 11 — I-6: Add Unique Index on DiscoveredConcept.Keyword
Change the existing index to `.IsUnique()` in `OnModelCreating`; add an EF migration.
- **Files:** `StockBotDbContext.cs`, new migration

## Task 12 — I-7: Skip Whitelist Fetch If Already Populated
At startup, count existing `TrackedEntity` stocks; skip the TWSE/TPEX API fetch if the table is non-empty.
- **Files:** `WhitelistInitializerWorker.cs`

## Task 13 — I-8: Remove Unused PollingTimeoutSeconds
Delete the property from `TelegramOptions` and the corresponding key from `appsettings.json`.
- **Files:** `TelegramOptions.cs`, `appsettings.json`

## Task 14 — I-9: Implement StockName Lookup in SignalAnalyzer
Inject `IServiceScopeFactory`; load a `StockCode → PrimaryName` dictionary from `TrackedEntity` at the start of each `AnalyzeAsync` call; pass the name into `BuildSignal`.
- **Files:** `SignalAnalyzer.cs`

## Task 15 — I-10: Move DocumentEmbedding to Domain Layer
Create `src/StockBot.Domain/Entities/DocumentEmbedding.cs` with `float[]` embedding; remove the class from `StockBotDbContext.cs`; add an EF value converter in `OnModelCreating` to translate `float[] ↔ Vector`.
- **Files:** `DocumentEmbedding.cs` (new in Domain), `StockBotDbContext.cs`, `ProcessingWorker.cs`

## Task 16 — I-11: Fix Shared DbContext in Integration Tests
Replace `fixture.DbContext` (shared instance) with a `CreateDbContext()` factory method; read the connection string from the `TEST_POSTGRES_CONNECTION` env var with the hardcoded string as fallback.
- **Files:** `DatabaseFixture.cs`, test files that consume `fixture.DbContext`
