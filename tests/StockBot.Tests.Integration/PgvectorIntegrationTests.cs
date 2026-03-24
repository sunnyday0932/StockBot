using Microsoft.EntityFrameworkCore;
using Pgvector;
using StockBot.Infrastructure.Persistence;
using StockBot.Tests.Integration.Fixtures;

namespace StockBot.Tests.Integration;

[Collection("Database")]
public class PgvectorIntegrationTests(DatabaseFixture fixture)
{
    private readonly StockBotDbContext _db = fixture.DbContext;

    [Fact]
    public async Task Can_save_and_retrieve_vector_embedding()
    {
        var docId = $"embed-{Guid.NewGuid()}";
        var embedding = new Vector(Enumerable.Range(0, 1536).Select(i => (float)i / 1536).ToArray());

        var record = new DocumentEmbedding
        {
            DocumentId = docId,
            Embedding = embedding,
            SentimentScore = 0.75f,
            ProcessedAt = DateTime.UtcNow
        };

        _db.Set<DocumentEmbedding>().Add(record);
        await _db.SaveChangesAsync();

        var saved = await _db.Set<DocumentEmbedding>().FindAsync(docId);

        Assert.NotNull(saved);
        Assert.Equal(0.75f, saved.SentimentScore);
        Assert.Equal(1536, saved.Embedding.ToArray().Length);
    }

    [Fact]
    public async Task Sentiment_score_in_embedding_is_optional()
    {
        var docId = $"embed-nosent-{Guid.NewGuid()}";
        var embedding = new Vector(new float[1536]);

        var record = new DocumentEmbedding
        {
            DocumentId = docId,
            Embedding = embedding,
            SentimentScore = null,
            ProcessedAt = DateTime.UtcNow
        };

        _db.Set<DocumentEmbedding>().Add(record);
        await _db.SaveChangesAsync();

        var saved = await _db.Set<DocumentEmbedding>().FindAsync(docId);

        Assert.NotNull(saved);
        Assert.Null(saved.SentimentScore);
    }
}
