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
using static IMEPlugin.Win32_Utils.Imm;

namespace IMEPlugin
{
    public unsafe class Plugin : IDalamudPlugin
    {
        public string Name => "Sample Plugin";
        public List<string> ImmCand = new List<string>();
        public string ImmComp = "";
        private PluginUI ui;

        private const string commandName = "/ime";

        private DalamudPluginInterface pi;
        private Thread thread;
        private bool threadRunning = false;

        delegate long WndProcDelegate(IntPtr hWnd, uint msg, ulong wParam, long lParam);
        private IntPtr _hWnd;
        private WndProcDelegate _wndProcDelegate;
        private IntPtr _wndProcPtr;
        private IntPtr _oldWndProcPtr;
        private IntPtr ImmFunc;
        private delegate char ImmFuncDelegate(Int64 a1, char a2, byte a3);
        private Hook<ImmFuncDelegate> ImmFuncHook;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;
            this._hWnd = this.pi.UiBuilder.WindowHandlePtr;

            this.ImmFunc = this.pi.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 10 48 83 FF 09");

            this.ImmFuncHook = new Hook<ImmFuncDelegate>(
                ImmFunc,
                new ImmFuncDelegate(ImmFuncDetour)
            );

            PluginLog.Log($"===== D A L A M U D  I M E =====");
            PluginLog.Log($"WindowHandlePtr addr:{this._hWnd}");
            PluginLog.Log($"ImmFunc addr:{ImmFunc}");

            InitializeWndProc();

            this.thread = new Thread(new ThreadStart(this.Loop));
            this.thread.Start();
            this.threadRunning = true;


            this.ui = new PluginUI(this);
            this.pi.UiBuilder.OnBuildUi += DrawUI;
            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Shows the IME panel"
            });
        }

        private void OnCommand(string command, string args)
        {
            this.ui.Visible = !this.ui.Visible;
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
                                this.ui.Visible = true;
                                this.ImmCand.Clear();
                                break;
                            case IMECommand.IMN_CLOSECANDIDATE:
                                this.ui.Visible = false;
                                this.ImmCand.Clear();
                                break;
                            case IMECommand.IMN_SETCONVERSIONMODE:
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
            char ret = this.ImmFuncHook.Original(a1, a2, a3);
            return (char)0;
        }

        public void Dispose()
        {

            this.ui.Dispose();
            this.threadRunning = false;
            this.thread.Abort();

            if (_oldWndProcPtr != IntPtr.Zero)
            {
                Win32.SetWindowLongPtr(_hWnd, WindowLongType.GWL_WNDPROC, _oldWndProcPtr);
                _oldWndProcPtr = IntPtr.Zero;
            }

            this.ImmFuncHook.Disable();

            this.pi.CommandManager.RemoveHandler(commandName);
            this.pi.Dispose();

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
            this.ui.Draw();
        }

    }
}
