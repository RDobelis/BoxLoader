using System.Text;
using AsnProcessor.Application.Services;
using AsnProcessor.Domain.Entities;
using Shouldly;

namespace AsnProcessor.Tests;

public class FileParserTests
{
    private readonly FileParser _fileParser = new();

    [Fact]
    public async Task ParseAsync_WhenCalled_ShouldReturnBoxes()
    {
        const string text = """
                            HDR  TRSP117                                                                                     6874453I
                            LINE P000001661                           9781473663800                     12
                            LINE P000001661                           9781473667273                     2
                            HDR  TRSP117                                                                                     6874454I
                            LINE G000009810                           9781473662179                     8
                            """;

        var boxes = await ParseTextAsync(text);

        boxes.Count.ShouldBe(2);

        boxes[0].SupplierIdentifier.ShouldBe("TRSP117");
        boxes[0].Identifier.ShouldBe("6874453I");
        boxes[0].Lines.Count.ShouldBe(2);

        boxes[1].Identifier.ShouldBe("6874454I");
        boxes[1].Lines.Count.ShouldBe(1);
        boxes[1].Lines.First().Isbn.ShouldBe("9781473662179");
    }

    [Fact]
    public async Task ParseAsync_WhenCalledWithInvalidLines_ShouldIgnoreInvalidLines()
    {
        const string text = """
                            HDR  TRSP117                                                                                     6874453I
                            LINE P000001661                           9781473663800                     12
                            INVALIDLINE something that does not match
                            LINE P000001661                           9781473667273                     2
                            """;

        var boxes = await ParseTextAsync(text);

        boxes.Count.ShouldBe(1);
        boxes[0].Lines.Count.ShouldBe(2);
    }

    private async Task<List<Box>> ParseTextAsync(string text)
    {
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return await _fileParser.ParseAsync(ms, CancellationToken.None).ToListAsync();
    }
}
