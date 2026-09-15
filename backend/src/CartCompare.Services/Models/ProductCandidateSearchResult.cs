namespace CartCompare.Services.Models;

public class ProductCandidateSearchResult
{
    public ProductCandidateSearchResultType Result { get; set; }

    public int ItemId { get; set; }

    public int StoreLocationId { get; set; }

    public string? RetailerName { get; set; }

    public string? ProviderName { get; set; }

    public string? SearchQuery { get; set; }

    public List<ProductCandidate> Candidates { get; set; } =
        new();
}