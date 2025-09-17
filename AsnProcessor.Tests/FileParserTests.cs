using System.Text;
using AsnProcessor.Application.Services;
using Shouldly;

namespace AsnProcessor.Tests;

public class FileParserTests
{
    [Fact]
    public async Task ParseAsync_WhenCalled_ShouldReturnBoxes()
    {
        var fileParser = new FileParser();
        const string text = """
                            HDR  TRSP117                                                                                     6874453I
                            LINE P000001661                           9781473663800                     12
                            LINE P000001661                           9781473667273                     2
                            HDR  TRSP117                                                                                     6874454I
                            LINE G000009810                           9781473662179                     8
                            """;

        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var boxes = new List<AsnProcessor.Domain.Entities.Box>();
        await foreach (var box in fileParser.ParseAsync(ms, CancellationToken.None))
        {
            boxes.Add(box);
        }

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
        var fileParser = new FileParser();
        const string text = """
                            HDR  TRSP117                                                                                     6874453I
                            LINE P000001661                           9781473663800                     12
                            INVALIDLINE something that does not match
                            LINE P000001661                           9781473667273                     2
                            """;

        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var boxes = new List<AsnProcessor.Domain.Entities.Box>();
        await foreach (var box in fileParser.ParseAsync(ms, CancellationToken.None))
        {
            boxes.Add(box);
        }

        boxes.Count.ShouldBe(1);
        boxes[0].Lines.Count.ShouldBe(2);
    }
}
