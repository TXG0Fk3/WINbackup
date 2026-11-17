# Lê e clica na janela do WINbackup por UI Automation. ui.ps1 names | ui.ps1 click "Nome"
param([string]$Cmd = 'names', [string]$Name = '')
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$p = Get-Process WINbackup -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { 'app fechado'; exit 1 }
$root = [Windows.Automation.AutomationElement]::RootElement
$cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $root.FindFirst('Children', $cond)
$all = $win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition)
if ($Cmd -eq 'names') {
    $all | ForEach-Object { $_.Current } | Where-Object { $_.Name } | ForEach-Object { "$($_.ControlType.ProgrammaticName -replace 'ControlType\.','') | $($_.Name)" }
} elseif ($Cmd -eq 'click') {
    $el = $all | Where-Object { $_.Current.Name -eq $Name -and $_.Current.ControlType.ProgrammaticName -match 'Button' } | Select-Object -First 1
    if (-not $el) { "não achei: $Name"; exit 1 }
    try { $el.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { $el.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
    "clicado: $Name"
}
