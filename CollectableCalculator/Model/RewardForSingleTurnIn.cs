namespace CollectableCalculator.Model;

internal sealed class RewardForSingleTurnIn
{
    public required ItemRef Item { get; init; }
    public required ERewardType RewardType { get; init; }
    public required int QuantityToTurnIn { get; set; }
}
