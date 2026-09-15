namespace CartCompare.Services.Models;

public class MultiStoreComparisonResult
{
    public List<StoreComparisonResult> Stores { get; set; }
        = new List<StoreComparisonResult>();

    public List<int> MissingStoreLocationIds { get; set; }
        = new List<int>();

    public int CompleteStoreCount { get; set; }

    public int IncompleteStoreCount { get; set; }
}