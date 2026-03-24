namespace StockBot.Infrastructure.InfluxDb;

public sealed class InfluxDbOptions
{
    public string Url    { get; set; } = string.Empty;
    public string Token  { get; set; } = string.Empty;
    public string Org    { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
}
