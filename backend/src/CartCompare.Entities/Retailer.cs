namespace CartCompare.Entities;

public class Retailer
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public bool SupportsMembership { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}