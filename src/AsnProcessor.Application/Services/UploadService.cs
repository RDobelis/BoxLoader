using System.Security.Cryptography;
using AsnProcessor.Application.Abstractions;
using AsnProcessor.Domain.Entities;
using AsnProcessor.Infrastructure;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AsnProcessor.Application.Services;

public sealed class UploadService(AsnDbContext db, IFileParser parser, IConfiguration config) : IUploadService
{
    private readonly string _archiveFolder = config.GetValue<string>("ArchiveFolder") ?? "archive";
    private readonly int _batchSize = config.GetValue("BatchSize", 10_000);

    public async Task HandleUploadAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var checksum = await ComputeChecksum(filePath, ct);
        if (await db.ProcessedFiles.AnyAsync(p => p.ChecksumSha256 == checksum, ct))
            return;

        Directory.CreateDirectory(_archiveFolder);

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

        await MoveToArchive(filePath);
    }

    private static async Task<byte[]> ComputeChecksum(string filePath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return await sha.ComputeHashAsync(fs, ct);
    }

    private async Task ProcessFile(string filePath, CancellationToken ct)
    {
        var buffer = new List<Box>(_batchSize);
        await using var fs = File.OpenRead(filePath);

        await foreach (var box in parser.ParseAsync(fs, ct))
        {
            buffer.Add(box);
            if (buffer.Count < _batchSize) continue;
            await InsertBatchAsync(buffer, ct);
            buffer.Clear();
        }

        if (buffer.Count > 0)
            await InsertBatchAsync(buffer, ct);
    }

    private async Task InsertBatchAsync(List<Box> buffer, CancellationToken ct)
    {
        await db.BulkInsertAsync(buffer, new BulkConfig { SetOutputIdentity = true }, cancellationToken: ct);

        var allLines = buffer.SelectMany(b => b.Lines.Select(l =>
        {
            l.BoxId = b.Id;
            return l;
        })).ToList();

        if (allLines.Count > 0)
            await db.BulkInsertAsync(allLines, cancellationToken: ct);
    }

    private Task MoveToArchive(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var destPath = Path.Combine(_archiveFolder, fileName);

        if (File.Exists(destPath))
        {
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            destPath = Path.Combine(_archiveFolder, $"{name}_{ts}{ext}");
        }

        File.Move(filePath, destPath);
        return Task.CompletedTask;
    }
}
