using System;
using System.Runtime.InteropServices;
using System.Reflection;

public static class FilterTests
{
    public static int Main()
    {
        int cases = 0;
        for (uint vk = 0; vk <= 255; vk++)
        for (uint flags = 0; flags <= 255; flags++)
        foreach (bool injectedOnly in new bool[] { false, true })
        {
            bool gamepad = vk >= 195 && vk <= 218;
            bool expected = gamepad && (!injectedOnly || (flags & 16) == 16);
            if (GamepadCursorFix.ShouldBlock(vk, flags, injectedOnly) != expected)
                throw new Exception("Unexpected filter decision.");
            cases++;
        }
        // Exercise the actual unmanaged-memory callback for both key edges.
        MethodInfo callback = typeof(GamepadCursorFix).GetMethod("Proc", BindingFlags.NonPublic | BindingFlags.Static);
        FieldInfo mode = typeof(GamepadCursorFix).GetField("_injectedOnly", BindingFlags.NonPublic | BindingFlags.Static);
        IntPtr data = Marshal.AllocHGlobal(32);
        try
        {
            for (int i = 0; i < 32; i++) Marshal.WriteByte(data, i, 0);
            foreach (bool injectedOnly in new bool[] { false, true })
            {
                mode.SetValue(null, injectedOnly);
                Marshal.WriteInt32(data, 8, 0x10);
                for (int vk = 0xC3; vk <= 0xDA; vk++)
                foreach (int message in new int[] { 0x100, 0x101, 0x104, 0x105 })
                {
                    Marshal.WriteInt32(data, vk);
                    IntPtr result = (IntPtr)callback.Invoke(null, new object[] { 0, new IntPtr(message), data });
                    if (result != new IntPtr(1)) throw new Exception("Callback failed to block gamepad event.");
                    cases++;
                }
            }
        }
        finally { Marshal.FreeHGlobal(data); }
        Console.WriteLine("Passed {0} filter checks ({1}-bit).", cases, IntPtr.Size * 8);
        return 0;
    }
}
