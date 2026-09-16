# GamepadCursorFix

Kills Windows' hidden gamepad **focus navigation** — the fix for the "white box" / phantom-selection bug that haunts controller-as-mouse setups.

## The bug

If you use a controller as your mouse (Steam Input, JoyXoff, Controller Companion, reWASD, ...), you may know this one: move the virtual mouse around and, sooner or later, an icon, shortcut, or tab suddenly gets a white selection/focus rectangle around it. The taskbar pops up when you press A or X. Clicks occasionally land twice. It's followed users across machines, mappers, and Windows versions for years — and there is no setting anywhere that turns it off.

## The cause

Windows' shell translates gamepad input into **synthetic keyboard events in the `VK_GAMEPAD_*` virtual-key range** (0xC3–0xDA, defined in `WinUser.h`) and feeds them to gamepad-aware UI surfaces as focus navigation.

Your mapper moves the pointer. Windows' layer moves keyboard focus. Two consumers, one controller — and the focus one has no off switch.

You can prove this on your own machine: run the input monitor in `tools/` and you'll see a stream of `INJECTED`-flagged `VK_GAMEPAD_*` keys riding alongside your controller input. On the machine this was developed on (Windows 11 build 26200), the source was the shell itself — `explorer.exe` with `xinput1_4.dll` loaded and `dwm.exe` with `gameinput.dll` — surviving every kill, disable, and uninstall thrown at it.

## The fix

A ~70-line utility installs a global low-level keyboard hook (`WH_KEYBOARD_LL`) and **silently discards every event in the `VK_GAMEPAD` range before any application can receive it.** Everything else passes through untouched.

Your mapper and your games are unaffected: they read the controller directly (XInput/HID). The swallowed events are the shell's shadow copy of your input — no legitimate software depends on them.

Swallowed range (`WinUser.h`):

| Codes | Meaning |
|---|---|
| `0xC3–0xC8` | A, B, X, Y, right/left shoulder |
| `0xC9–0xCA` | left/right trigger |
| `0xCB–0xCE` | D-pad up/down/left/right |
| `0xCF–0xD0` | menu, view |
| `0xD1–0xD2` | left/right thumbstick buttons |
| `0xD3–0xD6` | left stick up/down/right/left |
| `0xD7–0xDA` | right stick up/down/right/left |

## Install

No SDK needed — every Windows 10/11 ships the .NET Framework compiler:

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:GamepadCursorFix.exe GamepadCursorFix.cs
```

Run `GamepadCursorFix.exe`. To autostart at logon:

```
reg add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v GamepadCursorFix /t REG_SZ /d "\"C:\Path\To\GamepadCursorFix.exe\""
```

## Remove

```
taskkill /f /im GamepadCursorFix.exe
reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v GamepadCursorFix
```

Then delete the exe. That's the entire footprint.

## Caveats

- **Session-scoped:** low-level hooks need an interactive logon session; this is a user-namespace tool, not a service.
- **Unsigned hook exe:** the occasional antivirus heuristic may grumble at an unsigned executable installing a keyboard hook. The source is 70 lines — read it and compile it yourself.
- **Narrow by design:** any app that *deliberately* handles `VK_GAMEPAD` keyboard events (rare — some UWP keyboard shortcuts) will stop receiving them. The range check is one line in `Proc()` if you need to narrow it.
- **Windows may change the mechanism:** if a future build emits different VKs or moves to mouse-side injection, the filter needs a one-line update. The `tools/` monitor will show the new fingerprint.
- **Not this tool's fault (but you'll blame it):** Steam Input yields the controller to gamepad-aware windows (e.g., minimizing Settings can leave the stick dead until you restart Steam). That's Steam Input behavior — this tool filters keyboard events only and cannot affect mouse movement.

## Files

- `GamepadCursorFix.cs` — the fix
- `tools/InputMonitor.cs`, `tools/kl_monitor.ps1` — the diagnostic used to find the mechanism: logs every keyboard/mouse event with its `INJECTED` flag plus foreground-window changes. Run it, reproduce the bug, tap <kbd>ESC</kbd> on a physical keyboard to leave a timestamp marker, then read the log before the marker.

## License

MIT — do whatever, no warranty. If it fixes years of phantom boxes for you, a star is appreciated.
