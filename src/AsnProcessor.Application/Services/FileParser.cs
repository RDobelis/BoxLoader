using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AsnProcessor.Application.Abstractions;
using AsnProcessor.Domain.Entities;

namespace AsnProcessor.Application.Services;

public sealed partial class FileParser : IFileParser
{
    private static readonly Regex HdrRx = HdrRegex();

    private static readonly Regex LineRx = LineRegex();

    public async IAsyncEnumerable<Box> ParseAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        Box? current = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var raw = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var line = raw.TrimEnd();

            var hdr = HdrRx.Match(line);
            if (hdr.Success)
            {
                if (current != null) yield return current;

                current = new Box
                {
                    SupplierIdentifier = hdr.Groups["supplier"].Value,
                    Identifier = hdr.Groups["box"].Value
                };
                
                continue;
            }

            var match = LineRx.Match(line);
            
            if (!match.Success || current == null) continue;
            
            var quantity = int.TryParse(match.Groups["qty"].Value, out var q) ? q : 0;
            current.Lines.Add(new BoxLine
            {
                PoNumber = match.Groups["po"].Value,
                Isbn = match.Groups["isbn"].Value,
                Quantity = quantity
            });
        }

        if (current != null) yield return current;
    }

    [GeneratedRegex(@"^HDR\s+(?<supplier>\S+)\s+(?<box>\S+)\s*$", RegexOptions.Compiled)]
    private static partial Regex HdrRegex();
    
    [GeneratedRegex(@"^LINE\s+(?<po>\S+)\s+(?<isbn>\S+)\s+(?<qty>\d+)\s*$", RegexOptions.Compiled)]
    private static partial Regex LineRegex();
}
