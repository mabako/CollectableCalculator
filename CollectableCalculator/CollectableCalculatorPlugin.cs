using System;
using CollectableCalculator.Windows;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace CollectableCalculator
{
    // ReSharper disable once UnusedType.Global
    internal sealed class CollectableCalculatorPlugin : IDalamudPlugin
    {
        public string Name => "CollectableCalculator";

        private readonly DalamudPluginInterface _pluginInterface;
        private readonly Framework _framework;
        private readonly CommandManager _commandManager;
        private readonly WindowSystem _windowSystem;
        private readonly IconCache _iconCache;
        private readonly Calculator _calculator;
        private readonly ItemWindow _itemWindow;

        public CollectableCalculatorPlugin(DalamudPluginInterface pluginInterface, Framework framework,
            DataManager dataManager, CommandManager commandManager, ClientState clientState, ChatGui chatGui)
        {
            _pluginInterface = pluginInterface;
            _framework = framework;
            _commandManager = commandManager;

            _iconCache = new IconCache(pluginInterface, dataManager);

            _itemWindow = new ItemWindow(_iconCache);
            _windowSystem = new(typeof(CollectableCalculatorPlugin).AssemblyQualifiedName);
            _windowSystem.AddWindow(_itemWindow);

            _calculator = new Calculator(dataManager, clientState, _itemWindow);

            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
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
            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

            _iconCache.Dispose();

            _commandManager.RemoveHandler("/cc");
        }
    }
}
