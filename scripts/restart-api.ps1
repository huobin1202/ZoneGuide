param(
    [int[]]$Ports = @(56042, 56040),
    [string]$Project = ".\\src\\ZoneGuide.API\\ZoneGuide.API.csproj",
    [switch]$SkipBuild,
    [string]$TunnelUrl = ""
)

$ErrorActionPreference = "Stop"

Write-Host "[restart-api] Checking listeners on ports: $($Ports -join ', ')"

$ownerIds = @()
foreach ($port in $Ports) {
    $listeners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($listeners) {
        $ownerIds += $listeners.OwningProcess
    }
}

$ownerIds = $ownerIds | Sort-Object -Unique
if ($ownerIds.Count -gt 0) {
    Write-Host "[restart-api] Stopping process IDs: $($ownerIds -join ', ')"
    foreach ($ownerId in $ownerIds) {
        try {
            Stop-Process -Id $ownerId -Force -ErrorAction Stop
            Write-Host "[restart-api] Stopped PID $ownerId"
        }
        catch {
            Write-Warning "[restart-api] Could not stop PID ${ownerId}: $($_.Exception.Message)"
        }
    }
}
else {
    Write-Host "[restart-api] No process currently listening on target ports."
}

if (-not $SkipBuild) {
    Write-Host "[restart-api] Building API project..."
    dotnet build $Project -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "API build failed."
    }
}

if (-not [string]::IsNullOrWhiteSpace($TunnelUrl)) {
    $trimmedTunnel = $TunnelUrl.Trim()
    $env:ZONEGUIDE_PUBLIC_TUNNEL_URL = $trimmedTunnel
    Write-Host "[restart-api] Using tunnel URL from env: $trimmedTunnel"
}
elseif ($env:ZONEGUIDE_PUBLIC_TUNNEL_URL) {
    Write-Host "[restart-api] Existing tunnel URL env detected: $($env:ZONEGUIDE_PUBLIC_TUNNEL_URL)"
}
else {
    Write-Host "[restart-api] No tunnel URL env set. API will use appsettings base URL."
}

Write-Host "[restart-api] Starting API..."
dotnet run --project $Project
