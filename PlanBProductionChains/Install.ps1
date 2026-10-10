param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Plan B Terraform',
    [switch]$WaitForExit
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'Plan B Terraform.exe'))) {
    throw "Keine Plan-B-Installation gefunden: $GamePath"
}
if ($WaitForExit) {
    $deadline = (Get-Date).AddHours(2)
    while (Get-Process -Name 'Plan B Terraform' -ErrorAction SilentlyContinue) {
        if ((Get-Date) -gt $deadline) { throw 'Installation abgebrochen: Spiel wurde innerhalb von zwei Stunden nicht geschlossen.' }
        Start-Sleep -Milliseconds 250
    }
}
if (Get-Process -Name 'Plan B Terraform' -ErrorAction SilentlyContinue) {
    throw 'Bitte Plan B vor der Installation schließen.'
}
$pluginDll = Join-Path $PSScriptRoot 'bin\Release\net48\PlanBProductionChains.dll'
if (-not (Test-Path -LiteralPath $pluginDll)) { throw 'Zuerst dotnet build -c Release ausführen.' }
$loaderSource = Join-Path $PSScriptRoot 'Dependencies'
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'BepInEx\core\BepInEx.dll'))) {
    if (Test-Path -LiteralPath (Join-Path $GamePath 'winhttp.dll')) {
        throw 'Eine winhttp.dll ist bereits vorhanden. Bestehenden Mod-Loader prüfen, bevor BepInEx installiert wird.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $loaderSource 'winhttp.dll'))) {
        throw 'BepInEx-Abhängigkeiten fehlen. Siehe README.md.'
    }
    Copy-Item -LiteralPath (Join-Path $loaderSource 'BepInEx') -Destination $GamePath -Recurse
    Copy-Item -LiteralPath (Join-Path $loaderSource 'winhttp.dll') -Destination $GamePath
    Copy-Item -LiteralPath (Join-Path $loaderSource 'doorstop_config.ini') -Destination $GamePath
}
$pluginDirectory = Join-Path $GamePath 'BepInEx\plugins\PlanBProductionChains'
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination $pluginDirectory -Force
Write-Output "Installiert: $pluginDirectory"
