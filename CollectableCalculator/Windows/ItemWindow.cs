using System.Collections.Generic;
using System.Numerics;
using CollectableCalculator.Model;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ImGuiScene;

namespace CollectableCalculator.Windows
{
    internal sealed class ItemWindow : Window
    {
        private readonly IconCache _iconCache;
        private List<ActualReward> _currentRewards = new();

        public ItemWindow(IconCache iconCache)
            : base("Collectables Summary###CollectableCalculatorItems")
        {
            _iconCache = iconCache;

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
                TextureWrap? icon = _iconCache.GetIcon(item.Item.IconId);
                if (icon != null)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(21, 21));
                    ImGui.SameLine();
                }

                ImGui.TextUnformatted($"{item.Quantity}x {item.Item.Name}");
            }
        }

        public void Update(List<ActualReward> currentRewards)
        {
            _currentRewards = currentRewards;
        }
    }
}
