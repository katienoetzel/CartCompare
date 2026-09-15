using CartCompare.Entities;

namespace CartCompare.Services.Models;

public class CreateStoreLocationResult
{
    public StoreLocationCreateResult Result { get; set; }

    public StoreLocation? StoreLocation { get; set; }
}