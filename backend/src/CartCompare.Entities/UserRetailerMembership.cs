namespace CartCompare.Entities;

public class UserRetailerMembership
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int RetailerId { get; set; }

    public DateTime CreatedAt { get; set; }
}