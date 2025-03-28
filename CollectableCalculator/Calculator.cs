using System;
using System.Collections.Generic;
using System.Linq;
using CollectableCalculator.Model;
using CollectableCalculator.Windows;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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

    private readonly IDataManager _dataManager;
    private readonly IClientState _clientState;
    private readonly ItemWindow _itemWindow;
    private readonly IPluginLog _pluginLog;

    private Dictionary<uint, CollectableItem> _collectableItems = new();
    private long _lastUpdate;

    public Calculator(IDataManager dataManager, IClientState clientState, ItemWindow itemWindow, IPluginLog pluginLog)
    {
        _dataManager = dataManager;
        _clientState = clientState;
        _itemWindow = itemWindow;
        _pluginLog = pluginLog;
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
                new Reward
                {
                    RewardType = ERewardType.Item,
                    RewardItem = c.Item.RowId,
                    LowQuantity = c.RewardLow,
                    MidQuantity = c.RewardMid,
                    HighQuantity = c.RewardHigh,
                });

        var scripRewards = _dataManager.GetExcelSheet<CollectablesShopRewardScrip>()
            .ToDictionary(c => c.RowId, c =>
                new Reward
                {
                    RewardType = ERewardType.Scrips,
                    RewardItem = c.Currency switch
                    {
                        2 => 33913, // purple crafter
                        6 => 41784, // orange crafter
                        4 => 33914, // purple gatherer
                        7 => 41785, // orange gatherer
                        _ => 0,
                    },
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

        _collectableItems = _dataManager.GetSubrowExcelSheet<CollectablesShopItem>()
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
                {
                    TurnInItem = new ItemRef
                    {
                        Id = row.Item.RowId,
                        Name = GetItemName(row.Item.RowId),
                        IconId = GetIconId(row.Item.RowId),
                    },
                    RewardType = reward.RewardType,
                    RewardItem = new ItemRef
                    {
                        Id = reward.RewardItem,
                        Name = GetItemName(reward.RewardItem),
                        IconId = GetIconId(reward.RewardItem),
                    },
                    LowCollectability = new Collectability
                    {
                        MinimumQuality = refine?.LowCollectability ?? 0,
                        Quantity = reward.LowQuantity,
                    },
                    MidCollectability = new Collectability
                    {
                        MinimumQuality = refine?.MidCollectability ?? 0,
                        Quantity = reward.MidQuantity,
                    },
                    HighCollectability = new Collectability
                    {
                        MinimumQuality = refine?.HighCollectability ?? 0,
                        Quantity = reward.HighQuantity,
                    }
                };
            })
            .Where(c => c != null)
            .Cast<CollectableItem>()
            .DistinctBy(x => x.TurnInItem.Id) // some stuff like titanium nugget has multiple classes
            .ToDictionary(x => x.TurnInItem.Id, x => x);
    }

    private string GetItemName(uint itemId)
    {
        return _dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.Name.ToString() ?? $"Unknown #{itemId}";
    }

    private ushort GetIconId(uint itemId)
    {
        return _dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.Icon ?? 0;
    }

    private unsafe List<ActualReward> CalculateInventorySums()
    {
        if (!_clientState.IsLoggedIn || _clientState.IsPvPExcludingDen || !_itemWindow.IsOpen)
            return new();

#if !MOCK_INVENTORY
        var manager = InventoryManager.Instance();
        if (manager == null)
            return new();

        List<ActualReward> rewards = new();
        foreach (InventoryType inventory in _inventoryTypes)
        {
            InventoryContainer* container = manager->GetInventoryContainer(inventory);
            if (container == null || !container->IsLoaded)
                continue;

            for (int index = 0; index < container->Size; ++index)
            {
                var item = container->GetInventorySlot(index);
                if (item == null || item->ItemId == 0 ||
                    !_collectableItems.TryGetValue(item->ItemId, out var collectableItem))
                    continue;


                ActualReward? reward = collectableItem.FindByCollectability(item->SpiritbondOrCollectability);
                if (reward != null)
                    rewards.Add(reward);
            }
        }
#else
        List<ActualReward> rewards =
        [
            _collectableItems[44231].FindByCollectability(627)!,
            _collectableItems[44232].FindByCollectability(1200)!,
            _collectableItems[44233].FindByCollectability(799)!,
            _collectableItems[43923].FindByCollectability(900)!,

        ];
#endif

        return rewards.GroupBy(x => new { x.Item, x.RewardType }, x => x.QuantityToTurnIn)
            .Select(group => new ActualReward
            {
                Item = group.Key.Item,
                RewardType = group.Key.RewardType,
                QuantityToTurnIn = group.Sum(),
                QuantityInInventory = InventoryManager.Instance()->GetInventoryItemCount(group.Key.Item.Id)
            })
            .OrderBy(c => c.Item.Id)
            .ToList();
    }

    private sealed class Reward
    {
        public ERewardType RewardType { get; init; }
        public uint RewardItem { get; init; }
        public ushort LowQuantity { get; init; }
        public ushort MidQuantity { get; init; }
        public ushort HighQuantity { get; init; }
    }
}
