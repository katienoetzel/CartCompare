namespace CartCompare.Api.Models.Requests;

public class CreateRetailerRequest
{
    public required string Name { get; set; }

    public bool SupportsMembership { get; set; }
}