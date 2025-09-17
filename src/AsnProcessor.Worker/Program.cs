using AsnProcessor.Application.Abstractions;
using AsnProcessor.Application.Services;
using AsnProcessor.Infrastructure;
using AsnProcessor.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var dbPath = builder.Configuration.GetValue<string>("DatabasePath") ?? "asn.db";

builder.Services.AddDbContext<AsnDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton<IFileParser, FileParser>();
builder.Services.AddScoped<IUploadService, UploadService>();

builder.Services.Configure<InboxOptions>(builder.Configuration);

builder.Services.AddHostedService<FileWatcherService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AsnDbContext>();
    db.Database.Migrate();
}

// CLI override: --process <filePath>
if (args.Length == 2 && args[0].Equals("--process", StringComparison.OrdinalIgnoreCase))
{
    using var scope = host.Services.CreateScope();
    var uploader = scope.ServiceProvider.GetRequiredService<IUploadService>();
    await uploader.HandleUploadAsync(args[1], CancellationToken.None);
    return;
}

await host.RunAsync();