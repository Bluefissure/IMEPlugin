using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Hooking;
using System;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Network;

namespace IMEPlugin
{
    public class IMEPlugin : IDalamudPlugin
    {
        public string Name => "IMEPlugin";

        [PluginService] internal DalamudPluginInterface PluginInterface { get; set; }
        [PluginService] internal Condition Conditions { get; set; }
        [PluginService] internal SigScanner SigScanner { get; set; }
        [PluginService] internal CommandManager CommandManager { get; set; }
        [PluginService] internal GameNetwork GameNetwork { get; set; }

        internal IntPtr InGameIMEFunc;
        private delegate Int64 InGameIMEFuncDelegate(Int64 a1, Int64 a2, IntPtr a3);
        private Hook<InGameIMEFuncDelegate> InGameIMEFuncHook;

        public IMEPlugin()
        {
            this.InGameIMEFunc = SigScanner.ScanText("40 53 48 83 EC 20 41 C7 00 ?? ?? ?? ??");
            PluginLog.Log($"===== I M E P L U G I N =====");
            PluginLog.Log($"InGameIMEFunc addr:{InGameIMEFunc}");

            this.InGameIMEFuncHook = new Hook<InGameIMEFuncDelegate>(
                InGameIMEFunc,
                new InGameIMEFuncDelegate(InGameIMEFuncDetour)
            );

            this.InGameIMEFuncHook.Enable();
        }

        private Int64 InGameIMEFuncDetour(Int64 a1, Int64 a2, IntPtr a3)
        {
            return 0x80070057;
        }

        public void Dispose()
        {
            this.InGameIMEFuncHook.Disable();
        }

    }
}
