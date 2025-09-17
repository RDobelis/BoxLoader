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

            var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            switch (tokens[0])
            {
                case "HDR":
                {
                    if (current != null) yield return current;

                    current = new Box
                    {
                        SupplierIdentifier = tokens.Length > 1 ? tokens[1] : string.Empty,
                        Identifier = tokens.Length > 2 ? tokens[2] : string.Empty
                    };
                    continue;
                }
                case "LINE" when current != null:
                {
                    var poNumber = tokens.Length > 1 ? tokens[1] : string.Empty;
                    var isbn = tokens.Length > 2 ? tokens[2] : string.Empty;
                    var quantity = tokens.Length > 3 && int.TryParse(tokens[3], out var q) ? q : 0;

                    current.Lines.Add(new BoxLine
                    {
                        PoNumber = poNumber,
                        Isbn = isbn,
                        Quantity = quantity
                    });
                    break;
                }
            }
        }

        if (current != null) yield return current;
    }
}