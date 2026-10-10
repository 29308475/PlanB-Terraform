param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Plan B Terraform',
    [switch]$WaitForExit
)
$ErrorActionPreference = 'Stop'
if ($WaitForExit) {
    $deadline = (Get-Date).AddHours(2)
    while (Get-Process -Name 'Plan B Terraform' -ErrorAction SilentlyContinue) {
        if ((Get-Date) -gt $deadline) { throw 'Installation abgebrochen: Spiel nach zwei Stunden noch geöffnet.' }
        Start-Sleep -Seconds 1
    }
}
if (Get-Process -Name 'Plan B Terraform' -ErrorAction SilentlyContinue) {
    throw 'Bitte Plan B vor der Installation schließen.'
}
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'BepInEx\core\BepInEx.dll'))) {
    throw 'BepInEx 5 muss im Spiel installiert sein.'
}
$pluginDll = Join-Path $PSScriptRoot 'bin\Release\net48\PlanBBahnbetrieb.dll'
if (-not (Test-Path -LiteralPath $pluginDll)) { throw 'Zuerst dotnet build -c Release ausführen.' }
$destination = Join-Path $GamePath 'BepInEx\plugins\PlanBBahnbetrieb'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination $destination -Force
Write-Output "Installiert: $destination"
