using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Services.Models;
using CartCompare.Services.Services;

namespace CartCompare.Services.Tests.Services;

public class AutomaticProductMatchPolicyTests
{
    private readonly Item _item = new() { Id = 1, Name = "Whole Milk", Size = "1 gallon" };

    private static ProductCandidate Candidate(string name, string brand, string size = "1 gal") => new()
    {
        ExternalProductId = "milk", Name = name, Brand = brand, Size = size,
        IsMatch = true, MatchMethod = ProductMatchMethod.Deterministic, MatchConfidence = 0.9m,
        MatchReason = "Matching name and size"
    };

    [Theory]
    [InlineData("Harris Teeter™ Vitamin D Whole Milk Gallon", "Harris Teeter")]
    [InlineData("Highland Crest Vitamin D Whole Milk", "Highland Crest")]
    [InlineData("Lowes Foods Vitamin D Whole Milk 1 gal", "Lowes Foods")]
    [InlineData("Harris Teeter Whole Milk", "Harris Teeter")]
    public void VitaminDAndSpelledOutUnits_AreSafe(string name, string brand)
    {
        Assert.True(AutomaticProductMatchPolicy.IsSafe(_item, Candidate(name, brand)));
    }

    [Theory]
    [InlineData("Harris Teeter Chocolate Whole Milk")]
    [InlineData("Harris Teeter Lactose Free Vitamin D Whole Milk")]
    [InlineData("Harris Teeter Whole Milk Gallon Jug")]
    [InlineData("Harris Teeter Vitamin A Whole Milk")]
    public void VariantsAndUnexplainedTerms_AreRejected(string name)
    {
        Assert.False(AutomaticProductMatchPolicy.IsSafe(_item, Candidate(name, "Harris Teeter")));
    }

    [Fact]
    public void UnitWord_MustMatchTheCandidateSize()
    {
        Assert.False(AutomaticProductMatchPolicy.IsSafe(
            _item, Candidate("Harris Teeter Whole Milk Gallon", "Harris Teeter", "64 fl oz")));
    }
}
