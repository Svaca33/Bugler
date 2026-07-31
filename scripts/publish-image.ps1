#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the Bugler image and tags it with the version from Directory.Build.props.

.DESCRIPTION
    The tag is never typed by hand: it is read from <Version> in Directory.Build.props, the same
    property the assemblies inside the image are stamped from. The tag on the registry and the
    build inside the container therefore cannot drift apart.

    Pushing is deliberately a separate step. It needs credentials this script has no business
    holding, and it publishes to somewhere outside this machine — so unless -Push is given, the
    script stops after tagging and prints the command to run.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish-image.ps1 -Repository svaca33/bugler

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish-image.ps1 -Repository svaca33/bugler -Push
#>
[CmdletBinding()]
param(
    # The Docker Hub repository to tag for, e.g. 'account/bugler'.
    [Parameter(Mandatory = $true)]
    [string] $Repository,

    # Push after building. Requires `docker login` to have been done already.
    [switch] $Push
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot

function Write-Step([string] $message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Stop-WithFailure([string] $message) {
    Write-Host "!!! $message" -ForegroundColor Red
    exit 1
}

$propsPath = Join-Path $repo 'Directory.Build.props'
[xml] $props = Get-Content -Path $propsPath -Raw
$versionNode = $props.SelectSingleNode('//Version')
$version = if ($null -eq $versionNode) { '' } else { $versionNode.InnerText }
if ([string]::IsNullOrWhiteSpace($version)) {
    Stop-WithFailure "No <Version> in $propsPath - that file is where the version lives."
}

$tag = "${Repository}:${version}"
Write-Step "Building $tag"

Push-Location $repo
try {
    & docker build -t $tag .
    if ($LASTEXITCODE -ne 0) {
        Stop-WithFailure 'docker build failed - nothing was tagged.'
    }
}
finally {
    Pop-Location
}

if (-not $Push) {
    Write-Step "Built and tagged $tag"
    Write-Host ''
    Write-Host '    Not pushed. To publish it:' -ForegroundColor Yellow
    Write-Host "        docker login -u <account>   # a read-only token cannot push; use one that may write"
    Write-Host "        docker push $tag"
    Write-Host ''
    Write-Host "    Then pin it on the server: BUGLER_IMAGE=$tag in its .env" -ForegroundColor Yellow
    exit 0
}

Write-Step "Pushing $tag"
& docker push $tag
if ($LASTEXITCODE -ne 0) {
    Stop-WithFailure 'docker push failed. Signed in? `docker login -u <account>` with a token that may write.'
}

Write-Step "Published $tag - pin it on the server with BUGLER_IMAGE=$tag"
exit 0
