#Requires -Version 5.1
<#
.SYNOPSIS
    Rebuilds and restarts the Bugler application container from the current working tree.

.DESCRIPTION
    Stop local dev processes -> fast gate (dotnet build + tsc) -> rebuild and recreate the
    'bugler' compose service -> wait for /health.

    Everything running out of the repo tree is stopped first, not just the dev servers: a
    local Bugler.Host or Bugler.SampleSource holds write locks on bin/obj and would fail the
    build gate for reasons unrelated to the code. Whatever gets stopped is listed at the end.

    postgres is deliberately left running: the ingested telemetry and the admin account are
    what makes manual testing possible, and the image only bakes src/ and frontend/. mailpit
    comes up on its own as a dependency of the bugler service - whatever Bugler mails is read
    at http://localhost:8025 rather than delivered.

    Manual testing happens on http://localhost:8080 - the container serves the production
    frontend build from wwwroot, which is the artifact that actually ships. The dev server on
    :3000 serves unbuilt sources instead, so it is stopped rather than left to confuse.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/redeploy.ps1
#>
[CmdletBinding()]
param(
    # Skip the local build + typecheck gate and go straight to the image build.
    [switch] $SkipGate,
    [int] $HealthTimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$repoPrefix = $repo.TrimEnd('\') + '\'
$devRuntimes = @('dotnet.exe', 'bun.exe', 'node.exe')

function Write-Step([string] $message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Stop-WithFailure([string] $message) {
    Write-Host "!!! $message" -ForegroundColor Red
    exit 1
}

# Local processes go first: besides competing for :8080 they hold write locks on bin/obj,
# which makes the build gate fail for reasons that have nothing to do with the code.
Write-Step 'Stopping local dev servers and tools'
$stopped = @()
$candidates = @{}

Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | ForEach-Object {
    $executable = $_.ExecutablePath
    $commandLine = $_.CommandLine
    $runsFromRepo = $executable -and $executable.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)
    $runtimeInRepo = $commandLine -and ($devRuntimes -contains $_.Name) -and ($commandLine -like "*$repo*")
    if ($runsFromRepo -or $runtimeInRepo) {
        $candidates[[int] $_.ProcessId] = $_.Name
    }
}

# Docker publishes the container ports on the same numbers, so only recognisable dev
# runtimes holding :3000 or :8080 are fair game.
foreach ($port in 3000, 8080) {
    $owners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($owner in $owners) {
        $process = Get-Process -Id $owner -ErrorAction SilentlyContinue
        if ($null -ne $process -and $devRuntimes -contains "$($process.ProcessName).exe") {
            $candidates[[int] $process.Id] = $process.ProcessName
        }
    }
}

foreach ($processId in $candidates.Keys) {
    if ($processId -eq $PID) { continue }
    $name = $candidates[$processId]
    Write-Host "    stopping $name (pid $processId)"
    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    $stopped += "$name (pid $processId)"
}

if ($stopped.Count -eq 0) {
    Write-Host '    nothing to stop'
}
else {
    # Give Windows a moment to release the file handles the build is about to need.
    Start-Sleep -Milliseconds 750
}

if (-not $SkipGate) {
    Write-Step 'Gate: dotnet build Bugler.slnx'
    & dotnet build (Join-Path $repo 'Bugler.slnx') --nologo
    if ($LASTEXITCODE -ne 0) {
        Stop-WithFailure 'Backend build failed - nothing was redeployed.'
    }

    Write-Step 'Gate: bun run typecheck'
    Push-Location (Join-Path $repo 'frontend')
    try {
        & bun run typecheck
        if ($LASTEXITCODE -ne 0) {
            Stop-WithFailure 'Frontend typecheck failed - nothing was redeployed.'
        }
    }
    finally {
        Pop-Location
    }
}

Write-Step 'docker compose up -d --build bugler'
Push-Location $repo
try {
    & docker compose up -d --build bugler
    if ($LASTEXITCODE -ne 0) {
        Stop-WithFailure 'docker compose build/up failed.'
    }
}
finally {
    Pop-Location
}

Write-Step "Waiting for http://localhost:8080/health (timeout ${HealthTimeoutSeconds}s)"
$deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
$healthy = $false
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri 'http://localhost:8080/health' -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            $healthy = $true
            break
        }
    }
    catch {
        # Not up yet.
    }
    Start-Sleep -Seconds 2
}

if (-not $healthy) {
    Write-Host '--- docker compose logs --tail=50 bugler ---' -ForegroundColor Yellow
    Push-Location $repo
    try { & docker compose logs --tail=50 bugler } finally { Pop-Location }
    Stop-WithFailure "Container did not report healthy within ${HealthTimeoutSeconds}s."
}

Write-Step 'Redeployed. UI + REST on http://localhost:8080, OTLP on :4317 (gRPC) and :4318 (HTTP), mail on http://localhost:8025.'
if ($stopped.Count -gt 0) {
    Write-Host "    stopped on the way: $($stopped -join ', ') - restart them yourself if you still need them" -ForegroundColor Yellow
}
exit 0
