using System;
using System.Collections.Generic;
using System.Linq;
using CollectableCalculator.Model;
using CollectableCalculator.Windows;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace CollectableCalculator;

internal sealed class Calculator
{
    private readonly IReadOnlyList<InventoryType> _inventoryTypes = new List<InventoryType>
    {
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.SaddleBag1,
        InventoryType.SaddleBag2,
        InventoryType.PremiumSaddleBag1,
        InventoryType.PremiumSaddleBag2,
    }.AsReadOnly();

    private const byte MaxLevel = 100;

    private readonly IDataManager _dataManager;
    private readonly IClientState _clientState;
    private readonly ItemWindow _itemWindow;
    private readonly IPluginLog _pluginLog;
    private readonly Calculations _calculations;

    private Dictionary<uint, CollectableItem> _collectableItems = new();
    private long _lastUpdate;

    public Calculator(IDataManager dataManager, IClientState clientState, ItemWindow itemWindow, IPluginLog pluginLog)
    {
        _dataManager = dataManager;
        _clientState = clientState;
        _itemWindow = itemWindow;
        _pluginLog = pluginLog;
        _calculations = new(dataManager);
    }

    public void Update(IFramework framework)
    {
        long now = Environment.TickCount64;
        if (now - _lastUpdate < 200)
            return;

        _lastUpdate = now;

        try
        {
            if (_collectableItems.Count == 0)
                InitializeData();

            var sums = CalculateInventorySums();
            _itemWindow.Update(sums);
        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "Could not perform calculation update");
        }
    }

    private void InitializeData()
    {
        var itemRewards = _dataManager.GetExcelSheet<CollectablesShopRewardItem>()
            .ToDictionary(c => c.RowId, c =>
                new ShopReward
                {
                    RewardType = ERewardType.Item,
                    RewardItem = c.Item.RowId,
                    LowQuantity = c.RewardLow,
                    MidQuantity = c.RewardMid,
                    HighQuantity = c.RewardHigh,
                });

        var scripRewards = _dataManager.GetExcelSheet<CollectablesShopRewardScrip>()
            .ToDictionary(c => c.RowId, c =>
                new ShopReward
                {
                    RewardType = ERewardType.Scrips,
                    RewardItem = GetItemFromCurrency(c.Currency),
                    LowQuantity = c.LowReward,
                    MidQuantity = c.MidReward,
                    HighQuantity = c.HighReward,
                });

        // this is a pretty hack-ish way to determine scrip/item shops, but there's no consistent value to map all
        // (`Key` seems to be the most likely in the ShopItem sheet, but that's inaccurate for Oddly Specific mats)
        var shopRewards = _dataManager.GetExcelSheet<CollectablesShopItemGroup>(ClientLanguage.English)
            .Where(c => c.RowId != 0 && !string.IsNullOrEmpty(c.Name.ToString()))
            .ToDictionary(c => c.RowId,
                c => c.Name.ToString().StartsWith("Lv.") ? scripRewards : itemRewards);

        var regularShopItems = _dataManager.GetSubrowExcelSheet<CollectablesShopItem>()
            .Flatten()
            .Where(row =>
                row.RowId != 0 && row.CollectablesShopRefine.RowId != 0 && row.CollectablesShopItemGroup.RowId != 0)
            .Select(row =>
            {
                if (!shopRewards.TryGetValue(row.CollectablesShopItemGroup.RowId, out var shop))
                    return null;

                if (!shop.TryGetValue(row.CollectablesShopRewardScrip.RowId, out var reward))
                    return null;

                _pluginLog.Verbose($"Handling {row.RowId}.{row.SubrowId} -> {row.Item.RowId}");

                CollectablesShopRefine? refine = row.CollectablesShopRefine.ValueNullable;
                return new CollectableItem
                (
                    TurnInItem: new ItemRef
                    {
                        Id = row.Item.RowId,
                        Name = GetItemName(row.Item.RowId),
                        IconId = GetIconId(row.Item.RowId),
                    },
                    LevelForReward2: byte.MaxValue,
                    Availability: EAvailability.Always,
                    RewardType: reward.RewardType,
                    RewardItem1: new ItemRef
                    {
                        Id = reward.RewardItem,
                        Name = GetItemName(reward.RewardItem),
                        IconId = GetIconId(reward.RewardItem),
                    },
                    RewardItem2: null,
                    LowCollectability: new Collectability
                    {
                        MinimumQuality = refine?.LowCollectability ?? 0,
                        Quantity1 = reward.LowQuantity,
                        Quantity2 = 0,
                    },
                    MidCollectability: new Collectability
                    {
                        MinimumQuality = refine?.MidCollectability ?? 0,
                        Quantity1 = reward.MidQuantity,
                        Quantity2 = 0,
                    },
                    HighCollectability: new Collectability
                    {
                        MinimumQuality = refine?.HighCollectability ?? 0,
                        Quantity1 = reward.HighQuantity,
                        Quantity2 = 0,
                    },
                    TurnInJobs: DetermineTurnInJob(row.Item.RowId).ToList()
                );
            })
            .Where(c => c != null)
            .Cast<CollectableItem>(); // some stuff like titanium nugget has multiple classes;
        var deliveryItems = _dataManager.GetSubrowExcelSheet<SatisfactionSupply>()
            .Flatten()
            .Where(x => x.RowId != 0 && x.Item.RowId != 0 && x.Reward.RowId != 0)
            .Select(row =>
            {
                SatisfactionSupplyReward.SatisfactionSupplyRewardDataStruct rewardData1 =
                    row.Reward.Value.SatisfactionSupplyRewardData[0];
                uint rewardItem1 = GetItemFromCurrency(rewardData1.RewardCurrency);

                SatisfactionSupplyReward.SatisfactionSupplyRewardDataStruct rewardData2 =
                    row.Reward.Value.SatisfactionSupplyRewardData[1];
                uint rewardItem2 = GetItemFromCurrency(rewardData2.RewardCurrency);

                return new CollectableItem
                (
                    TurnInItem: new ItemRef
                    {
                        Id = row.Item.RowId,
                        Name = GetItemName(row.Item.RowId),
                        IconId = GetIconId(row.Item.RowId),
                    },
                    Availability: EAvailability.WeeklyDelivery,
                    RewardType: ERewardType.Scrips,
                    RewardItem1: new ItemRef
                    {
                        Id = rewardItem1,
                        Name = GetItemName(rewardItem1),
                        IconId = GetIconId(rewardItem1),
                    },
                    RewardItem2: new ItemRef
                    {
                        Id = rewardItem2,
                        Name = GetItemName(rewardItem2),
                        IconId = GetIconId(rewardItem2),
                    },
                    LevelForReward2: row.Reward.Value.MinLevelForSecondReward == 0
                        ? MaxLevel
                        : row.Reward.Value.MinLevelForSecondReward,
                    LowCollectability: new Collectability
                    {
                        MinimumQuality = row.CollectabilityLow,
                        Quantity1 = rewardData1.QuantityLow,
                        Quantity2 = rewardData2.QuantityLow,
                    },
                    MidCollectability: new Collectability
                    {
                        MinimumQuality = row.CollectabilityMid,
                        Quantity1 = rewardData1.QuantityMid,
                        Quantity2 = rewardData2.QuantityMid,
                    },
                    HighCollectability: new Collectability
                    {
                        MinimumQuality = row.CollectabilityHigh,
                        Quantity1 = rewardData1.QuantityHigh,
                        Quantity2 = rewardData2.QuantityHigh,
                    },
                    TurnInJobs: DetermineTurnInJob(row.Item.RowId).ToList()
                );
            });
        _collectableItems = regularShopItems.Concat(deliveryItems)
            .GroupBy(x => x.TurnInItem.Id)
            .ToDictionary(
                c => c.Key,
                x => x.First() with { TurnInJobs = x.SelectMany(y => y.TurnInJobs).Distinct().ToList() });
    }

    private IEnumerable<EClassJob> DetermineTurnInJob(uint itemId)
    {
        var recipeLookup = _dataManager.GetExcelSheet<RecipeLookup>()
            .GetRowOrDefault(itemId);
        if (recipeLookup != null)
        {
            var v = recipeLookup.Value;
            if (v.CRP.RowId != 0)
                yield return EClassJob.Carpenter;

            if (v.BSM.RowId != 0)
                yield return EClassJob.Blacksmith;

            if (v.ARM.RowId != 0)
                yield return EClassJob.Armorer;

            if (v.GSM.RowId != 0)
                yield return EClassJob.Goldsmith;

            if (v.LTW.RowId != 0)
                yield return EClassJob.Leatherworker;

            if (v.WVR.RowId != 0)
                yield return EClassJob.Weaver;

            if (v.ALC.RowId != 0)
                yield return EClassJob.Alchemist;

            if (v.CUL.RowId != 0)
                yield return EClassJob.Culinarian;

            yield break;
        }

        FishingNoteInfo? fish = _dataManager.GetExcelSheet<FishingNoteInfo>()
            .Cast<FishingNoteInfo?>()
            .Where(x => x is { RowId: not 0, IsCollectable: 1 })
            .FirstOrDefault(x => x!.Value.Item.RowId == itemId);
        if (fish != null)
        {
            yield return EClassJob.Fisher;
            yield break;
        }

        var gatheringPoints = _dataManager.GetExcelSheet<GatheringPointBase>()
            .Where(x => x.Item.Any(y => y.RowId > 0 && y.RowId == itemId))
            .ToList();
        if (gatheringPoints.Any(x => x.GatheringType.RowId is 0 or 2))
            yield return EClassJob.Miner;

        if (gatheringPoints.Any(x => x.GatheringType.RowId is 1 or 3))
            yield return EClassJob.Botanist;
    }

    private string GetItemName(uint itemId)
    {
        return _dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.Name.ToString() ?? $"Unknown #{itemId}";
    }

    private ushort GetIconId(uint itemId)
    {
        return _dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.Icon ?? 0;
    }

    private uint GetItemFromCurrency(ushort currency)
    {
        return currency switch
        {
            2 => 33913, // purple crafter
            6 => 41784, // orange crafter
            4 => 33914, // purple gatherer
            7 => 41785, // orange gatherer
            _ => 0,
        };
    }

    private unsafe List<ConditionalReward> CalculateInventorySums()
    {
        if (!_clientState.IsLoggedIn || _clientState.IsPvPExcludingDen || !_itemWindow.IsOpen)
            return new();

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return new();

        List<SingleReward> individualRewards = new();
        foreach (InventoryType inventory in _inventoryTypes)
        {
            InventoryContainer* container = inventoryManager->GetInventoryContainer(inventory);
            if (container == null || !container->IsLoaded)
                continue;

            for (int index = 0; index < container->Size; ++index)
            {
                var item = container->GetInventorySlot(index);
                if (item == null || item->ItemId == 0 ||
                    !_collectableItems.TryGetValue(item->ItemId, out var collectableItem))
                    continue;

                AllRewardsForSingleTurnIn? rewards = collectableItem.FindByCollectability(item->SpiritbondOrCollectability);
                if (rewards != null)
                {
                    individualRewards.Add(rewards.ToReward1(item->SpiritbondOrCollectability));
                    if (rewards.HasReward2)
                        individualRewards.Add(rewards.ToReward2(item->SpiritbondOrCollectability)!);
                }
            }
        }

        UpdateWeeklyDeliveryItems(individualRewards);

        return individualRewards.GroupBy(x => new { x.Reward.Item, x.Reward.RewardType })
            .Select(group => BuildConditionalReward(group.Key.Item, group.Key.RewardType, group.ToList()))
            .OrderBy(c => c.Item.Id)
            .ToList();
    }

    private unsafe ConditionalReward BuildConditionalReward(ItemRef rewardItem, ERewardType rewardType,
        IReadOnlyList<SingleReward> group)
    {
        int minimumReward = 0;
        int bonusReward = 0;
        Dictionary<EClassJob, int> bonusRewards = group
            .SelectMany(x => x.TurnInJobs)
            .Distinct()
            .ToDictionary(x => x, _ => 0);
        foreach (var item in group)
        {
            int rewardForTurnIn = item.Reward.QuantityToTurnIn;
            if (rewardForTurnIn == 0)
                continue;

            if (item.TurnInJobs.Count == 1 || item.RequiredLevel == 0 ||
                IsAlwaysEligibleForReward2(item.TurnInJobs, item.RequiredLevel))
            {
                // no matter how we turn this in, we get the same rewards
                minimumReward += rewardForTurnIn;
            }
            else
            {
                // we get different rewards per class depending on e.g. level, and we can actually turn this in
                // on multiple different classes
                bool anyEligible = false;
                foreach (EClassJob classJob in item.TurnInJobs)
                {
                    if (IsEligibleForReward2(classJob, item.RequiredLevel))
                    {
                        bonusRewards[classJob] += rewardForTurnIn;
                        anyEligible = true;
                    }
                }

                if (anyEligible)
                    bonusReward += rewardForTurnIn;
            }
        }

        return new ConditionalReward
        {
            Item = rewardItem,
            RewardType = rewardType,
            MinimumRewardQuantity = minimumReward,
            MaximumRewardQuantity = minimumReward + bonusReward,
            QuantityInInventory = InventoryManager.Instance()->GetInventoryItemCount(rewardItem.Id),
            TurnInQuantityPerJob = bonusRewards,
            RequiredLevelForMaximumReward = group.Max(x => x.RequiredLevel),
        };
    }

    /// <remarks>
    /// this doesn't account for delivering on multiple different roles (i.e. you have this week's fisher + crafter
    /// item in your inventory, but that is almost impossible to handle properly).
    /// </remarks>
    private unsafe Dictionary<uint, (int Allowances, int Multiplier)> BuildWeeklyDeliveryAllowances()
    {
        Dictionary<uint, (int, int)> allowances = new();
        var satisfactionSupplyManager = SatisfactionSupplyManager.Instance();
        if (satisfactionSupplyManager != null &&
            satisfactionSupplyManager->BonusGuaranteeRowId != 0xFF &&
            satisfactionSupplyManager->GetRemainingAllowances() > 0)
        {
            var bonusGuaranteeRow = _dataManager.GetExcelSheet<SatisfactionBonusGuarantee>()
                .GetRow(satisfactionSupplyManager->BonusGuaranteeRowId);
            foreach (var npc in _dataManager.GetExcelSheet<SatisfactionNpc>()
                         .Where(x => x.RowId > 0 && x.QuestRequired.RowId != 0))
            {
                byte npcIndex = (byte)(npc.RowId - 1);
                var rank = satisfactionSupplyManager->SatisfactionRanks[npcIndex];
                if (rank == 0)
                    continue;

                var supplyIndex = npc.SatisfactionNpcParams[rank].SupplyIndex;
                if (supplyIndex == 0)
                    continue;

                int remainingNpcAllowances =
                    npc.DeliveriesPerWeek - satisfactionSupplyManager->UsedAllowances[npcIndex];
                if (remainingNpcAllowances == 0)
                    continue;

                remainingNpcAllowances = Math.Min(satisfactionSupplyManager->GetRemainingAllowances(),
                    remainingNpcAllowances);

                uint[] items = _calculations.CalculateRequestedItems(npcIndex);
                for (int i = 0; i < items.Length; ++ i)
                {
                    var satisfactionSupplyRows =
                        _dataManager.GetSubrowExcelSheet<SatisfactionSupply>().GetRow((uint)supplyIndex);
                    int itemIndex = (int)items[i];
                    var satisfactionSupplyRow = satisfactionSupplyRows[itemIndex];

                    bool bonusOverride = i switch
                    {
                        0 => bonusGuaranteeRow.BonusDoH.Contains(npcIndex),
                        1 => bonusGuaranteeRow.BonusDoL.Contains(npcIndex),
                        2 => bonusGuaranteeRow.BonusFisher.Contains(npcIndex),
                        _ => false
                    };
                    int bonusMultiplier = satisfactionSupplyRow.Reward.Value.BonusMultiplier;
                    if (bonusOverride && !satisfactionSupplyRow.IsBonus)
                    {
                        var bonusSupplyRow =
                            satisfactionSupplyRows.FirstOrDefault(
                                x => x.Slot == satisfactionSupplyRow.Slot && x.IsBonus);
                        bonusMultiplier = bonusSupplyRow.Reward.Value.BonusMultiplier;
                    }

                    uint itemId = satisfactionSupplyRow.Item.RowId;
                    allowances.Add(itemId, (remainingNpcAllowances, bonusMultiplier));
                }
            }
        }

        return allowances;
    }

    private void UpdateWeeklyDeliveryItems(List<SingleReward> allRewards)
    {
        var allowances = BuildWeeklyDeliveryAllowances();
        var weeklyLimitedItems = allRewards.Where(x => x.Availability == EAvailability.WeeklyDelivery)
            .GroupBy(x => new { x.TurnInItemId, x.Reward.Item })
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Collectability).ToList());
        foreach (var (key, items) in weeklyLimitedItems)
        {
            if (allowances.TryGetValue(key.TurnInItemId, out var weeklyItem))
            {
                while (items.Count > weeklyItem.Allowances)
                {
                    allRewards.Remove(items[0]);
                    items.RemoveAt(0);
                }

                if (weeklyItem.Multiplier != 100)
                {
                    foreach (var item in items)
                        item.Reward.QuantityToTurnIn = item.Reward.QuantityToTurnIn * weeklyItem.Multiplier / 100;
                }
            }
            else
            {
                foreach (var item in items)
                    allRewards.Remove(item);
            }
        }
    }

    private static bool IsAlwaysEligibleForReward2(List<EClassJob> classJobs, byte minimumLevel)
    {
        return classJobs.All(x => IsEligibleForReward2(x, minimumLevel));
    }

    private static unsafe bool IsEligibleForReward2(EClassJob classJob, byte minimumLevel)
    {
        PlayerState* playerState = PlayerState.Instance();
        if (playerState == null)
            return false;

        // this is fine for crafters/gatherers, would need a sheet lookup for combat jobs
        var level = playerState->ClassJobLevels[(byte)classJob - 1];
        return level > 0 && level >= minimumLevel;
    }

    private sealed class ShopReward
    {
        public ERewardType RewardType { get; init; }
        public uint RewardItem { get; init; }
        public ushort LowQuantity { get; init; }
        public ushort MidQuantity { get; init; }
        public ushort HighQuantity { get; init; }
    }
}
