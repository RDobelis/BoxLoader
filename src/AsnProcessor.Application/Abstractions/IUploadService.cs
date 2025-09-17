namespace AsnProcessor.Application.Abstractions;

public interface IUploadService
{
    Task HandleUploadAsync(string filePath, CancellationToken cancellationToken);
}