using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IProductCandidateService
{
    Task<ProductCandidateSearchResult> FindCandidatesAsync(
        int itemId,
        int storeLocationId,
        string? query = null
    );
}