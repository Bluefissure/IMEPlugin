using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using ImGuiNET;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Data;
using Dalamud.Logging;

namespace IMEPlugin.Gui
{
    public class ConfigurationWindow
    {
        public IMEPlugin Plugin;
        public bool WindowVisible;
        public virtual bool Visible
        {
            get => WindowVisible;
            set => WindowVisible = value;
        }
        public Configuration Config => IMEPlugin.Config;

        public ConfigurationWindow(IMEPlugin plugin)
        {
            Plugin = plugin;
        }

        public void DrawUi()
        {
            if (Plugin.Conditions == null) return;
            if (!Visible)
            {
                return;
            }
            ImGui.SetNextWindowSize(new Vector2(530, 160), ImGuiCond.Appearing);
            if (ImGui.Begin($"{Plugin.Name} Panel", ref WindowVisible, ImGuiWindowFlags.NoScrollWithMouse))
            {
                /*
                var DirectChatMode = Config.DirectChatMode;
                if (ImGui.Checkbox("Direct Chat Mode", ref DirectChatMode))
                {
                    Config.DirectChatMode = DirectChatMode;
                    Config.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Click this if you are using direct chat mode with controller.");
                */
                var UseSystemIME = Config.UseSystemIME;
                if (ImGui.Checkbox("Use System IME", ref UseSystemIME))
                {
                    Config.UseSystemIME = UseSystemIME;
                    Config.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Fallback in-game IME to system IM .");
                ImGui.End();
            }
        }
    }
}
