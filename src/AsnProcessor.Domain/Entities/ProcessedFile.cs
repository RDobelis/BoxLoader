namespace AsnProcessor.Domain.Entities;

public class ProcessedFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;
    public byte[] ChecksumSha256 { get; set; } = [];
    public DateTimeOffset ProcessedAt { get; set; }
}