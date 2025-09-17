using AsnProcessor.Application.Abstractions;
using AsnProcessor.Domain.Entities;

namespace AsnProcessor.Application.Services;

public sealed class FileParser : IFileParser
{
    public async IAsyncEnumerable<Box> ParseAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        Box? current = null;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var raw = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var tokens = ParseTokens(raw);
            if (tokens.Length == 0) continue;

            switch (tokens[0])
            {
                case "HDR":
                {
                    if (current != null) yield return current;
                    current = CreateBox(tokens);
                    continue;
                }
                case "LINE" when current != null:
                {
                    current.Lines.Add(CreateBoxLine(tokens));
                    break;
                }
            }
        }

        if (current != null) yield return current;
    }

    private static string[] ParseTokens(string line) => line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string GetTokenOrEmpty(string[] tokens, int index) => tokens.Length > index ? tokens[index] : string.Empty;

    private static Box CreateBox(string[] tokens) => new()
    {
        SupplierIdentifier = GetTokenOrEmpty(tokens, 1),
        Identifier = GetTokenOrEmpty(tokens, 2)
    };

    private static BoxLine CreateBoxLine(string[] tokens) => new()
    {
        PoNumber = GetTokenOrEmpty(tokens, 1),
        Isbn = GetTokenOrEmpty(tokens, 2),
        Quantity = tokens.Length > 3 && int.TryParse(tokens[3], out var quantity) ? quantity : 0
    };
}