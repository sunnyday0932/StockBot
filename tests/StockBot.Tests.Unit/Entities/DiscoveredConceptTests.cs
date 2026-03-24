using StockBot.Domain.Entities;

namespace StockBot.Tests.Unit.Entities;

public class DiscoveredConceptTests
{
    [Fact]
    public void New_concept_should_not_be_approved_by_default()
    {
        var concept = new DiscoveredConcept
        {
            Keyword = "玻璃基板",
            SourceDocumentId = "doc-001",
            FirstDiscoveredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        Assert.False(concept.IsApprovedAndPromoted);
    }

    [Fact]
    public void Last_seen_should_be_updatable_independently_from_first_discovered()
    {
        var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var last = new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Utc);

        var concept = new DiscoveredConcept
        {
            Keyword = "矽光子",
            SourceDocumentId = "doc-001",
            AppearanceCount = 15,
            FirstDiscoveredAt = first,
            LastSeenAt = last
        };

        Assert.NotEqual(concept.FirstDiscoveredAt, concept.LastSeenAt);
        Assert.True(concept.LastSeenAt > concept.FirstDiscoveredAt);
    }
}
