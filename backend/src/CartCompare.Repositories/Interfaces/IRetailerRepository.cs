using CartCompare.Entities;

namespace CartCompare.Repositories.Interfaces;

public interface IRetailerRepository
{
    Task<List<Retailer>> GetActiveAsync();

    Task<Retailer?> GetByIdAsync(int id);

    Task AddAsync(Retailer retailer);
}