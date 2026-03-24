namespace StockBot.Infrastructure.Options;

public sealed class InfluxDbOptions
{
    public string Url    { get; init; } = string.Empty;
    public string Token  { get; init; } = string.Empty;
    public string Org    { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
}
