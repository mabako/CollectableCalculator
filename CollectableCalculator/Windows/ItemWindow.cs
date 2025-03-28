using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CollectableCalculator.Model;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace CollectableCalculator.Windows;

internal sealed class ItemWindow : Window
{
    private const string Title = "Collectables Summary";
    private readonly ITextureProvider _textureProvider;
    private readonly Configuration _configuration;
    private List<ConditionalReward> _currentRewards = new();

    public ItemWindow(ITextureProvider textureProvider, Configuration configuration)
        : base($"{Title}###CollectableCalculatorItems")
    {
        _textureProvider = textureProvider;
        _configuration = configuration;

        Position = new Vector2(300, 300);
        PositionCondition = ImGuiCond.FirstUseEver;

        Size = new Vector2(100, 100);
        SizeCondition = ImGuiCond.FirstUseEver;

        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void OnOpen()
    {
        SizeConstraints ??= new()
        {
            // AlwaysAutoResize ignores the title bar size (title/any icons) and just resizes the window according to
            // the content; so we make sure the title bar is rendered properly (icons like the hamburger menu scale with
            // font size)
            MinimumSize = new Vector2(ImGui.CalcTextSize(Title).X + 4 * ImGui.GetFontSize(), 40)
        };
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
            ISharedImmediateTexture icon = _textureProvider.GetFromGameIcon(new GameIconLookup(item.Item.IconId));
            if (icon.TryGetWrap(out IDalamudTextureWrap? wrap, out _))
            {
                ImGui.Image(wrap.ImGuiHandle, new Vector2(21, 21));
                ImGui.SameLine();

                wrap.Dispose();
            }

            string label;
            if (item.HasOptionalRewards())
                label = $"{item.MinimumRewardQuantity:N0} - {item.MaximumRewardQuantity:N0}x";
            else
                label = $"{item.MinimumRewardQuantity:N0}x";

            if (item.QuantityInInventory > 0 && (
                    (item.RewardType == ERewardType.Scrips && _configuration.ShowTotalForScrips) ||
                    (item.RewardType == ERewardType.Item && _configuration.ShowTotalForItems)))
            {
                if (item.HasOptionalRewards())
                    label +=
                        $" ({item.MinimumRewardQuantity + item.QuantityInInventory:N0} - {item.MaximumRewardQuantity + item.QuantityInInventory:N0}x)";
                else
                    label += $" ({item.MinimumRewardQuantity + item.QuantityInInventory:N0}x)";
            }

            label += $" {item.Item.Name}";
            ImGui.TextUnformatted(label);

            if (item.HasOptionalRewards())
            {
                ImGui.SameLine();
                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
                }

                if (ImGui.IsItemHovered())
                {
                    using var tooltip = ImRaii.Tooltip();
                    if (tooltip)
                    {
                        var groups = item.TurnInQuantityPerJob.GroupBy(x => x.Value, x => x.Key)
                            .OrderByDescending(x => x.Key);
                        foreach (var group in groups)
                        {
                            ImGui.Text($"You will earn {group.Key:N0}x if you turn your items in as:");
                            foreach (EClassJob classJob in group)
                                ImGui.BulletText(classJob.ToString());
                        }
                    }
                }
            }
        }
    }

    public void Update(List<ConditionalReward> currentRewards)
    {
        _currentRewards = currentRewards;
    }
}
