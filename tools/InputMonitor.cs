using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

public static class InputMonitor
{
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate void WinEventProc(IntPtr hHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int x;
        public int y;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetKbHook(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetMsHook(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessageW(ref MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    private static HookProc _kbProc;
    private static HookProc _msProc;
    private static WinEventProc _weProc;
    private static IntPtr _kbHook;
    private static IntPtr _msHook;
    private static IntPtr _weHook;
    private static StreamWriter _w;
    private static Thread _thread;
    private static readonly object _lock = new object();
    private static int _lastMoveTick;
    private static int _moves;
    private static int _movesInjected;

    public static void Start(string logPath)
    {
        if (_thread != null) return;
        _kbProc = new HookProc(KbProc);
        _msProc = new HookProc(MsProc);
        _weProc = new WinEventProc(WeProc);
        _w = new StreamWriter(logPath, false);
        _w.AutoFlush = true;
        _thread = new Thread(new ThreadStart(Run));
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
    }

    private static void Run()
    {
        IntPtr hMod = GetModuleHandleW(null);
        _kbHook = SetKbHook(13, _kbProc, hMod, 0);   // WH_KEYBOARD_LL
        _msHook = SetMsHook(14, _msProc, hMod, 0);   // WH_MOUSE_LL
        _weHook = SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, _weProc, 0, 0, 0); // foreground changes
        Log(string.Format("{0:HH:mm:ss.fff} MONITOR started kbHook={1} msHook={2} weHook={3}", DateTime.Now, _kbHook, _msHook, _weHook));
        MSG msg = new MSG();
        while (GetMessageW(ref msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        Log(string.Format("{0:HH:mm:ss.fff} MONITOR pump exited", DateTime.Now));
    }

    private static void Log(string s)
    {
        lock (_lock) { _w.WriteLine(s); }
    }

    private static string FlagStr(uint flags)
    {
        string f = ((flags & 0x10) != 0) ? "INJECTED" : "hw";
        if ((flags & 0x02) != 0) f += "+LOWERIL";
        return f;
    }

    private static IntPtr KbProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            uint msg = unchecked((uint)wParam.ToInt64());
            if (msg == 0x100 || msg == 0x101 || msg == 0x104 || msg == 0x105)
            {
                KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                bool down = (msg == 0x100 || msg == 0x104);
                string name = VkName(k.vkCode);
                if (name == null) name = string.Format("vk_0x{0:X2}", k.vkCode);
                Log(string.Format("{0:HH:mm:ss.fff} KEY {1} {2} {3} extra=0x{4:X}", DateTime.Now, name, down ? "down" : "up", FlagStr(k.flags), k.dwExtraInfo.ToInt64()));
            }
        }
        return CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private static string VkName(uint vk)
    {
        switch (vk)
        {
            case 0x08: return "BKSP";
            case 0x09: return "TAB";
            case 0x0D: return "ENTER";
            case 0x1B: return "ESC";
            case 0x20: return "SPACE";
            case 0x21: return "PGUP";
            case 0x22: return "PGDN";
            case 0x23: return "END";
            case 0x24: return "HOME";
            case 0x25: return "LEFT";
            case 0x26: return "UP";
            case 0x27: return "RIGHT";
            case 0x28: return "DOWN";
            case 0x5B: return "LWIN";
            case 0x5C: return "RWIN";
            case 0x5D: return "APPS";
            default: return null;
        }
    }

    private static IntPtr MsProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            uint msg = unchecked((uint)wParam.ToInt64());
            if (msg == 0x200)
            {
                _moves++;
                MSLLHOOKSTRUCT m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                if ((m.flags & 0x10) != 0) _movesInjected++;
                int now = Environment.TickCount;
                if (now - _lastMoveTick >= 500)
                {
                    _lastMoveTick = now;
                    Log(string.Format("{0:HH:mm:ss.fff} MOUSE moves x{1} ({2} injected) sampleFlags={3}", DateTime.Now, _moves, _movesInjected, FlagStr(m.flags)));
                    _moves = 0; _movesInjected = 0;
                }
            }
            else if (msg == 0x201 || msg == 0x202 || msg == 0x204 || msg == 0x205 || msg == 0x207 || msg == 0x208)
            {
                MSLLHOOKSTRUCT m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                Log(string.Format("{0:HH:mm:ss.fff} MOUSE {1} {2}", DateTime.Now, MouseName(msg), FlagStr(m.flags)));
            }
        }
        return CallNextHookEx(_msHook, nCode, wParam, lParam);
    }

    private static string MouseName(uint msg)
    {
        switch (msg)
        {
            case 0x201: return "LDown";
            case 0x202: return "LUp";
            case 0x204: return "RDown";
            case 0x205: return "RUp";
            case 0x207: return "MDown";
            case 0x208: return "MUp";
            default: return string.Format("0x{0:X}", msg);
        }
    }

    private static void WeProc(IntPtr hHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == 0x0003)
        {
            uint pid = 0;
            GetWindowThreadProcessId(hwnd, out pid);
            string pn = "?";
            try { pn = Process.GetProcessById((int)pid).ProcessName; } catch { }
            Log(string.Format("{0:HH:mm:ss.fff} FOREGROUND -> {1} (pid {2})", DateTime.Now, pn, pid));
        }
    }
}
