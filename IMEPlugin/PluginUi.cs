using System;
using IMEPlugin.Gui;

namespace IMEPlugin
{
    public class PluginUi
    {
        private readonly IMEPlugin _plugin;
        public ConfigurationWindow ConfigWindow { get; }

        public PluginUi(IMEPlugin plugin)
        {
            ConfigWindow = new ConfigurationWindow(plugin);
            _plugin = plugin;
            _plugin.PluginInterface.UiBuilder.Draw += Draw;
            _plugin.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        }

        private void Draw()
        {
            ConfigWindow.DrawUi();
        }
        private void OnOpenConfigUi()
        {
            ConfigWindow.Visible = true;
        }

        public void Dispose()
        {
            _plugin.PluginInterface.UiBuilder.Draw -= Draw;
            _plugin.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        }
    }
}
