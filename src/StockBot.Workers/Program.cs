using Microsoft.EntityFrameworkCore;
using StockBot.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

// PostgreSQL + pgvector
builder.Services.AddDbContext<StockBotDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseVector()
    )
);

var host = builder.Build();
host.Run();
