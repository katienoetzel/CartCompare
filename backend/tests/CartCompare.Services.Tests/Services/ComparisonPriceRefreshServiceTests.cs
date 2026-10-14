using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class ComparisonPriceRefreshServiceTests
{
    private readonly Mock<IPriceService> _prices = new();
    private readonly Mock<IProductCandidateService> _candidates = new();
    private readonly Mock<IProductPriceSyncService> _sync = new();
    private readonly Item _item = new() { Id = 1, Name = "Whole Milk", Size = "1 gallon" };
    private readonly StoreLocation _store = new()
    {
        Id = 2, RetailerId = 3, ExternalLocationId = "store", AddressLine1 = "Main St",
        City = "Raleigh", State = "NC", PostalCode = "27613"
    };
    private readonly RetailerProduct _product = new()
    {
        Id = 4, ItemId = 1, RetailerId = 3, Name = "Whole Milk", ExternalProductId = "milk"
    };
    private ComparisonPriceRefreshService Service => new(_prices.Object, _candidates.Object, _sync.Object);

    private Price Price(DateTime checkedAt, AvailabilityStatus availability = AvailabilityStatus.Available) =>
        new() { RegularPrice = 3m, LastCheckedAt = checkedAt, AvailabilityStatus = availability, SourceProvider = "Test" };

    private ProductCandidate Candidate(string id = "milk", string name = "Harris Teeter Whole Milk") => new()
    {
        ExternalProductId = id, Name = name, Brand = "Harris Teeter", Size = "1 gallon",
        IsMatch = true, MatchMethod = ProductMatchMethod.Deterministic,
        MatchConfidence = 0.9m, MatchReason = "Matching name and size"
    };

    [Fact]
    public async Task FreshPrice_IsReusedWithoutProviderCalls()
    {
        _prices.Setup(service => service.GetByProductAndStoreAsync(4, 2))
            .ReturnsAsync(Price(DateTime.UtcNow.AddMinutes(-10)));
        var result = await Service.RefreshAsync(_item, _store, new() { _product });
        Assert.Contains(4, result.UsableProductIds);
        Assert.Equal("cached", result.Status);
        _sync.VerifyNoOtherCalls();
        _candidates.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrOldPrice_IsRefreshed(bool oldPrice)
    {
        _prices.Setup(service => service.GetByProductAndStoreAsync(4, 2))
            .ReturnsAsync(oldPrice ? Price(DateTime.UtcNow.AddHours(-7)) : null);
        _sync.Setup(service => service.SyncAsync(1, 2, "milk"))
            .ReturnsAsync(new ProductPriceSyncResult
            {
                Result = ProductPriceSyncResultType.Succeeded, RetailerProduct = _product,
                Price = Price(DateTime.UtcNow), PriceSynced = true
            });
        var result = await Service.RefreshAsync(_item, _store, new() { _product });
        Assert.Contains(4, result.UsableProductIds);
        Assert.Equal("refreshed", result.Status);
        _sync.Verify(service => service.SyncAsync(1, 2, "milk"), Times.Once);
    }

    [Fact]
    public async Task RefreshFailure_DoesNotAllowOldPriceIntoComparison()
    {
        _prices.Setup(service => service.GetByProductAndStoreAsync(4, 2))
            .ReturnsAsync(Price(DateTime.UtcNow.AddHours(-7)));
        _sync.Setup(service => service.SyncAsync(1, 2, "milk"))
            .ThrowsAsync(new HttpRequestException("Provider failed"));
        var result = await Service.RefreshAsync(_item, _store, new() { _product });
        Assert.Empty(result.UsableProductIds);
        Assert.Equal("provider_error", result.Status);
    }

    [Fact]
    public async Task FreshUnavailableSnapshot_IsNotRepeatedlyFetched()
    {
        _prices.Setup(service => service.GetByProductAndStoreAsync(4, 2))
            .ReturnsAsync(Price(DateTime.UtcNow, AvailabilityStatus.Unavailable));
        var result = await Service.RefreshAsync(_item, _store, new() { _product });
        Assert.Empty(result.UsableProductIds);
        Assert.Equal("unavailable", result.Status);
        _sync.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SingleSafeMatch_IsSyncedThroughGuardedPath()
    {
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ReturnsAsync(new ProductCandidateSearchResult { Candidates = new() { Candidate() } });
        _sync.Setup(service => service.SyncMatchedAsync(1, 2, "milk"))
            .ReturnsAsync(new ProductPriceSyncResult
            {
                Result = ProductPriceSyncResultType.Succeeded, RetailerProduct = _product,
                Price = Price(DateTime.UtcNow)
            });
        var result = await Service.RefreshAsync(_item, _store, new());
        Assert.Contains(4, result.UsableProductIds);
        _sync.Verify(service => service.SyncMatchedAsync(1, 2, "milk"), Times.Once);
    }

    [Fact]
    public async Task MissingSavedId_SearchesBySavedNameAndRetriesApprovedProduct()
    {
        var rediscovered = Candidate();
        rediscovered.IsMatch = false; // The existing link was already approved.
        _sync.SetupSequence(service => service.SyncAsync(1, 2, "milk"))
            .ReturnsAsync(new ProductPriceSyncResult { Result = ProductPriceSyncResultType.ProviderProductNotFound })
            .ReturnsAsync(new ProductPriceSyncResult
            {
                Result = ProductPriceSyncResultType.Succeeded, RetailerProduct = _product,
                Price = Price(DateTime.UtcNow)
            });
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, "Whole Milk"))
            .ReturnsAsync(new ProductCandidateSearchResult { Candidates = new() { rediscovered } });
        var result = await Service.RefreshAsync(_item, _store, new() { _product });
        Assert.Contains(4, result.UsableProductIds);
        Assert.Equal("refreshed", result.Status);
        _sync.Verify(service => service.SyncAsync(1, 2, "milk"), Times.Exactly(2));
        _sync.Verify(service => service.SyncMatchedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MissingSavedId_UsesGuardedMatchingForReplacement()
    {
        _sync.Setup(service => service.SyncAsync(1, 2, "milk"))
            .ReturnsAsync(new ProductPriceSyncResult { Result = ProductPriceSyncResultType.ProviderProductNotFound });
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, "Whole Milk"))
            .ReturnsAsync(new ProductCandidateSearchResult { Candidates = new() { Candidate("replacement") } });
        _sync.Setup(service => service.SyncMatchedAsync(1, 2, "replacement"))
            .ReturnsAsync(new ProductPriceSyncResult
            {
                Result = ProductPriceSyncResultType.Succeeded,
                RetailerProduct = new RetailerProduct
                {
                    Id = 5, ItemId = 1, RetailerId = 3, Name = "Whole Milk", ExternalProductId = "replacement"
                },
                Price = Price(DateTime.UtcNow)
            });
        var result = await Service.RefreshAsync(_item, _store, new() { _product });
        Assert.DoesNotContain(4, result.UsableProductIds);
        Assert.Contains(5, result.UsableProductIds);
        _sync.Verify(service => service.SyncMatchedAsync(1, 2, "replacement"), Times.Once);
    }

    [Fact]
    public async Task VerifiedUpc_IsPreferredOverTextOnlyCandidates()
    {
        var verified = Candidate("verified");
        verified.MatchMethod = ProductMatchMethod.Upc;
        verified.MatchConfidence = 1m;
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ReturnsAsync(new ProductCandidateSearchResult
            {
                Candidates = new() { Candidate(), verified }
            });
        _sync.Setup(service => service.SyncMatchedAsync(1, 2, "verified"))
            .ReturnsAsync(new ProductPriceSyncResult
            {
                Result = ProductPriceSyncResultType.Succeeded, RetailerProduct = _product,
                Price = Price(DateTime.UtcNow)
            });
        var result = await Service.RefreshAsync(_item, _store, new());
        Assert.Contains(4, result.UsableProductIds);
        _sync.Verify(service => service.SyncMatchedAsync(1, 2, "verified"), Times.Once);
        _sync.Verify(service => service.SyncMatchedAsync(1, 2, "milk"), Times.Never);
    }

    private ProductPriceSyncResult Synced(int productId, string externalId) => new()
    {
        Result = ProductPriceSyncResultType.Succeeded,
        RetailerProduct = new RetailerProduct
        {
            Id = productId, ItemId = 1, RetailerId = 3, Name = "Whole Milk", ExternalProductId = externalId
        },
        Price = Price(DateTime.UtcNow)
    };

    [Fact]
    public async Task MultipleSafeMatches_AreAllLinkedSoTheCheapestCanWin()
    {
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ReturnsAsync(new ProductCandidateSearchResult
            {
                Candidates = new() { Candidate("one"), Candidate("two") }
            });
        _sync.Setup(service => service.SyncMatchedAsync(1, 2, "one")).ReturnsAsync(Synced(5, "one"));
        _sync.Setup(service => service.SyncMatchedAsync(1, 2, "two")).ReturnsAsync(Synced(6, "two"));
        var result = await Service.RefreshAsync(_item, _store, new());
        Assert.Equal(new HashSet<int> { 5, 6 }, result.UsableProductIds);
        Assert.Equal("refreshed", result.Status);
    }

    [Fact]
    public async Task OneFailedSafeMatch_DoesNotHideAnotherUsableMatch()
    {
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ReturnsAsync(new ProductCandidateSearchResult
            {
                Candidates = new() { Candidate("one"), Candidate("two") }
            });
        _sync.Setup(service => service.SyncMatchedAsync(1, 2, "one"))
            .ReturnsAsync(new ProductPriceSyncResult { Result = ProductPriceSyncResultType.PriceNotAvailable });
        _sync.Setup(service => service.SyncMatchedAsync(1, 2, "two")).ReturnsAsync(Synced(6, "two"));
        var result = await Service.RefreshAsync(_item, _store, new());
        Assert.Equal(new HashSet<int> { 6 }, result.UsableProductIds);
        Assert.Equal("refreshed", result.Status);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task TooManySafeMatches_AreLeftForManualSelection()
    {
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ReturnsAsync(new ProductCandidateSearchResult
            {
                Candidates = new() { Candidate("one"), Candidate("two"), Candidate("three"), Candidate("four") }
            });
        var result = await Service.RefreshAsync(_item, _store, new());
        Assert.Equal("ambiguous_match", result.Status);
        Assert.Empty(result.UsableProductIds);
        _sync.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmptySearch_AndFailedSearch_HaveDifferentOutcomes()
    {
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ReturnsAsync(new ProductCandidateSearchResult());
        Assert.Equal("no_match", (await Service.RefreshAsync(_item, _store, new())).Status);
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ThrowsAsync(new TaskCanceledException("Timed out"));
        Assert.Equal("provider_error", (await Service.RefreshAsync(_item, _store, new())).Status);
    }

    [Theory]
    [InlineData("Harris Teeter Chocolate Whole Milk")]
    [InlineData("Harris Teeter Lactose Free Whole Milk")]
    [InlineData("Harris Teeter Organic Whole Milk")]
    public async Task ExtraVariantTerms_AreNotAutomaticallyMatched(string name)
    {
        _candidates.Setup(service => service.FindCandidatesAsync(1, 2, null))
            .ReturnsAsync(new ProductCandidateSearchResult { Candidates = new() { Candidate(name: name) } });
        var result = await Service.RefreshAsync(_item, _store, new());
        Assert.Equal("match_uncertain", result.Status);
        _sync.VerifyNoOtherCalls();
    }
}
