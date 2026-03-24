using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;

namespace StockBot.Infrastructure.Persistence;

public class StockBotDbContext(DbContextOptions<StockBotDbContext> options) : DbContext(options)
{
    public DbSet<TrackedEntity> TrackedEntities => Set<TrackedEntity>();
    public DbSet<EntityAlias> EntityAliases => Set<EntityAlias>();
    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();
    public DbSet<DiscoveredConcept> DiscoveredConcepts => Set<DiscoveredConcept>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 啟用 pgvector 擴充套件
        modelBuilder.HasPostgresExtension("vector");

        // TrackedEntity
        modelBuilder.Entity<TrackedEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>();
            e.Property(x => x.PrimaryName).HasMaxLength(100).IsRequired();
            e.Property(x => x.StockCode).HasMaxLength(10);
            e.HasMany(x => x.Aliases)
             .WithOne(x => x.Entity)
             .HasForeignKey(x => x.EntityId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // EntityAlias
        modelBuilder.Entity<EntityAlias>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Keyword).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Keyword); // 比對時快速查找
        });

        // SourceDocument
        modelBuilder.Entity<SourceDocument>(e =>
        {
            e.HasKey(x => x.DocumentId);
            e.Property(x => x.SourceType).HasConversion<string>();
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.Url).HasMaxLength(1000);
            e.HasIndex(x => x.PublishedAt);
            e.HasIndex(x => x.SourceType);
        });

        // DiscoveredConcept
        modelBuilder.Entity<DiscoveredConcept>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Keyword).HasMaxLength(100).IsRequired();
            e.Property(x => x.AssociatedStockCode).HasMaxLength(10);
            e.HasIndex(x => x.Keyword);
            e.HasIndex(x => x.IsApprovedAndPromoted);
        });

        // AnalysisResult 與 VectorEmbedding 存入獨立的 pgvector 資料表
        modelBuilder.Entity<DocumentEmbedding>(e =>
        {
            e.HasKey(x => x.DocumentId);
            e.Property(x => x.Embedding)
             .HasColumnType("vector(1536)"); // OpenAI text-embedding-3-small 維度
        });
    }
}

// VectorEmbedding 獨立存放，避免污染 SourceDocument 主表
public class DocumentEmbedding
{
    public string DocumentId { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;
    public float? SentimentScore { get; set; }
    public DateTime ProcessedAt { get; set; }
}
