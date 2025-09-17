using System.Security.Cryptography;
using AsnProcessor.Application.Abstractions;
using AsnProcessor.Domain.Entities;
using AsnProcessor.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AsnProcessor.Application.Services;

public sealed class UploadService(AsnDbContext db, IFileParser parser) : IUploadService
{
    private const int BatchSize = 1000;

    public async Task HandleUploadAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        byte[] checksum;
        await using (var fs = File.OpenRead(filePath))
        using (var sha = SHA256.Create())
        {
            checksum = await sha.ComputeHashAsync(fs, cancellationToken);
        }

        var already = await db.ProcessedFiles.AnyAsync(p => p.ChecksumSha256 == checksum, cancellationToken);
        if (already) return;

        // Execute pragma statements BEFORE starting transaction
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous = OFF;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var buffer = new List<Box>(BatchSize);

        await using (var fs2 = File.OpenRead(filePath))
        {
            await foreach (var box in parser.ParseAsync(fs2, cancellationToken))
            {
                buffer.Add(box);
                if (buffer.Count < BatchSize) continue;
                await InsertBatchAsync(buffer, cancellationToken);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0) await InsertBatchAsync(buffer, cancellationToken);

        db.ProcessedFiles.Add(new ProcessedFile
        {
            FileName = Path.GetFileName(filePath),
            ChecksumSha256 = checksum,
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task InsertBatchAsync(List<Box> buffer, CancellationToken ct)
    {
        var prev = db.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            await db.Boxes.AddRangeAsync(buffer, ct);
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = prev;
        }
    }
}
