using CartCompare.Services.Services;

namespace CartCompare.Services.Tests.Services;

public class ProductSizeTests
{
    [Theory]
    [InlineData("1 lb", "16 oz")]
    [InlineData("1 lb", "4 ct / 16 oz")]
    [InlineData("2 lb", "32 oz")]
    [InlineData("1 gallon", "128 fl oz")]
    [InlineData("1 gallon", "1 gal")]
    [InlineData("1/2 gallon", "half gallon")]
    [InlineData("1/2 gal", "64 fl. oz")]
    [InlineData("12 count", "1 dozen")]
    [InlineData("12 count", "Dozen")]
    [InlineData("12 oz", "12 ounces")]
    [InlineData("9.6oz", "9.6 oz")]
    [InlineData("per lb", "per lb")]
    [InlineData("per lb", "1.25 lb")]
    [InlineData("per lb", "18 oz")]
    public void Matches_WhenSizesAreEquivalent(string itemSize, string productSize)
    {
        Assert.True(ProductSize.Matches(itemSize, productSize));
    }

    [Theory]
    [InlineData("1 lb", "8 oz")]
    [InlineData("1 gallon", "1/2 gal")]
    [InlineData("1 gallon", "1 lb")]
    [InlineData("12 count", "18 ct")]
    [InlineData("1 lb", "per lb")]
    [InlineData("per lb", "4 ct")]
    [InlineData("per lb", null)]
    public void DoesNotMatch_WhenAmountsOrDimensionsDiffer(string itemSize, string? productSize)
    {
        Assert.False(ProductSize.Matches(itemSize, productSize));
    }

    [Theory]
    [InlineData("1.25 lb", 1.25)]
    [InlineData("18 oz", 1.125)]
    [InlineData("4 ct / 16 oz", 1)]
    public void Pounds_ReadsThePackageWeight(string size, decimal pounds)
    {
        Assert.Equal(pounds, ProductSize.Pounds(size));
    }

    [Theory]
    [InlineData("4 ct")]
    [InlineData("per lb")]
    [InlineData(null)]
    public void Pounds_IsNullWithoutAWeight(string? size)
    {
        Assert.Null(ProductSize.Pounds(size));
    }
}
