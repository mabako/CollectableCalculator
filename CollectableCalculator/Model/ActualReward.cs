namespace CollectableCalculator.Model
{
    internal sealed class ActualReward
    {
        public required ItemRef Item { get; init; }
        public required ERewardType RewardType { get; init; }
        public required int QuantityToTurnIn { get; init; }
        public required int QuantityInInventory { get; init; }
    }
}
