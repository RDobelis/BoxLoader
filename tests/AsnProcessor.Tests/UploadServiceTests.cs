using System.Text;
using AsnProcessor.Application.Services;
using AsnProcessor.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace AsnProcessor.Tests;

public class UploadServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AsnDbContext _db;
    private readonly UploadService _uploadService;
    private readonly string _archiveFolder;

    public UploadServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var opts = new DbContextOptionsBuilder<AsnDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AsnDbContext(opts);
        _db.Database.EnsureCreated();

        var parser = new FileParser();

        _archiveFolder = Path.Combine(Path.GetTempPath(), "test-archive-" + Guid.NewGuid());
        Directory.CreateDirectory(_archiveFolder);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArchiveFolder"] = _archiveFolder,
                ["BatchSize"] = "10"
            })
            .Build();

        _uploadService = new UploadService(_db, parser, config);
    }

    [Fact]
    public async Task HandleUploadAsync_SameFileTwice_ShouldNotDuplicate_AndArchiveFile()
    {
        const string text = """
                            HDR  TRSP117                                                                                     6874453I
                            LINE P000001661                           9781473663800                     12
                            """;
        var tmp = await CreateTempFileAsync(text);

        await _uploadService.HandleUploadAsync(tmp, CancellationToken.None);

        _db.Boxes.Count().ShouldBe(1);
        _db.BoxLines.Count().ShouldBe(1);
        Directory.GetFiles(_archiveFolder).Length.ShouldBe(1);
    }

    [Fact]
    public async Task HandleUploadAsync_ShouldFlushBuffer_WhenMoreThanBatchSizeLines()
    {
        var sb = new StringBuilder();
        sb.AppendLine("HDR  TRSP117                                                                                     6874453I");
        for (var i = 0; i < 1005; i++)
        {
            sb.AppendLine($"LINE P000001661 9781473663{i:D4} {i + 1}");
        }

        var tmp = await CreateTempFileAsync(sb.ToString());

        await _uploadService.HandleUploadAsync(tmp, CancellationToken.None);

        _db.Boxes.Count().ShouldBe(1);
        _db.BoxLines.Count().ShouldBe(1005);

        // Verify file moved to archive
        Directory.GetFiles(_archiveFolder).Length.ShouldBe(1);
    }

    private static async Task<string> CreateTempFileAsync(string content)
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, content);
        return tmp;
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();

        if (Directory.Exists(_archiveFolder))
        {
            Directory.Delete(_archiveFolder, recursive: true);
        }
    }
}
