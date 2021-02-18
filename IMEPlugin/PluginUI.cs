using ImGuiNET;
using System;
using System.Numerics;

namespace IMEPlugin
{
    class PluginUI : IDisposable
    {
        private Plugin _plugin;

        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }
        public PluginUI(Plugin plugin)
        {
            this._plugin = plugin;
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            DrawMainWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(100, 200), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("IME Plugin", ref this.visible, ImGuiWindowFlags.NoTitleBar))
            {
                ImGui.Text(_plugin.ImmComp);
                ImGui.Separator();
                for(int i = 0; i< _plugin.ImmCand.Count; i++)
                {
                    ImGui.Text($"{i+1}. {_plugin.ImmCand[i]}");
                }
            }
            ImGui.End();
        }

    }
}
