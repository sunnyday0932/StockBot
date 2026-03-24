using Microsoft.EntityFrameworkCore;
using StockBot.Domain.Entities;
using StockBot.Domain.Enums;
using StockBot.Tests.Integration.Fixtures;

namespace StockBot.Tests.Integration;

[Collection("Database")]
public class SourceDocumentIntegrationTests(DatabaseFixture fixture)
{
    private readonly StockBot.Infrastructure.Persistence.StockBotDbContext _db = fixture.DbContext;

    [Fact]
    public async Task Can_save_and_retrieve_ptt_document_with_vote_counts()
    {
        var doc = new SourceDocument
        {
            DocumentId = $"ptt-test-{Guid.NewGuid()}",
            SourceType = SourceType.PttStock,
            Author = "test_user",
            Title = "[標的] 2330 台積電 多",
            Content = "AI 需求旺，看好長線",
            PublishedAt = DateTime.UtcNow,
            Url = "https://www.ptt.cc/bbs/Stock/test",
            PttUpvoteCount = 88,
            PttDownvoteCount = 5,
            PttArrowCount = 12
        };

        _db.SourceDocuments.Add(doc);
        await _db.SaveChangesAsync();

        var saved = await _db.SourceDocuments.FindAsync(doc.DocumentId);

        Assert.NotNull(saved);
        Assert.Equal(SourceType.PttStock, saved.SourceType);
        Assert.Equal(88, saved.PttUpvoteCount);
        Assert.Equal(5, saved.PttDownvoteCount);
        Assert.Equal(12, saved.PttArrowCount);
    }

    [Fact]
    public async Task News_document_should_have_null_ptt_fields()
    {
        var doc = new SourceDocument
        {
            DocumentId = $"news-test-{Guid.NewGuid()}",
            SourceType = SourceType.NewsCnyes,
            Title = "鉅亨網：台積電法說會重點",
            Content = "EPS 創新高",
            PublishedAt = DateTime.UtcNow
        };

        _db.SourceDocuments.Add(doc);
        await _db.SaveChangesAsync();

        var saved = await _db.SourceDocuments.FindAsync(doc.DocumentId);

        Assert.NotNull(saved);
        Assert.Null(saved.PttUpvoteCount);
        Assert.Null(saved.PttDownvoteCount);
        Assert.Null(saved.PttArrowCount);
    }

    [Fact]
    public async Task Can_query_documents_by_source_type()
    {
        var pttDoc = new SourceDocument
        {
            DocumentId = $"ptt-query-{Guid.NewGuid()}",
            SourceType = SourceType.PttStock,
            Title = "PTT 文章",
            Content = "內文",
            PublishedAt = DateTime.UtcNow
        };
        var newsDoc = new SourceDocument
        {
            DocumentId = $"news-query-{Guid.NewGuid()}",
            SourceType = SourceType.NewsUdn,
            Title = "經濟日報新聞",
            Content = "內文",
            PublishedAt = DateTime.UtcNow
        };

        _db.SourceDocuments.AddRange(pttDoc, newsDoc);
        await _db.SaveChangesAsync();

        var pttDocs = await _db.SourceDocuments
            .Where(d => d.SourceType == SourceType.PttStock)
            .ToListAsync();

        Assert.Contains(pttDocs, d => d.DocumentId == pttDoc.DocumentId);
        Assert.DoesNotContain(pttDocs, d => d.DocumentId == newsDoc.DocumentId);
    }
}
