param(
    [Parameter(Mandatory = $true)][string]$SourceDll,
    [Parameter(Mandatory = $true)][string]$DestinationDll,
    [Parameter(Mandatory = $true)][string]$ProcessIds
)
$ErrorActionPreference = 'Stop'
$pendingLog = Join-Path (Split-Path -Parent $SourceDll) 'install.log'
try {
    foreach ($gameProcessId in ($ProcessIds -split ',')) {
        Wait-Process -Id ([int]$gameProcessId) -ErrorAction SilentlyContinue
    }
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            Copy-Item -LiteralPath $SourceDll -Destination $DestinationDll
            $expectedHash = (Get-FileHash -LiteralPath $SourceDll).Hash
            $installedHash = (Get-FileHash -LiteralPath $DestinationDll).Hash
            if ($expectedHash -ne $installedHash) { throw 'Installed DLL checksum mismatch.' }
            "Installed successfully: $DestinationDll" | Set-Content -LiteralPath $pendingLog
            exit 0
        } catch [System.IO.IOException] {
            if ($attempt -eq 29) { throw }
            Start-Sleep -Seconds 1
        }
    }
} catch {
    "Installation failed: $_" | Set-Content -LiteralPath $pendingLog
    exit 1
}
