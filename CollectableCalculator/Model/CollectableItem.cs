using System.Collections.Generic;
using System.Linq;

namespace CollectableCalculator.Model
{
    internal sealed class CollectableItem
    {
        public required ItemRef TurnInItem { get; init; }
        public required ItemRef RewardItem { get; init; }

        public required ERewardType RewardType { get; init; }

        public required Collectability LowCollectability { get; init; }
        public required Collectability MidCollectability { get; init; }
        public required Collectability HighCollectability { get; init; }

        private IEnumerable<Collectability> Collectabilities
            => new List<Collectability> { HighCollectability, MidCollectability, LowCollectability }
                .Where(c => c.MinimumQuality != 0);

        public ActualReward? FindByCollectability(ushort quality)
        {
            return Collectabilities.Where(c => quality >= c.MinimumQuality)
                .Select(c => new ActualReward
                {
                    Item = RewardItem,
                    RewardType = RewardType,
                    QuantityToTurnIn = c.Quantity,
                    QuantityInInventory = 0
                })
                .FirstOrDefault();
        }
    }
}
