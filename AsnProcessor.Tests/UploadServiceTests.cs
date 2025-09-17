using AsnProcessor.Application.Services;
using AsnProcessor.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AsnProcessor.Tests;

public class UploadServiceTests
{
    [Fact]
    public async Task HandleUploadAsync_WhenCalled_ShouldStoreData()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var opts = new DbContextOptionsBuilder<AsnDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AsnDbContext(opts);
        await db.Database.EnsureCreatedAsync();

        var parser = new FileParser();
        var uploadService = new UploadService(db, parser);

        const string text = """
                            HDR  TRSP117                                                                                     6874453I
                            LINE P000001661                           9781473663800                     12
                            """;

        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, text);

        await uploadService.HandleUploadAsync(tmp, CancellationToken.None);

        db.Boxes.Count().ShouldBe(1);
        db.BoxLines.Count().ShouldBe(1);

        var box = db.Boxes.Include(x => x.Lines).First();
        box.Identifier.ShouldBe("6874453I");
        box.Lines.First().Quantity.ShouldBe(12);

        File.Delete(tmp);
    
        await connection.CloseAsync();
    }
}