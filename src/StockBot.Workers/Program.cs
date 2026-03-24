using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using StockBot.Infrastructure.InfluxDb;
using StockBot.Infrastructure.MarketData;
using StockBot.Infrastructure.Options;
using StockBot.Infrastructure.Persistence;
using StockBot.Infrastructure.Processing;
using StockBot.Workers.Workers;

var builder = Host.CreateApplicationBuilder(args);

// PostgreSQL + pgvector
builder.Services.AddDbContext<StockBotDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseVector()
    )
);

// InfluxDB
builder.Services.Configure<InfluxDbOptions>(
    builder.Configuration.GetSection("InfluxDb"));
builder.Services.AddSingleton<IInfluxDbWriter, InfluxDbWriter>();

// Market Data Fetchers（Polling 模式，REST API 類型）
// 新增來源只需實作 IPollingMarketDataFetcher 並在此加入新的 AddHttpClient 即可
builder.Services.Configure<TwseMarketFetcherOptions>(
    builder.Configuration.GetSection("TwseApi"));
builder.Services.AddHttpClient<TwseMarketFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IPollingMarketDataFetcher>(sp =>
    sp.GetRequiredService<TwseMarketFetcher>());

builder.Services.Configure<TpexMarketFetcherOptions>(
    builder.Configuration.GetSection("TpexApi"));
builder.Services.AddHttpClient<TpexMarketFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IPollingMarketDataFetcher>(sp =>
    sp.GetRequiredService<TpexMarketFetcher>());

// PTT Crawler
builder.Services.Configure<PttCrawlerOptions>(
    builder.Configuration.GetSection("PttCrawler"));
builder.Services.AddHttpClient<PttCrawlerWorker>(client =>
{
    client.DefaultRequestHeaders.Add("Cookie", "over18=1");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Cnyes News Crawler
builder.Services.Configure<CnyesCrawlerOptions>(
    builder.Configuration.GetSection("CnyesCrawler"));
builder.Services.AddHttpClient<CnyesNewsCrawlerWorker>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Processing
builder.Services.AddSingleton<ITopDownMatcher, TopDownMatcher>();

// Workers
builder.Services.AddHostedService<WhitelistInitializerWorker>();
builder.Services.AddHostedService<MarketDataWorker>();
builder.Services.AddHostedService<PttCrawlerWorker>();
builder.Services.AddHostedService<CnyesNewsCrawlerWorker>();
builder.Services.AddHostedService<ProcessingWorker>();

var host = builder.Build();
host.Run();
