using Microsoft.EntityFrameworkCore;
using StockBot.Domain.Entities;
using StockBot.Tests.Integration.Fixtures;

namespace StockBot.Tests.Integration;

[Collection("Database")]
public class DiscoveredConceptIntegrationTests(DatabaseFixture fixture)
{
    private readonly StockBot.Infrastructure.Persistence.StockBotDbContext _db = fixture.DbContext;

    [Fact]
    public async Task Can_save_discovered_concept_with_timestamps()
    {
        var now = DateTime.UtcNow;
        var concept = new DiscoveredConcept
        {
            SourceDocumentId = "doc-001",
            AssociatedStockCode = "2330",
            Keyword = $"玻璃基板-{Guid.NewGuid()}",
            AppearanceCount = 3,
            FirstDiscoveredAt = now.AddDays(-5),
            LastSeenAt = now,
            IsApprovedAndPromoted = false
        };

        _db.DiscoveredConcepts.Add(concept);
        await _db.SaveChangesAsync();

        var saved = await _db.DiscoveredConcepts.FindAsync(concept.Id);

        Assert.NotNull(saved);
        Assert.Equal("2330", saved.AssociatedStockCode);
        Assert.False(saved.IsApprovedAndPromoted);
        Assert.True(saved.LastSeenAt > saved.FirstDiscoveredAt);
    }

    [Fact]
    public async Task Can_query_pending_approval_concepts()
    {
        var keyword = $"矽光子-{Guid.NewGuid()}";
        _db.DiscoveredConcepts.Add(new DiscoveredConcept
        {
            SourceDocumentId = "doc-002",
            Keyword = keyword,
            AppearanceCount = 7,
            FirstDiscoveredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            IsApprovedAndPromoted = false
        });
        await _db.SaveChangesAsync();

        var pending = await _db.DiscoveredConcepts
            .Where(c => !c.IsApprovedAndPromoted)
            .ToListAsync();

        Assert.Contains(pending, c => c.Keyword == keyword);
    }

    [Fact]
    public async Task Can_approve_concept()
    {
        var keyword = $"CoWoS-{Guid.NewGuid()}";
        var concept = new DiscoveredConcept
        {
            SourceDocumentId = "doc-003",
            Keyword = keyword,
            AppearanceCount = 12,
            FirstDiscoveredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            IsApprovedAndPromoted = false
        };

        _db.DiscoveredConcepts.Add(concept);
        await _db.SaveChangesAsync();

        concept.IsApprovedAndPromoted = true;
        await _db.SaveChangesAsync();

        var saved = await _db.DiscoveredConcepts.FindAsync(concept.Id);
        Assert.True(saved!.IsApprovedAndPromoted);
    }
}
