using AsnProcessor.Application.Services;
using AsnProcessor.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AsnProcessor.Tests;

public class UploadServiceTests
{
    [Fact]
    public async Task HandleUploadAsync_SameFileTwice_ShouldNotDuplicate()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<AsnDbContext>().UseSqlite(conn).Options;
        await using var db = new AsnDbContext(opts);
        await db.Database.EnsureCreatedAsync();

        var parser = new FileParser();
        var sut = new UploadService(db, parser);

        const string text = """
                            HDR  TRSP117                                                                                     6874453I
                            LINE P000001661                           9781473663800                     12
                            """;
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, text);

        await sut.HandleUploadAsync(tmp, CancellationToken.None);
        await sut.HandleUploadAsync(tmp, CancellationToken.None);

        db.Boxes.Count().ShouldBe(1);
        db.BoxLines.Count().ShouldBe(1);
    }
}