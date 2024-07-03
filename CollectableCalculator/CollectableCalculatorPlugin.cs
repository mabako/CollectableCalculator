using CollectableCalculator.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CollectableCalculator;

// ReSharper disable once UnusedType.Global
internal sealed class CollectableCalculatorPlugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IFramework _framework;
    private readonly ICommandManager _commandManager;
    private readonly WindowSystem _windowSystem;
    private readonly Calculator _calculator;
    private readonly ItemWindow _itemWindow;
    private readonly ConfigWindow _configWindow;

    public CollectableCalculatorPlugin(IDalamudPluginInterface pluginInterface, IFramework framework,
        IDataManager dataManager, ITextureProvider textureProvider, ICommandManager commandManager,
        IClientState clientState, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _framework = framework;
        _commandManager = commandManager;

        var configuration = (Configuration?)_pluginInterface.GetPluginConfig() ?? new Configuration();
        _itemWindow = new ItemWindow(textureProvider, configuration);
        _configWindow = new ConfigWindow(configuration, _pluginInterface);
        _windowSystem = new(typeof(CollectableCalculatorPlugin).AssemblyQualifiedName);
        _windowSystem.AddWindow(_itemWindow);
        _windowSystem.AddWindow(_configWindow);

        _calculator = new Calculator(dataManager, clientState, _itemWindow, pluginLog);

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += _itemWindow.Toggle;
        _pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
        _framework.Update += _calculator.Update;

        _commandManager.AddHandler("/cc", new CommandInfo(ToggleCalculator)
        {
            HelpMessage = "Opens the summary window",
        });
    }

    private void ToggleCalculator(string command, string arguments)
    {
        _itemWindow.IsOpen = !_itemWindow.IsOpen;
    }

    public void Dispose()
    {
        _framework.Update -= _calculator.Update;
        _pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle;
        _pluginInterface.UiBuilder.OpenMainUi -= _itemWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        _commandManager.RemoveHandler("/cc");
    }
}
