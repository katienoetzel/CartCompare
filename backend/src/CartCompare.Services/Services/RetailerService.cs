using CartCompare.Entities;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;

namespace CartCompare.Services.Services;

public class RetailerService : IRetailerService
{
    private readonly IRetailerRepository _retailerRepository;

    public RetailerService(IRetailerRepository retailerRepository)
    {
        _retailerRepository = retailerRepository;
    }

    public async Task<List<Retailer>> GetActiveAsync()
    {
        return await _retailerRepository.GetActiveAsync();
    }

    public async Task<Retailer?> GetByIdAsync(int id)
    {
        return await _retailerRepository.GetByIdAsync(id);
    }

    public async Task<Retailer> CreateAsync(
        string name,
        bool supportsMembership)
    {
        var retailer = new Retailer
        {
            Name = name.Trim(),
            SupportsMembership = supportsMembership,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _retailerRepository.AddAsync(retailer);

        return retailer;
    }
}