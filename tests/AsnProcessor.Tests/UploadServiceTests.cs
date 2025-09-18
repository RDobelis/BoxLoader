using System.Text;
using AsnProcessor.Application.Abstractions;
using AsnProcessor.Application.Services;
using AsnProcessor.Domain.Entities;
using AsnProcessor.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
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
        Directory.GetFiles(_archiveFolder).Length.ShouldBe(1);
    }
    
    [Fact]
    public async Task HandleUploadAsync_ShouldRenameIfFileExistsInArchive()
    {
        const string text1 = """
                             HDR  TRSP117                                                                                     1111111A
                             LINE P000001661                           9781473663800                     12
                             """;

        const string text2 = """
                             HDR  TRSP117                                                                                     2222222B
                             LINE P000001662                           9781473663817                     5
                             """;

        const string fileName = "sameName.txt";

        var tmp1 = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(tmp1, text1);
        await _uploadService.HandleUploadAsync(tmp1, CancellationToken.None);

        var tmp2 = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(tmp2, text2);
        await _uploadService.HandleUploadAsync(tmp2, CancellationToken.None);

        var archivedFiles = Directory.GetFiles(_archiveFolder);
        archivedFiles.Length.ShouldBe(2);

        archivedFiles.Any(f => Path.GetFileName(f) == fileName).ShouldBeTrue();
        archivedFiles.Any(f => Path.GetFileName(f).Contains("_")).ShouldBeTrue();
    }
    
    [Fact]
    public async Task HandleUploadAsync_WhenParserThrows_ShouldMoveFileToFailed()
    {
        var failedFolder = Path.Combine(Path.GetTempPath(), "test-failed-" + Guid.NewGuid());
        Directory.CreateDirectory(failedFolder);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArchiveFolder"] = _archiveFolder,
                ["FailedFolder"] = failedFolder,
                ["BatchSize"] = "10"
            })
            .Build();

        var badParser = Substitute.For<IFileParser>();
        badParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(_ => ThrowingAsyncEnumerable());

        var badService = new UploadService(_db, badParser, config);

        var tmp = await CreateTempFileAsync("BROKEN FILE CONTENT");

        await Should.ThrowAsync<InvalidDataException>(async () => await badService.HandleUploadAsync(tmp, CancellationToken.None));

        var failedFiles = Directory.GetFiles(failedFolder);
        failedFiles.Length.ShouldBe(1);
        File.Exists(tmp).ShouldBeFalse();
    }

    private static async IAsyncEnumerable<Box> ThrowingAsyncEnumerable()
    {
        await Task.Yield();
        throw new InvalidDataException("bad data");
        yield break; // required for async iterators
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
