using AsnProcessor.Application.Abstractions;

namespace AsnProcessor.Worker;

public sealed class FileWatcherService(ILogger<FileWatcherService> logger, IServiceProvider provider, IConfiguration configuration) : BackgroundService
{
    private readonly string _watchPath = configuration.GetValue<string>("InboxFolder") ?? "inbox";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_watchPath);
        logger.LogInformation("Watching folder: {Path}", Path.GetFullPath(_watchPath));

        using var watcher = new FileSystemWatcher(_watchPath);
        watcher.Filter = "*.txt";
        watcher.IncludeSubdirectories = false;
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime;
        watcher.EnableRaisingEvents = true;

        watcher.Created += (_, e) => _ = ProcessWhenReady(e.FullPath, stoppingToken);
        watcher.Changed += (_, e) => _ = ProcessWhenReady(e.FullPath, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessWhenReady(string path, CancellationToken ct)
    {
        try
        {
            long lastSize = -1;
            for (var i = 0; i < 20 && !ct.IsCancellationRequested; i++)
            {
                if (!File.Exists(path)) return;
                
                var info = new FileInfo(path);
                if (info.Length > 0 && info.Length == lastSize) break;

                lastSize = info.Length;
                await Task.Delay(250, ct);
            }

            using var scope = provider.CreateScope();
            var uploader = scope.ServiceProvider.GetRequiredService<IUploadService>();
            await uploader.HandleUploadAsync(path, ct);

            logger.LogInformation("Processed: {File}", path);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed processing {File}", path);
        }
    }
}
