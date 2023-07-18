using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;

namespace CollectableCalculator.Windows;

public class ConfigWindow : Window
{
    private readonly Configuration _configuration;
    private readonly DalamudPluginInterface _pluginInterface;

    private bool _showTotalForScrips;
    private bool _showTotalForItems;

    public ConfigWindow(Configuration configuration, DalamudPluginInterface pluginInterface)
        : base("Collectables Configuration###CollectableCalculatorConfiguration")
    {
        _configuration = configuration;
        _pluginInterface = pluginInterface;

        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;

        Size = new Vector2(100, 100);
        SizeCondition = ImGuiCond.FirstUseEver;

        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void OnOpen()
    {
        _showTotalForScrips = _configuration.ShowTotalForScrips;
        _showTotalForItems = _configuration.ShowTotalForItems;
    }

    public override void Draw()
    {
        ImGui.Text("Display Settings");
        ImGui.Checkbox("Show total scrip count (items to turn in + scrips in currency window)",
            ref _showTotalForScrips);
        ImGui.Checkbox("Show total item count (items to turn in + items in your inventory)", ref _showTotalForItems);

        ImGui.Separator();

        bool save = ImGui.Button("Save");
        ImGui.SameLine();
        bool saveAndClose = ImGui.Button("Save & Close");
        ImGui.SameLine();
        bool close = ImGui.Button("Close");

        if (save || saveAndClose)
        {
            _configuration.ShowTotalForScrips = _showTotalForScrips;
            _configuration.ShowTotalForItems = _showTotalForItems;
            _pluginInterface.SavePluginConfig(_configuration);
        }

        if (close || saveAndClose)
            IsOpen = false;
    }

    public void OpenConfigWindow()
    {
        IsOpen = !IsOpen;
    }
}
