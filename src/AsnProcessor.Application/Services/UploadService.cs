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
    private readonly string _failedFolder = config.GetValue<string>("FailedFolder") ?? "failed";
    private readonly int _batchSize = config.GetValue<int>("BatchSize");

    public async Task HandleUploadAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        try
        {
            var checksum = await ComputeChecksum(filePath, cancellationToken);
            if (await db.ProcessedFiles.AnyAsync(p => p.ChecksumSha256 == checksum, cancellationToken))
                return;

            Directory.CreateDirectory(_archiveFolder);
            Directory.CreateDirectory(_failedFolder);

            await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous = OFF;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            await ProcessFile(filePath, cancellationToken);

            db.ProcessedFiles.Add(new ProcessedFile
            {
                FileName = Path.GetFileName(filePath),
                ChecksumSha256 = checksum,
                ProcessedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await MoveToArchive(filePath);
        }
        catch (Exception)
        {
            await MoveToFailed(filePath);
            throw;
        }
    }

    private static async Task<byte[]> ComputeChecksum(string filePath, CancellationToken cancellationToken)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return await sha.ComputeHashAsync(fs, cancellationToken);
    }

    private async Task ProcessFile(string filePath, CancellationToken cancellationToken)
    {
        var buffer = new List<Box>(_batchSize);
        await using var fs = File.OpenRead(filePath);

        await foreach (var box in parser.ParseAsync(fs, cancellationToken))
        {
            buffer.Add(box);
            if (buffer.Count < _batchSize) continue;
            await InsertBatchAsync(buffer, cancellationToken);
            buffer.Clear();
        }

        if (buffer.Count > 0) await InsertBatchAsync(buffer, cancellationToken);
    }

    private async Task InsertBatchAsync(List<Box> buffer, CancellationToken cancellationToken)
    {
        await db.BulkInsertAsync(buffer, new BulkConfig { SetOutputIdentity = true }, cancellationToken: cancellationToken);

        var allLines = buffer.SelectMany(b => b.Lines.Select(l =>
        {
            l.BoxId = b.Id;
            return l;
        })).ToList();

        if (allLines.Count > 0) await db.BulkInsertAsync(allLines, cancellationToken: cancellationToken);
    }

    private Task MoveToArchive(string filePath)
    {
        var destPath = GetUniquePath(_archiveFolder, filePath);
        File.Move(filePath, destPath);
        return Task.CompletedTask;
    }

    private Task MoveToFailed(string filePath)
    {
        var destPath = GetUniquePath(_failedFolder, filePath);
        File.Move(filePath, destPath);
        return Task.CompletedTask;
    }

    private static string GetUniquePath(string targetFolder, string sourceFile)
    {
        var fileName = Path.GetFileName(sourceFile);
        var destPath = Path.Combine(targetFolder, fileName);

        if (!File.Exists(destPath)) return destPath;
        
        var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        destPath = Path.Combine(targetFolder, $"{name}_{ts}{ext}");

        return destPath;
    }
}
