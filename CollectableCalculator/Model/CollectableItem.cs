using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace CollectableCalculator.Model;

internal sealed record CollectableItem(
    ItemRef TurnInItem,
    ItemRef RewardItem1,
    ItemRef? RewardItem2,
    byte LevelForReward2,
    EAvailability Availability,
    ERewardType RewardType,
    Collectability LowCollectability,
    Collectability MidCollectability,
    Collectability HighCollectability,
    List<EClassJob> TurnInJobs
)
{
    private IEnumerable<Collectability> Collectabilities
        => new List<Collectability> { HighCollectability, MidCollectability, LowCollectability }
            .Where(c => c.MinimumQuality != 0);

    public AllRewardsForSingleTurnIn? FindByCollectability(ushort quality)
    {
        return Collectabilities.Where(c => quality >= c.MinimumQuality)
            .Select(c =>
            {
                var reward1 = new RewardForSingleTurnIn
                {
                    Item = RewardItem1,
                    RewardType = RewardType,
                    QuantityToTurnIn = c.Quantity1,
                };

                var reward2 = RewardItem2 != null
                    ? new RewardForSingleTurnIn
                    {
                        Item = RewardItem2,
                        RewardType = RewardType,
                        QuantityToTurnIn = c.Quantity2,
                    }
                    : null;

                return new AllRewardsForSingleTurnIn(TurnInItem.Id, reward1, reward2, LevelForReward2, TurnInJobs, Availability);
            })
            .FirstOrDefault();
    }
}
