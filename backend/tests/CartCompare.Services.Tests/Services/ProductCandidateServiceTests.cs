using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Providers.Interfaces;
using CartCompare.Providers.Models;
using CartCompare.Repositories.Interfaces;
using CartCompare.Services.Interfaces;
using CartCompare.Services.Models;
using CartCompare.Services.Services;
using Moq;

namespace CartCompare.Services.Tests.Services;

public class ProductCandidateServiceTests
{
    private readonly Mock<IItemRepository>
        _itemRepositoryMock;

    private readonly Mock<IStoreLocationRepository>
        _storeLocationRepositoryMock;

    private readonly Mock<IRetailerRepository>
        _retailerRepositoryMock;

    private readonly Mock<IPriceProviderResolver>
        _providerResolverMock;

    private readonly Mock<IPriceProvider>
        _providerMock;

    private readonly Mock<IProductMatchingService>
        _productMatchingServiceMock;

    private readonly ProductCandidateService
        _service;

    public ProductCandidateServiceTests()
    {
        _itemRepositoryMock =
            new Mock<IItemRepository>();

        _storeLocationRepositoryMock =
            new Mock<IStoreLocationRepository>();

        _retailerRepositoryMock =
            new Mock<IRetailerRepository>();

        _providerResolverMock =
            new Mock<IPriceProviderResolver>();

        _providerMock =
            new Mock<IPriceProvider>();

        _productMatchingServiceMock =
            new Mock<IProductMatchingService>();

        _service =
            new ProductCandidateService(
                _itemRepositoryMock.Object,
                _storeLocationRepositoryMock.Object,
                _retailerRepositoryMock.Object,
                _providerResolverMock.Object,
                _productMatchingServiceMock.Object
            );
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenItemDoesNotExist_ReturnsItemNotFound()
    {
        // Arrange
        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(1)
            )
            .ReturnsAsync((Item?)null);

        // Act
        var result =
            await _service.FindCandidatesAsync(
                1,
                10
            );

        // Assert
        Assert.Equal(
            ProductCandidateSearchResultType.ItemNotFound,
            result.Result
        );

        Assert.Equal(
            1,
            result.ItemId
        );

        Assert.Equal(
            10,
            result.StoreLocationId
        );

        _storeLocationRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenStoreDoesNotExist_ReturnsStoreLocationNotFound()
    {
        // Arrange
        var item =
            CreateItem();

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(10)
            )
            .ReturnsAsync(
                (StoreLocation?)null
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                10
            );

        // Assert
        Assert.Equal(
            ProductCandidateSearchResultType
                .StoreLocationNotFound,
            result.Result
        );

        _retailerRepositoryMock.Verify(
            repository =>
                repository.GetByIdAsync(
                    It.IsAny<int>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenRetailerDoesNotExist_ReturnsRetailerNotFound()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.Id
                )
            )
            .ReturnsAsync(store);

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.RetailerId
                )
            )
            .ReturnsAsync(
                (Retailer?)null
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id
            );

        // Assert
        Assert.Equal(
            ProductCandidateSearchResultType
                .RetailerNotFound,
            result.Result
        );

        _providerResolverMock.Verify(
            resolver =>
                resolver.Resolve(
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenProviderDoesNotExist_ReturnsProviderNotFound()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        SetupBaseData(
            item,
            store,
            retailer
        );

        _providerResolverMock
            .Setup(resolver =>
                resolver.Resolve(
                    retailer.Name
                )
            )
            .Returns(
                (IPriceProvider?)null
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id
            );

        // Assert
        Assert.Equal(
            ProductCandidateSearchResultType
                .ProviderNotFound,
            result.Result
        );

        Assert.Equal(
            retailer.Name,
            result.RetailerName
        );

        _providerMock.Verify(
            provider =>
                provider.SearchProductsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenQueryIsMissing_UsesTrimmedItemName()
    {
        // Arrange
        var item =
            CreateItem(
                name: "  Whole Milk  "
            );

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        SetupSuccessfulBase(
            item,
            store,
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    "Whole Milk"
                )
            )
            .ReturnsAsync(
                new List<ProviderProduct>()
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id
            );

        // Assert
        Assert.Equal(
            ProductCandidateSearchResultType.Succeeded,
            result.Result
        );

        Assert.Equal(
            "Whole Milk",
            result.SearchQuery
        );

        _providerMock.Verify(
            provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    "Whole Milk"
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenCustomQueryIsProvided_UsesTrimmedCustomQuery()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        SetupSuccessfulBase(
            item,
            store,
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    "Kroger vitamin d milk"
                )
            )
            .ReturnsAsync(
                new List<ProviderProduct>()
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id,
                "  Kroger vitamin d milk  "
            );

        // Assert
        Assert.Equal(
            ProductCandidateSearchResultType.Succeeded,
            result.Result
        );

        Assert.Equal(
            "Kroger vitamin d milk",
            result.SearchQuery
        );

        _providerMock.Verify(
            provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    "Kroger vitamin d milk"
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_MapsProviderProductAndMatchResultToCandidate()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var providerProduct =
            CreateProviderProduct(
                externalProductId: "PRODUCT-123",
                name: "Kroger Whole Milk"
            );

        SetupSuccessfulBase(
            item,
            store,
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    item.Name
                )
            )
            .ReturnsAsync(
                new List<ProviderProduct>
                {
                    providerProduct
                }
            );

        _productMatchingServiceMock
            .Setup(service =>
                service.EvaluateAsync(
                    item,
                    providerProduct
                )
            )
            .ReturnsAsync(
                new ProductMatchResult
                {
                    IsMatch = true,

                    MatchMethod =
                        ProductMatchMethod.Deterministic,

                    MatchConfidence =
                        0.90m,

                    Reason =
                        "Strong deterministic match."
                }
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id
            );

        // Assert
        Assert.Single(
            result.Candidates
        );

        var candidate =
            result.Candidates[0];

        Assert.Equal(
            providerProduct.ExternalProductId,
            candidate.ExternalProductId
        );

        Assert.Equal(
            providerProduct.Name,
            candidate.Name
        );

        Assert.Equal(
            providerProduct.Brand,
            candidate.Brand
        );

        Assert.Equal(
            providerProduct.Size,
            candidate.Size
        );

        Assert.Equal(
            providerProduct.Upc,
            candidate.Upc
        );

        Assert.True(
            candidate.IsMatch
        );

        Assert.Equal(
            ProductMatchMethod.Deterministic,
            candidate.MatchMethod
        );

        Assert.Equal(
            0.90m,
            candidate.MatchConfidence
        );

        Assert.Equal(
            "Strong deterministic match.",
            candidate.MatchReason
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_EvaluatesEveryProviderProduct()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var firstProduct =
            CreateProviderProduct(
                "PRODUCT-1",
                "Whole Milk"
            );

        var secondProduct =
            CreateProviderProduct(
                "PRODUCT-2",
                "Reduced Fat Milk"
            );

        var thirdProduct =
            CreateProviderProduct(
                "PRODUCT-3",
                "Chocolate Milk"
            );

        var providerProducts =
            new List<ProviderProduct>
            {
                firstProduct,
                secondProduct,
                thirdProduct
            };

        SetupSuccessfulBase(
            item,
            store,
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    item.Name
                )
            )
            .ReturnsAsync(
                providerProducts
            );

        _productMatchingServiceMock
            .Setup(service =>
                service.EvaluateAsync(
                    item,
                    It.IsAny<ProviderProduct>()
                )
            )
            .ReturnsAsync(
                new ProductMatchResult
                {
                    IsMatch = false,
                    MatchMethod = null,
                    MatchConfidence = null,
                    Reason = "No match."
                }
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id
            );

        // Assert
        Assert.Equal(
            3,
            result.Candidates.Count
        );

        _productMatchingServiceMock.Verify(
            service =>
                service.EvaluateAsync(
                    item,
                    It.IsAny<ProviderProduct>()
                ),
            Times.Exactly(3)
        );

        _productMatchingServiceMock.Verify(
            service =>
                service.EvaluateAsync(
                    item,
                    firstProduct
                ),
            Times.Once
        );

        _productMatchingServiceMock.Verify(
            service =>
                service.EvaluateAsync(
                    item,
                    secondProduct
                ),
            Times.Once
        );

        _productMatchingServiceMock.Verify(
            service =>
                service.EvaluateAsync(
                    item,
                    thirdProduct
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_SortsMatchesByMatchStatusConfidenceThenName()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        var lowerConfidenceZeta =
            CreateProviderProduct(
                "PRODUCT-1",
                "Zeta Whole Milk"
            );

        var highestConfidence =
            CreateProviderProduct(
                "PRODUCT-2",
                "Premium Whole Milk"
            );

        var nonMatch =
            CreateProviderProduct(
                "PRODUCT-3",
                "Chocolate Milk"
            );

        var lowerConfidenceAlpha =
            CreateProviderProduct(
                "PRODUCT-4",
                "Alpha Whole Milk"
            );

        SetupSuccessfulBase(
            item,
            store,
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    item.Name
                )
            )
            .ReturnsAsync(
                new List<ProviderProduct>
                {
                    nonMatch,
                    lowerConfidenceZeta,
                    highestConfidence,
                    lowerConfidenceAlpha
                }
            );

        SetupMatchResult(
            item,
            lowerConfidenceZeta,
            isMatch: true,
            confidence: 0.90m
        );

        SetupMatchResult(
            item,
            highestConfidence,
            isMatch: true,
            confidence: 0.98m
        );

        SetupMatchResult(
            item,
            nonMatch,
            isMatch: false,
            confidence: null
        );

        SetupMatchResult(
            item,
            lowerConfidenceAlpha,
            isMatch: true,
            confidence: 0.90m
        );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id
            );

        // Assert
        Assert.Equal(
            4,
            result.Candidates.Count
        );

        Assert.Equal(
            "PRODUCT-2",
            result.Candidates[0]
                .ExternalProductId
        );

        // Same confidence, so alphabetical
        // name ordering decides these two.
        Assert.Equal(
            "PRODUCT-4",
            result.Candidates[1]
                .ExternalProductId
        );

        Assert.Equal(
            "PRODUCT-1",
            result.Candidates[2]
                .ExternalProductId
        );

        // Non-match comes after every match.
        Assert.Equal(
            "PRODUCT-3",
            result.Candidates[3]
                .ExternalProductId
        );
    }

    [Fact]
    public async Task FindCandidatesAsync_WhenProviderReturnsNoProducts_ReturnsSuccessfulEmptyCandidateList()
    {
        // Arrange
        var item =
            CreateItem();

        var store =
            CreateStore();

        var retailer =
            CreateRetailer();

        SetupSuccessfulBase(
            item,
            store,
            retailer
        );

        _providerMock
            .Setup(provider =>
                provider.SearchProductsAsync(
                    retailer.Name,
                    store.ExternalLocationId,
                    item.Name
                )
            )
            .ReturnsAsync(
                new List<ProviderProduct>()
            );

        // Act
        var result =
            await _service.FindCandidatesAsync(
                item.Id,
                store.Id
            );

        // Assert
        Assert.Equal(
            ProductCandidateSearchResultType.Succeeded,
            result.Result
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.Equal(
            item.Id,
            result.ItemId
        );

        Assert.Equal(
            store.Id,
            result.StoreLocationId
        );

        Assert.Equal(
            retailer.Name,
            result.RetailerName
        );

        Assert.Equal(
            "KrogerPublicApi",
            result.ProviderName
        );

        _productMatchingServiceMock.Verify(
            service =>
                service.EvaluateAsync(
                    It.IsAny<Item>(),
                    It.IsAny<ProviderProduct>()
                ),
            Times.Never
        );
    }

    private void SetupBaseData(
        Item item,
        StoreLocation store,
        Retailer retailer)
    {
        _itemRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    item.Id
                )
            )
            .ReturnsAsync(item);

        _storeLocationRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.Id
                )
            )
            .ReturnsAsync(store);

        _retailerRepositoryMock
            .Setup(repository =>
                repository.GetByIdAsync(
                    store.RetailerId
                )
            )
            .ReturnsAsync(retailer);
    }

    private void SetupSuccessfulBase(
        Item item,
        StoreLocation store,
        Retailer retailer)
    {
        SetupBaseData(
            item,
            store,
            retailer
        );

        _providerMock
            .SetupGet(provider =>
                provider.ProviderName
            )
            .Returns(
                "KrogerPublicApi"
            );

        _providerResolverMock
            .Setup(resolver =>
                resolver.Resolve(
                    retailer.Name
                )
            )
            .Returns(
                _providerMock.Object
            );
    }

    private void SetupMatchResult(
        Item item,
        ProviderProduct providerProduct,
        bool isMatch,
        decimal? confidence)
    {
        _productMatchingServiceMock
            .Setup(service =>
                service.EvaluateAsync(
                    item,
                    providerProduct
                )
            )
            .ReturnsAsync(
                new ProductMatchResult
                {
                    IsMatch =
                        isMatch,

                    MatchMethod =
                        isMatch
                            ? ProductMatchMethod
                                .Deterministic
                            : null,

                    MatchConfidence =
                        confidence,

                    Reason =
                        isMatch
                            ? "Match."
                            : "No match."
                }
            );
    }

    private static Item CreateItem(
        int id = 1,
        string name = "Whole Milk")
    {
        return new Item
        {
            Id = id,
            Name = name,
            Size = "1 gallon",
            IsActive = true
        };
    }

    private static StoreLocation
        CreateStore()
    {
        return new StoreLocation
        {
            Id = 10,

            RetailerId = 1,

            ExternalLocationId =
                "KROGER-STORE-123",

            Name =
                "Test Kroger",

            AddressLine1 =
                "123 Test Street",

            City =
                "Cincinnati",

            State =
                "OH",

            PostalCode =
                "45202",

            IsActive =
                true
        };
    }

    private static Retailer
        CreateRetailer()
    {
        return new Retailer
        {
            Id = 1,

            Name =
                "Kroger",

            SupportsMembership =
                true,

            IsActive =
                true
        };
    }

    private static ProviderProduct
        CreateProviderProduct(
            string externalProductId,
            string name)
    {
        return new ProviderProduct
        {
            ExternalProductId =
                externalProductId,

            Name =
                name,

            Brand =
                "Kroger",

            Size =
                "1 gal",

            Upc =
                $"UPC-{externalProductId}"
        };
    }
}