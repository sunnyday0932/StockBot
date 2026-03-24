using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using StockBot.Infrastructure.Persistence;

namespace StockBot.Tests.Integration.Fixtures;

/// <summary>
/// 整合測試共用 Fixture，連接本地 Docker PostgreSQL。
/// 每個 Collection 共用一個 DbContext，測試結束後清理資料。
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=stockbot;Username=stockbot;Password=stockbot_pass";

    public StockBotDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StockBotDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseVector())
            .Options;

        DbContext = new StockBotDbContext(options);

        // 確保 schema 是最新的
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // 清理測試資料，保留 schema
        await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"EntityAliases\"");
        await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"TrackedEntities\"");
        await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"SourceDocuments\"");
        await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"DiscoveredConcepts\"");
        await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"DocumentEmbedding\"");
        await DbContext.DisposeAsync();
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
