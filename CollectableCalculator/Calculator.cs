using System;
using System.Collections.Generic;
using System.Linq;
using CollectableCalculator.Model;
using CollectableCalculator.Windows;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;

namespace CollectableCalculator
{
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

        private readonly DataManager _dataManager;
        private readonly ClientState _clientState;
        private readonly ItemWindow _itemWindow;

        private Dictionary<uint, CollectableItem> _collectableItems = new();
        private long _lastUpdate;

        public Calculator(DataManager dataManager, ClientState clientState, ItemWindow itemWindow)
        {
            _dataManager = dataManager;
            _clientState = clientState;
            _itemWindow = itemWindow;
        }

        public void Update(Framework framework)
        {
            long now = Environment.TickCount64;
            if (now - _lastUpdate < 200 || !_dataManager.IsDataReady)
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
                PluginLog.Error(e, "Could not perform calculation update");
            }
        }

        private void InitializeData()
        {
            var itemRewards = _dataManager.GetExcelSheet<CollectablesShopRewardItem>()!
                .ToDictionary(c => c.RowId, c =>
                    new Reward
                    {
                        RewardType = ERewardType.Item,
                        RewardItem = c.Item.Row,
                        LowQuantity = c.RewardLow,
                        MidQuantity = c.RewardMid,
                        HighQuantity = c.RewardHigh,
                    });

            var scripRewards = _dataManager.GetExcelSheet<CollectablesShopRewardScrip>()!
                .ToDictionary(c => c.RowId, c =>
                    new Reward
                    {
                        RewardType = ERewardType.Scrips,
                        RewardItem = c.Currency switch
                        {
                            2 => 25199, // white crafter
                            6 => 33913, // purple crafter
                            4 => 25200, // white gatherer
                            7 => 33914, // purple gatherer
                            _ => 0,
                        },
                        LowQuantity = c.LowReward,
                        MidQuantity = c.MidReward,
                        HighQuantity = c.HighReward,
                    });

            // this is a pretty hack-ish way to determine scrip/item shops, but there's no consistent value to map all
            // (`Key` seems to be the most likely in the ShopItem sheet, but that's inaccurate for Oddly Specific mats)
            var shopRewards = _dataManager.GetExcelSheet<CollectablesShopItemGroup>(ClientLanguage.English)!
                .Where(c => c.RowId != 0 && !string.IsNullOrEmpty(c.Name.RawString))
                .ToDictionary(c => c.RowId,
                    c => c.Name.RawString.StartsWith("Lv.") ? scripRewards : itemRewards);

            _collectableItems = _dataManager.GetExcelSheet<CollectablesShopItem>()!
                .Where(row =>
                    row.RowId != 0 && row.CollectablesShopRefine.Row != 0 && row.CollectablesShopItemGroup.Row != 0)
                .Select(row =>
                {
                    if (!shopRewards.TryGetValue(row.CollectablesShopItemGroup.Row, out var shop))
                        return null;

                    if (!shop.TryGetValue(row.CollectablesShopRewardScrip.Row, out var reward))
                        return null;

                    PluginLog.Verbose($"Handling {row.RowId}.{row.SubRowId} -> {row.Item.Row}");

                    CollectablesShopRefine? refine = row.CollectablesShopRefine.Value;
                    return new CollectableItem
                    {
                        TurnInItem = new ItemRef
                        {
                            Id = row.Item.Row,
                            Name = GetItemName(row.Item.Row),
                            IconId = GetIconId(row.Item.Row),
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
            return _dataManager.GetExcelSheet<Item>()!.GetRow(itemId)?.Name?.ToString() ?? $"Unknown #{itemId}";
        }

        private ushort GetIconId(uint itemId)
        {
            return _dataManager.GetExcelSheet<Item>()!.GetRow(itemId)?.Icon ?? 0;
        }

        private unsafe List<ActualReward> CalculateInventorySums()
        {
            if (!_clientState.IsLoggedIn || _clientState.IsPvPExcludingDen || !_itemWindow.IsOpen)
                return new();

            var manager = InventoryManager.Instance();
            if (manager == null)
                return new();

            List<ActualReward> rewards = new();
            foreach (InventoryType inventory in _inventoryTypes)
            {
                InventoryContainer* container = manager->GetInventoryContainer(inventory);
                if (container == null || container->Loaded == 0)
                    continue;

                for (int index = 0; index < container->Size; ++index)
                {
                    var item = container->GetInventorySlot(index);
                    if (item == null || item->ItemID == 0 ||
                        !_collectableItems.TryGetValue(item->ItemID, out var collectableItem))
                        continue;


                    ActualReward? reward = collectableItem.FindByCollectability(item->Spiritbond);
                    if (reward != null)
                        rewards.Add(reward);
                }
            }

            return rewards.GroupBy(x => x.Item, x => x.Quantity)
                .Select(group => new ActualReward { Item = group.Key, Quantity = group.Sum() })
                .OrderBy(c => c.Item.Id)
                .ToList();
        }

        private sealed class Reward
        {
            public ERewardType RewardType { get; set; }
            public uint RewardItem { get; set; }
            public ushort LowQuantity { get; set; }
            public ushort MidQuantity { get; set; }
            public ushort HighQuantity { get; set; }
        }
    }
}
