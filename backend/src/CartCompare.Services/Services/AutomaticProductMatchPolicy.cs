using CartCompare.Entities;
using CartCompare.Entities.Enums;
using CartCompare.Services.Models;

namespace CartCompare.Services.Services;

public static class AutomaticProductMatchPolicy
{
    // A product may be linked without a person checking it when the
    // matcher found strong evidence: a known UPC, or every item word,
    // the same size and no variant word (organic, lactose free, ...).
    // Extra words such as a brand or "Grade A" are fine; when several
    // products qualify, the comparison prices the cheapest.
    public static bool IsSafe(Item item, ProductCandidate candidate)
    {
        if (!candidate.IsMatch) return false;
        if (candidate.MatchMethod == ProductMatchMethod.Upc &&
            candidate.MatchConfidence == 1m) return true;

        // Without a size on both sides, "Whole Milk" could be a half pint.
        return candidate.MatchMethod == ProductMatchMethod.Deterministic &&
            candidate.MatchConfidence >= 0.90m &&
            !string.IsNullOrWhiteSpace(item.Size) &&
            !string.IsNullOrWhiteSpace(candidate.Size);
    }
}
