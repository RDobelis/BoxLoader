namespace AsnProcessor.Domain.Entities;

public class Box
{
    public int Id { get; set; }
    public string SupplierIdentifier { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public ICollection<BoxLine> Lines { get; set; } = new List<BoxLine>();
}