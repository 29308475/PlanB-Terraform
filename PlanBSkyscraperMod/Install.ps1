param(
    [string]$GamePath = 'C:/Program Files (x86)/Steam/steamapps/common/Plan B Terraform'
)
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'PlanBSkyscraperMod.csproj'
$gameRoot = (Resolve-Path -LiteralPath $GamePath).Path
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'BepInEx/core/BepInEx.dll'))) {
    throw 'BepInEx 5 was not found in the selected game installation.'
}
& dotnet build $projectPath -c Release "-p:GamePath=$gameRoot"
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no DLL installed.' }
$sourceDll = Join-Path $PSScriptRoot 'bin/Release/net48/PlanBSkyscraperMod.dll'
$helperPath = Join-Path $PSScriptRoot 'ApplyPending.ps1'
# Supersede only this mod's queued installers, so an old DLL cannot overwrite
# a newer update when the game exits.
Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" | Where-Object {
    $_.ProcessId -ne $PID -and $_.CommandLine -and
    $_.CommandLine.Contains($helperPath) -and $_.CommandLine.Contains('-SourceDll')
} | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
$destinationDirectory = Join-Path $gameRoot 'BepInEx/plugins/PlanBSkyscraperMod'
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
$destinationDll = Join-Path $destinationDirectory 'PlanBSkyscraperMod.dll'
if (Test-Path -LiteralPath $destinationDll) {
    $backupPath = Join-Path $PSScriptRoot ('backups/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Copy-Item -LiteralPath $destinationDll -Destination $backupPath
}
try {
    Copy-Item -LiteralPath $sourceDll -Destination $destinationDll
} catch [System.IO.IOException] {
    $gameProcesses = @(Get-Process 'Plan B Terraform' -ErrorAction SilentlyContinue)
    if ($gameProcesses.Count -eq 0) { throw }
    $pendingDirectory = Join-Path $PSScriptRoot ('pending/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $pendingDirectory -Force | Out-Null
    $pendingDll = Join-Path $pendingDirectory 'PlanBSkyscraperMod.dll'
    Copy-Item -LiteralPath $sourceDll -Destination $pendingDll
    $processList = ($gameProcesses.Id -join ',')
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $helperPath),
        '-SourceDll', ('"{0}"' -f $pendingDll), '-DestinationDll', ('"{0}"' -f $destinationDll),
        '-ProcessIds', $processList)
    Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList $arguments | Out-Null
    Write-Output "Update queued after the game exits: $pendingDll"
    return
}
Write-Output "Installed: $destinationDll"
if (Get-Process 'Plan B Terraform' -ErrorAction SilentlyContinue) {
    Write-Output 'The game is running. The new mod will load after the next restart.'
}
