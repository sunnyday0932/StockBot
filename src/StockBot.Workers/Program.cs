using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using StockBot.Infrastructure.InfluxDb;
using StockBot.Infrastructure.MarketData;
using StockBot.Infrastructure.Persistence;
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

// TWSE Market Data Fetcher（具名 HttpClient，設定 timeout）
builder.Services.AddHttpClient<TwseMarketFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Workers
builder.Services.AddHostedService<MarketDataWorker>();

var host = builder.Build();
host.Run();
