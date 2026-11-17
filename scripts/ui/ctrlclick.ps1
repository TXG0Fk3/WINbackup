# Clique de mouse real no elemento com esse nome (opcional: segurando Ctrl)
param([string]$Name, [switch]$Ctrl)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class M { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, IntPtr e);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint f, IntPtr e);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); }
'@
[M]::SetProcessDPIAware() | Out-Null
$p = Get-Process WINbackup | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
[M]::SetForegroundWindow($p.MainWindowHandle) | Out-Null; Start-Sleep -Milliseconds 300
$cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = [Windows.Automation.AutomationElement]::RootElement.FindFirst('Children', $cond)
$el = $win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name -eq $Name } | Select-Object -First 1
if (-not $el) { "não achei $Name"; exit 1 }
$b = $el.Current.BoundingRectangle
[M]::SetCursorPos([int]($b.X + $b.Width / 2), [int]($b.Y + $b.Height / 2)) | Out-Null
if ($Ctrl) { [M]::keybd_event(0x11, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80 }
[M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60; [M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero)
if ($Ctrl) { Start-Sleep -Milliseconds 80; [M]::keybd_event(0x11, 0, 2, [IntPtr]::Zero) }
"clique em $Name" + $(if ($Ctrl) { ' com Ctrl' })
