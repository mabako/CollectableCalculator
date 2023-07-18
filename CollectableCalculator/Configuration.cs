using Dalamud.Configuration;

namespace CollectableCalculator;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool ShowTotalForScrips { get; set; } = true;
    public bool ShowTotalForItems { get; set; } = true;
}
