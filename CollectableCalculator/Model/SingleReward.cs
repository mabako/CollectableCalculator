using System.Collections.Generic;

namespace CollectableCalculator.Model;

/// <summary>
/// A single item can be mapped to multiple rewards (such as weekly deliveries having a purple + orange scrip reward),
/// this class is a single reward (i.e. only the purple scrip reward).
/// </summary>
internal sealed record SingleReward(
    uint TurnInItemId,
    RewardForSingleTurnIn Reward,
    byte RequiredLevel,
    List<EClassJob> TurnInJobs,
    EAvailability Availability,
    ushort Collectability);
