using System.Security.Cryptography;
using AsnProcessor.Application.Abstractions;
using AsnProcessor.Domain.Entities;
using AsnProcessor.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AsnProcessor.Application.Services;

public sealed class UploadService(AsnDbContext db, IFileParser parser) : IUploadService
{
    private const int BatchSize = 1000;

    public async Task HandleUploadAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        var checksum = await ComputeChecksum(filePath, ct);
        if (await db.ProcessedFiles.AnyAsync(p => p.ChecksumSha256 == checksum, ct)) return;

        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous = OFF;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await ProcessFile(filePath, ct);

        db.ProcessedFiles.Add(new ProcessedFile
        {
            FileName = Path.GetFileName(filePath),
            ChecksumSha256 = checksum,
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task<byte[]> ComputeChecksum(string filePath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return await sha.ComputeHashAsync(fs, ct);
    }

    private async Task ProcessFile(string filePath, CancellationToken ct)
    {
        var buffer = new List<Box>(BatchSize);
        await using var fs = File.OpenRead(filePath);

        await foreach (var box in parser.ParseAsync(fs, ct))
        {
            buffer.Add(box);
            if (buffer.Count < BatchSize) continue;
            await InsertBatchAsync(buffer, ct);
            buffer.Clear();
        }

        if (buffer.Count > 0) await InsertBatchAsync(buffer, ct);
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

