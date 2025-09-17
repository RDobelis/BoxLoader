namespace AsnProcessor.Domain.Entities;

public class BoxLine
{
    public int Id { get; set; }
    public int BoxId { get; set; }
    public string PoNumber { get; set; } = null!;
    public string Isbn { get; set; } = null!;
    public int Quantity { get; set; }
}