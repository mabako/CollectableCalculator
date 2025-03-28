using System.Collections.Generic;

namespace CollectableCalculator.Model;

/// <summary>
/// A single item, with multiple rewards (e.g. for weekly deliveries giving both purple and orange scrips).
/// </summary>
internal sealed record AllRewardsForSingleTurnIn(
    uint TurnInItemId,
    RewardForSingleTurnIn Reward1,
    RewardForSingleTurnIn? Reward2,
    byte LevelForReward2,
    List<EClassJob> TurnInJobs,
    EAvailability Availability)
{
    public SingleReward ToReward1(ushort collectability) =>
        new(TurnInItemId, Reward1, 0, TurnInJobs, Availability, collectability);

    public bool HasReward2 => Reward2 != null;

    public SingleReward? ToReward2(ushort collectability)
    {
        if (Reward2 == null)
            return null;

        return new SingleReward(TurnInItemId, Reward2, LevelForReward2, TurnInJobs, Availability, collectability);
    }
}
