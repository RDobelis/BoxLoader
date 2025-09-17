using AsnProcessor.Domain.Entities;

namespace AsnProcessor.Application.Abstractions;

public interface IFileParser
{
    IAsyncEnumerable<Box> ParseAsync(Stream stream, CancellationToken cancellationToken);
}