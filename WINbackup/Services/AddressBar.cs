using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WINbackup.Services;

/// <summary>
/// Abre uma página interna do navegador (edge://, chrome://…). Edge, Chrome e os outros baseados no Chromium
/// ignoram esses endereços quando vêm de outro programa e abrem só uma Nova guia; então o app abre uma janela
/// nova do navegador e digita o endereço na barra, como o usuário faria.
/// </summary>
public static class AddressBar
{
    /// <summary>true se o endereço foi digitado na janela nova do navegador.</summary>
    public static async Task<bool> OpenAsync(string exe, string process, string url)
    {
        var before = Windows(process).ToHashSet();
        ShellLauncher.Run(exe, "--new-window");
        IntPtr window = IntPtr.Zero;
        for (var i = 0; i < 40 && window == IntPtr.Zero; i++)
        {
            await Task.Delay(250);
            window = Windows(process).FirstOrDefault(h => !before.Contains(h));
        }
        if (window == IntPtr.Zero) return false;
        await Task.Delay(700);   // a janela termina de abrir e o foco vai para a barra de endereço
        SetForegroundWindow(window);
        await Task.Delay(200);
        // Só digita se a janela do navegador estiver mesmo em primeiro plano
        if (GetForegroundWindow() != window) return false;

        var keys = new List<INPUT>();
        Key(keys, VK_CONTROL, false); Key(keys, (ushort)'L', false); Key(keys, (ushort)'L', true); Key(keys, VK_CONTROL, true);
        foreach (var c in url) { Char(keys, c, false); Char(keys, c, true); }
        Key(keys, VK_RETURN, false); Key(keys, VK_RETURN, true);
        var arr = keys.ToArray();
        return SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>()) == arr.Length;
    }

    /// <summary>Janelas principais visíveis do navegador.</summary>
    static List<IntPtr> Windows(string process)
    {
        var pids = Process.GetProcessesByName(process).Select(p => { using (p) return (uint)p.Id; }).ToHashSet();
        var list = new List<IntPtr>();
        if (pids.Count == 0) return list;
        EnumWindows((h, _) =>
        {
            if (IsWindowVisible(h) && GetWindow(h, GW_OWNER) == IntPtr.Zero && GetWindowThreadProcessId(h, out var pid) != 0 && pids.Contains(pid)
                && GetWindowTextLength(h) > 0)
                list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    static void Key(List<INPUT> list, ushort vk, bool up) =>
        list.Add(new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } } });

    static void Char(List<INPUT> list, char c, bool up) =>
        list.Add(new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0) } } });

    const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4, GW_OWNER = 4;
    const ushort VK_CONTROL = 0x11, VK_RETURN = 0x0D;

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
