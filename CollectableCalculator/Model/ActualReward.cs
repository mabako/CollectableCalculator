namespace CollectableCalculator.Model
{
    internal sealed class ActualReward
    {
        public required ItemRef Item { get; init; }
        public required int Quantity { get; init; }
    }
}