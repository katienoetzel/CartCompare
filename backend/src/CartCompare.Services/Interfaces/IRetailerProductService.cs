using CartCompare.Entities;
using CartCompare.Services.Models;

namespace CartCompare.Services.Interfaces;

public interface IRetailerProductService
{
    Task<RetailerProduct?> GetByIdAsync(int id);

    Task<List<RetailerProduct>> GetByRetailerIdAsync(
        int retailerId
    );

    Task<List<RetailerProduct>> GetByItemIdAsync(
        int itemId
    );

    Task<CreateRetailerProductResult> CreateAsync(
        int itemId,
        int retailerId,
        string externalProductId,
        string name,
        string? brand,
        string? size,
        string? upc
    );
}