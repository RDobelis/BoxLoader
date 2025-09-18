using AsnProcessor.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AsnProcessor.Worker;

public class FileWatcherService(ILogger<FileWatcherService> logger, IServiceProvider provider, IOptions<InboxOptions> options) : BackgroundService
{
    private readonly InboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.InboxPath);
        Directory.CreateDirectory(_options.ArchivePath);

        using var watcher = new FileSystemWatcher(_options.InboxPath);
        watcher.Filter = "*.txt";
        watcher.EnableRaisingEvents = true;

        watcher.Created += async (_, e) =>
        {
            try
            {
                logger.LogInformation("Processing file: {file}", e.FullPath);
                await ProcessWhenReady(e.FullPath, cancellationToken);
                logger.LogInformation("Processed successfully: {file}", e.FullPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing {file}", e.FullPath);
            }
        };

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private async Task ProcessWhenReady(string path, CancellationToken cancellationToken)
    {
        const int maxRetries = 10;
        const int delayMs = 500;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                // Try exclusive open → ensures file is not locked
                await using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    break;
                }
            }
            catch (IOException)
            {
                if (i == maxRetries - 1) throw;
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        using var scope = provider.CreateScope();
        var uploader = scope.ServiceProvider.GetRequiredService<IUploadService>();
        await uploader.HandleUploadAsync(path, cancellationToken);
    }
}
