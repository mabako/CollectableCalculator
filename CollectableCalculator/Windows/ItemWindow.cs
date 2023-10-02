using System.Collections.Generic;
using System.Numerics;
using CollectableCalculator.Model;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CollectableCalculator.Windows;

internal sealed class ItemWindow : Window
{
    private readonly IconCache _iconCache;
    private readonly Configuration _configuration;
    private List<ActualReward> _currentRewards = new();

    public ItemWindow(IconCache iconCache, Configuration configuration)
        : base("Collectables Summary###CollectableCalculatorItems")
    {
        _iconCache = iconCache;
        _configuration = configuration;

        Position = new Vector2(300, 300);
        PositionCondition = ImGuiCond.FirstUseEver;

        Size = new Vector2(100, 100);
        SizeCondition = ImGuiCond.FirstUseEver;

        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void Draw()
    {
        if (_currentRewards.Count == 0)
        {
            ImGui.TextUnformatted("You have no items to turn in.");
            return;
        }

        ImGui.TextUnformatted("The contents of your inventory can be turned in for:");
        foreach (var item in _currentRewards)
        {
            ImGui.Spacing();
            IDalamudTextureWrap? icon = _iconCache.GetIcon(item.Item.IconId);
            if (icon != null)
            {
                ImGui.Image(icon.ImGuiHandle, new Vector2(21, 21));
                ImGui.SameLine();
            }

            if (item.QuantityInInventory > 0 && (
                    (item.RewardType == ERewardType.Scrips && _configuration.ShowTotalForScrips) ||
                    (item.RewardType == ERewardType.Item && _configuration.ShowTotalForItems)))
                ImGui.TextUnformatted(
                    $"{item.QuantityToTurnIn:N0}x ({(item.QuantityToTurnIn + item.QuantityInInventory):N0}x) {item.Item.Name}");
            else
                ImGui.TextUnformatted($"{item.QuantityToTurnIn:N0}x {item.Item.Name}");
        }
    }

    public void Update(List<ActualReward> currentRewards)
    {
        _currentRewards = currentRewards;
    }
}
