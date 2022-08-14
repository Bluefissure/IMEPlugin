using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Hooking;
using ImGuiNET;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using IMEPlugin.Win32_Utils;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Network;
using static IMEPlugin.Win32_Utils.Imm;

namespace IMEPlugin
{
    public unsafe class IMEPlugin : IDalamudPlugin
    {
        public string Name => "IMEPlugin";
        public List<string> ImmCand = new List<string>();
        public string ImmComp = "";
        private PluginUI Gui { get; set; }


        [PluginService] public DalamudPluginInterface PluginInterface { get; set; }
        [PluginService] public Condition Conditions { get; set; }
        [PluginService] public SigScanner SigScanner { get; set; }
        [PluginService] public CommandManager CommandManager { get; set; }
        [PluginService] public GameNetwork GameNetwork { get; set; }

        private const string commandName = "/ime";

        private bool threadRunning = false;

        delegate long WndProcDelegate(IntPtr hWnd, uint msg, ulong wParam, long lParam);
        private IntPtr _hWnd;
        private WndProcDelegate _wndProcDelegate;
        private IntPtr _wndProcPtr;
        private IntPtr _oldWndProcPtr;
        private IntPtr ImmFunc;
        private delegate char ImmFuncDelegate(Int64 a1, char a2, byte a3);
        private Hook<ImmFuncDelegate> ImmFuncHook;
        public IMEPlugin()
        {
            this._hWnd = PluginInterface.UiBuilder.WindowHandlePtr;

            this.ImmFunc = SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 10 48 83 FF 09");

            this.ImmFuncHook = new Hook<ImmFuncDelegate>(
                ImmFunc,
                new ImmFuncDelegate(ImmFuncDetour)
            );

            PluginLog.Log($"===== D A L A M U D  I M E =====");
            PluginLog.Log($"WindowHandlePtr addr:{this._hWnd}");
            PluginLog.Log($"ImmFunc addr:{ImmFunc}");

            InitializeWndProc();

            this.threadRunning = true;


            Gui = new PluginUI(this);
            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Shows the IME panel"
            });
        }

        private void OnCommand(string command, string args)
        {
            Gui.Visible = !Gui.Visible;
        }

        #region WndProc
        void InitializeWndProc()
        {
            _wndProcDelegate = WndProcDetour;
            _wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _oldWndProcPtr = Win32.SetWindowLongPtr(_hWnd, WindowLongType.GWL_WNDPROC, _wndProcPtr);
        }

        private long WndProcDetour(IntPtr hWnd, uint msg, ulong wParam, long lParam)
        {
            if (hWnd == _hWnd && ImGui.GetCurrentContext() != IntPtr.Zero && ImGui.GetIO().WantTextInput)
            {
                var io = ImGui.GetIO();
                var wmsg = (WindowsMessage)msg;

                switch (wmsg)
                {
                    case WindowsMessage.WM_IME_NOTIFY:
                        switch ((IMECommand)wParam)
                        {
                            case IMECommand.IMN_CHANGECANDIDATE:
                                Gui.Visible = true;
                                if (hWnd == IntPtr.Zero)
                                    return 0;
                                var hIMC = Imm.ImmGetContext(hWnd);
                                if (hIMC == IntPtr.Zero)
                                    return 0;
                                var size = Imm.ImmGetCandidateList(hIMC, 0, IntPtr.Zero, 0);
                                if (size > 0)
                                {
                                    IntPtr candlistPtr = Marshal.AllocHGlobal((int)size);
                                    size = ImmGetCandidateList(hIMC, 0, candlistPtr, (uint)size);
                                    CandidateList candlist = Marshal.PtrToStructure<CandidateList>(candlistPtr);
                                    var pageSize = candlist.dwPageSize;
                                    int candCount = candlist.dwCount;
                                    if (pageSize > 0 && candCount > 1)
                                    {
                                        int[] dwOffsets = new int[candCount];
                                        for (int i = 0; i < candCount; i++)
                                            dwOffsets[i] = Marshal.ReadInt32(candlistPtr + (i + 6) * sizeof(int));

                                        int pageStart = candlist.dwPageStart;
                                        int pageEnd = pageStart + pageSize;

                                        string[] cand = new string[pageSize];
                                        this.ImmCand.Clear();
                                        for (int i = 0; i < pageSize; i++)
                                        {
                                            var offStart = dwOffsets[i + pageStart];
                                            var offEnd = i + pageStart + 1 < candCount ? dwOffsets[i + pageStart + 1] : size;
                                            IntPtr pStrStart = candlistPtr + (int)offStart;
                                            IntPtr pStrEnd = candlistPtr + (int)offEnd;
                                            int len = (int)(pStrEnd.ToInt64() - pStrStart.ToInt64());
                                            if(len > 0)
                                            {
                                                var candBytes = new byte[len];
                                                Marshal.Copy(pStrStart, candBytes, 0, len);
                                                string candStr = Encoding.Unicode.GetString(candBytes);
                                                cand[i] = candStr;
                                                this.ImmCand.Add(candStr);
                                            }
                                        }
                                    }
                                    Marshal.FreeHGlobal(candlistPtr);
                                }
                                break;
                            case IMECommand.IMN_OPENCANDIDATE:
                                Gui.Visible = true;
                                this.ImmCand.Clear();
                                break;
                            case IMECommand.IMN_CLOSECANDIDATE:
                                Gui.Visible = false;
                                this.ImmCand.Clear();
                                break;
                            default:
                                break;
                        }
                        break;
                    case WindowsMessage.WM_IME_COMPOSITION:
                        if ((lParam & (long)IMEComposition.GCS_RESULTSTR) > 0)
                        {
                            var hIMC = Imm.ImmGetContext(hWnd);
                            if (hIMC == IntPtr.Zero)
                                return 0;
                            var dwSize = Imm.ImmGetCompositionString(hIMC, (uint)IMEComposition.GCS_RESULTSTR, IntPtr.Zero, 0);
                            IntPtr unmanagedPointer = Marshal.AllocHGlobal((int)dwSize);
                            Imm.ImmGetCompositionString(hIMC, (uint)IMEComposition.GCS_RESULTSTR, unmanagedPointer, (uint)dwSize);
                            byte[] bytes = new byte[dwSize];
                            Marshal.Copy(unmanagedPointer, bytes, 0, (int)dwSize);
                            Marshal.FreeHGlobal(unmanagedPointer);
                            string lpstr = Encoding.Unicode.GetString(bytes);
                            io.AddInputCharactersUTF8(lpstr);
                            this.ImmComp = "";
                            this.ImmCand.Clear();
                            Gui.Visible = false;
                        }
                        if (((long)(IMEComposition.GCS_COMPSTR | IMEComposition.GCS_COMPATTR | IMEComposition.GCS_COMPCLAUSE |
                            IMEComposition.GCS_COMPREADATTR | IMEComposition.GCS_COMPREADCLAUSE | IMEComposition.GCS_COMPREADSTR) & lParam) > 0)
                        {
                            var hIMC = Imm.ImmGetContext(hWnd);
                            if (hIMC == IntPtr.Zero)
                                return 0;
                            var dwSize = Imm.ImmGetCompositionString(hIMC, (uint)IMEComposition.GCS_COMPSTR, IntPtr.Zero, 0);
                            IntPtr unmanagedPointer = Marshal.AllocHGlobal((int)dwSize);
                            Imm.ImmGetCompositionString(hIMC, (uint)IMEComposition.GCS_COMPSTR, unmanagedPointer, (uint)dwSize);
                            byte[] bytes = new byte[dwSize];
                            Marshal.Copy(unmanagedPointer, bytes, 0, (int)dwSize);
                            Marshal.FreeHGlobal(unmanagedPointer);
                            string lpstr = Encoding.Unicode.GetString(bytes);
                            this.ImmComp = lpstr;
                            if(lpstr == "")
                                Gui.Visible = false;
                        }
                        break;

                    default:
                        break;
                }
            }

            return Win32.CallWindowProc(_oldWndProcPtr, hWnd, msg, wParam, lParam);
        }
        #endregion

        private char ImmFuncDetour(Int64 a1, char a2, byte a3)
        {
            try
            {
                char ret = this.ImmFuncHook.Original(a1, a2, a3);
                if (ImGui.GetIO().WantTextInput)
                {
                    return (char)0;
                }
                return ret;
            } catch {
                PluginLog.Information("[IME Plugin] Please don't crash in hook.");
            }
            return this.ImmFuncHook.Original(a1, a2, a3);
        }

        public void Dispose()
        {

            Gui.Dispose();

            if (_oldWndProcPtr != IntPtr.Zero)
            {
                Win32.SetWindowLongPtr(_hWnd, WindowLongType.GWL_WNDPROC, _oldWndProcPtr);
                _oldWndProcPtr = IntPtr.Zero;
            }

            this.ImmFuncHook.Disable();

            CommandManager.RemoveHandler(commandName);

        }


        public void Loop()
        {
            while (this.threadRunning)
            {
                try
                {
                    if (ImGui.GetIO().WantTextInput)
                    {
                        this.ImmFuncHook.Enable();
                    }
                    else
                    {
                        this.ImmFuncHook.Disable();
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        private void DrawUI()
        {
            Gui.Draw();
        }

    }
}
