using System.Collections.Generic;
using System.Numerics;
using CollectableCalculator.Model;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace CollectableCalculator.Windows;

internal sealed class ItemWindow : Window
{
    private const string Title = "Collectables Summary";
    private readonly ITextureProvider _textureProvider;
    private readonly Configuration _configuration;
    private List<ActualReward> _currentRewards = new();

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
