namespace CartCompare.Entities;

public class SavedItem
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ItemId { get; set; }

    public DateTime CreatedAt { get; set; }
}