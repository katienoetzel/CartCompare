using CartCompare.Entities;

namespace CartCompare.Services.Interfaces;

public interface IRetailerService
{
    Task<List<Retailer>> GetActiveAsync();

    Task<Retailer?> GetByIdAsync(int id);

    Task<Retailer> CreateAsync(
        string name,
        bool supportsMembership
    );
}