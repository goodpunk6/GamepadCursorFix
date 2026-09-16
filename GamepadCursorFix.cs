using System;
using System.Runtime.InteropServices;
using System.Threading;

// GamepadCursorFix — kills Windows' phantom gamepad focus navigation.
// Installs a global low-level keyboard hook (WH_KEYBOARD_LL) and silently discards
// every event in the VK_GAMEPAD_* range (0xC3-0xDA) before any application sees it,
// so the shell's injected gamepad-navigation keys can never move focus again.
// Mappers and games are unaffected (they read the controller via XInput/HID directly;
// these events are the shell's shadow copy). No logging, no files, no network.
public static class GamepadCursorFix
{
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetHook(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string m);
    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessageW(ref MSG m, IntPtr h, uint a, uint b);
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern IntPtr DispatchMessageW(ref MSG m);

    private static HookProc _proc;   // static: keeps the delegate alive while native code holds it
    private static IntPtr _hook;

    [STAThread]
    public static int Main()
    {
        bool created;
        using (var mux = new Mutex(true, "GamepadCursorFix_SingleInstance", out created))
        {
            if (!created) return 0; // already running
            _proc = new HookProc(Proc);
            _hook = SetHook(13, _proc, GetModuleHandleW(null), 0); // WH_KEYBOARD_LL, global
            if (_hook == IntPtr.Zero) return 1;
            MSG m = new MSG();
            while (GetMessageW(ref m, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref m); DispatchMessageW(ref m); }
            UnhookWindowsHookEx(_hook);
        }
        return 0;
    }

    private static IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            uint msg = unchecked((uint)wParam.ToInt64());
            if (msg == 0x100 || msg == 0x101 || msg == 0x104 || msg == 0x105) // key down/up (incl. sys)
            {
                uint vk = (uint)Marshal.ReadInt32(lParam); // vkCode = first field, single fast read
                if (vk >= 0xC3 && vk <= 0xDA) return new IntPtr(1); // swallow VK_GAMEPAD_* range
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
