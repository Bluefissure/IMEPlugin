using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Hooking;
using System;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Network;
using ImGuiNET;
using System.Runtime.InteropServices;

namespace IMEPlugin
{
    public class IMEPlugin : IDalamudPlugin
    {
        public string Name => "IMEPlugin";

        internal static Configuration Config;
        [PluginService] internal DalamudPluginInterface PluginInterface { get; set; }
        [PluginService] internal Condition Conditions { get; set; }
        [PluginService] internal SigScanner SigScanner { get; set; }
        [PluginService] internal CommandManager CommandManager { get; set; }
        [PluginService] internal GameNetwork GameNetwork { get; set; }
        public PluginUi Gui { get; private set; }

        internal IntPtr InGameIMEFunc;
        private delegate Int64 InGameIMEFuncDelegate(Int64 a1, Int64 a2, IntPtr a3);
        private Hook<InGameIMEFuncDelegate> InGameIMEFuncHook;


        internal IntPtr IsDirectChatFunc;
        private delegate char IsDirectChatFuncDelegate(Int64 a1);
        private Hook<IsDirectChatFuncDelegate> IsDirectChatFuncHook;

        public IMEPlugin()
        {
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(PluginInterface);

            this.InGameIMEFunc = SigScanner.ScanText("40 53 48 83 EC 20 41 C7 00 ?? ?? ?? ??");
            //this.IsDirectChatFunc = SigScanner.ScanText("40 56 48 83 EC 30 80 79 1C 00");
            PluginLog.Log($"===== I M E P L U G I N =====");
            PluginLog.Log($"InGameIMEFunc addr:{InGameIMEFunc:X}");
            //PluginLog.Log($"IsDirectChatFunc addr:{IsDirectChatFunc:X}");

            this.InGameIMEFuncHook = new Hook<InGameIMEFuncDelegate>(
                InGameIMEFunc,
                new InGameIMEFuncDelegate(InGameIMEFuncDetour)
            );
            /*
            this.IsDirectChatFuncHook = new Hook<IsDirectChatFuncDelegate>(
                IsDirectChatFunc,
                new IsDirectChatFuncDelegate(IsDirectChatFuncDetour)
            );
            */

            Gui = new PluginUi(this);

            CommandManager.AddHandler("/imeplugin", new CommandInfo(CommandHandler)
            {
                HelpMessage = "/imeplugin - open the IMEPlugin panel."
            });

            this.InGameIMEFuncHook.Enable();
            //this.IsDirectChatFuncHook.Enable();
        }

        public void CommandHandler(string command, string arguments)
        {
            var args = arguments.Trim().Replace("\"", string.Empty);

            if (string.IsNullOrEmpty(args) || args.Equals("config", StringComparison.OrdinalIgnoreCase))
            {
                Gui.ConfigWindow.Visible = !Gui.ConfigWindow.Visible;
                return;
            }
        }

        private Int64 InGameIMEFuncDetour(Int64 a1, Int64 a2, IntPtr a3)
        {
            if (Config.UseSystemIME)
            {
                if (ImGui.GetIO().WantTextInput)
                    return 0x80070057;
                else
                {
                    InGameIMEFuncHook.Original(a1, a2, a3);
                }
            }
            return InGameIMEFuncHook.Original(a1, a2, a3);
        }
        private char IsDirectChatFuncDetour(Int64 a1)
        {
            if (Config.DirectChatMode && ImGui.GetIO().WantTextInput)
            {
                return (char)0;
            }
            return IsDirectChatFuncHook.Original(a1);
        }

        public void Dispose()
        {
            this.InGameIMEFuncHook.Disable();
            // this.IsDirectChatFuncHook.Disable();
            CommandManager.RemoveHandler("/imeplugin");
            Gui?.Dispose();
            // PluginInterface?.Dispose();
        }

    }
}
