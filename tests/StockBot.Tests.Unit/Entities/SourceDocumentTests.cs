using StockBot.Domain.Entities;
using StockBot.Domain.Enums;

namespace StockBot.Tests.Unit.Entities;

public class SourceDocumentTests
{
    [Fact]
    public void Ptt_document_should_support_vote_counts()
    {
        var doc = new SourceDocument
        {
            DocumentId = "M.1234567890.A.123",
            SourceType = SourceType.PttStock,
            Title = "[標的] 2330 台積電",
            Content = "看好未來",
            PublishedAt = DateTime.UtcNow,
            PttUpvoteCount = 42,
            PttDownvoteCount = 3,
            PttArrowCount = 10
        };

        Assert.Equal(42, doc.PttUpvoteCount);
        Assert.Equal(3, doc.PttDownvoteCount);
        Assert.Equal(10, doc.PttArrowCount);
    }

    [Fact]
    public void Non_ptt_document_should_have_null_vote_counts()
    {
        var doc = new SourceDocument
        {
            DocumentId = "news-001",
            SourceType = SourceType.NewsCnyes,
            Title = "鉅亨網新聞",
            Content = "內文",
            PublishedAt = DateTime.UtcNow
        };

        Assert.Null(doc.PttUpvoteCount);
        Assert.Null(doc.PttDownvoteCount);
        Assert.Null(doc.PttArrowCount);
    }

    [Theory]
    [InlineData(SourceType.PttStock)]
    [InlineData(SourceType.NewsYahoo)]
    [InlineData(SourceType.NewsCnyes)]
    [InlineData(SourceType.NewsUdn)]
    [InlineData(SourceType.Mops)]
    [InlineData(SourceType.Threads)]
    public void All_source_types_are_valid(SourceType sourceType)
    {
        var doc = new SourceDocument
        {
            DocumentId = "test-id",
            SourceType = sourceType,
            Title = "Test",
            Content = "Content",
            PublishedAt = DateTime.UtcNow
        };

        Assert.Equal(sourceType, doc.SourceType);
    }
}
