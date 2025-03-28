using System.Collections.Generic;

namespace CollectableCalculator.Model;

internal sealed class ConditionalReward
{
    public required ItemRef Item { get; init; }
    public required ERewardType RewardType { get; init; }
    public required int MinimumRewardQuantity { get; init; }
    public required int MaximumRewardQuantity { get; init; }
    public required Dictionary<EClassJob, int> TurnInQuantityPerJob { get; init; }
    public required int QuantityInInventory { get; init; }
    public required byte RequiredLevelForMaximumReward { get; set; }

    public bool HasOptionalRewards() => MinimumRewardQuantity != MaximumRewardQuantity;
}
