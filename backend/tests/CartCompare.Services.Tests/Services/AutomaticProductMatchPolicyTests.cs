using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Services.Models;
using CartCompare.Services.Services;

namespace CartCompare.Services.Tests.Services;

public class AutomaticProductMatchPolicyTests
{
    private readonly Item _item = new() { Id = 1, Name = "Whole Milk", Size = "1 gallon" };

    private static ProductCandidate Candidate(
        string? size = "1 gal",
        bool isMatch = true,
        ProductMatchMethod? method = ProductMatchMethod.Deterministic,
        decimal? confidence = 0.9m) => new()
    {
        ExternalProductId = "milk", Name = "Lowes Foods Grade A Vitamin D Whole Milk", Size = size,
        IsMatch = isMatch, MatchMethod = method, MatchConfidence = confidence,
        MatchReason = "Matching name and size"
    };

    [Fact]
    public void DeterministicMatchWithSizes_IsSafeEvenWithExtraWords()
    {
        Assert.True(AutomaticProductMatchPolicy.IsSafe(_item, Candidate()));
    }

    [Fact]
    public void VerifiedUpc_IsSafe()
    {
        Assert.True(AutomaticProductMatchPolicy.IsSafe(
            _item, Candidate(size: null, method: ProductMatchMethod.Upc, confidence: 1m)));
    }

    [Fact]
    public void MatcherRejection_IsNotSafe()
    {
        Assert.False(AutomaticProductMatchPolicy.IsSafe(_item, Candidate(isMatch: false)));
    }

    [Fact]
    public void LowConfidence_IsNotSafe()
    {
        Assert.False(AutomaticProductMatchPolicy.IsSafe(_item, Candidate(confidence: 0.8m)));
    }

    [Fact]
    public void MissingSize_IsNotSafe()
    {
        Assert.False(AutomaticProductMatchPolicy.IsSafe(_item, Candidate(size: null)));
        Assert.False(AutomaticProductMatchPolicy.IsSafe(
            new Item { Id = 1, Name = "Whole Milk" }, Candidate()));
    }
}
