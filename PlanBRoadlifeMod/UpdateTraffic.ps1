param([switch]$WaitForExit)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Install.ps1') -WaitForExit:$WaitForExit
$configPath = 'C:\Program Files (x86)\Steam\steamapps\common\Plan B Terraform\BepInEx\config\tony.planb.roadlife.cfg'
$content = [System.IO.File]::ReadAllText($configPath)
$content = [regex]::Replace($content, '(?m)^MaximumVehicles\s*=\s*[^\r\n]*', 'MaximumVehicles = 150')
$content = [regex]::Replace($content, '(?m)^VehiclesPerRoadTile\s*=\s*[^\r\n]*', 'VehiclesPerRoadTile = 0.15')
$content = [regex]::Replace($content, '(?m)^MaximumDensity\s*=\s*[^\r\n]*', 'MaximumDensity = 0.4375')
[System.IO.File]::WriteAllText($configPath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Output 'Verkehr erhöht: MaximumVehicles=150; VehiclesPerRoadTile=0.15; MaximumDensity=0.4375'
