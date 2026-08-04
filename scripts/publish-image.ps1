#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the Bugler image and tags it with the version from Directory.Build.props.

.DESCRIPTION
    Not the normal way to release. A release is a pushed git tag, which .github/workflows/release.yml
    turns into an image on GHCR and a GitHub Release, built on a clean runner and authenticated with
    that run's own token. This script is for building the image locally — to look at it, to try it,
    or to publish by hand on the day CI cannot.

    The tag is never typed by hand. It is <VersionPrefix> from Directory.Build.props followed by
    the number of commits made since that major.minor began, and that same count is passed into
    the container build, so the assemblies inside the image carry exactly the version the tag
    claims. Every commit is therefore a version of its own, and no two builds can claim the same
    one.

    A working tree with uncommitted changes is refused: the count would name a commit whose
    content is not what is being built, and a tag that lies about which commit it holds is worse
    than no tag. -AllowDirty is there for when that is understood and meant anyway.

    Pushing is deliberately a separate step. It needs credentials this script has no business
    holding, and it publishes to somewhere outside this machine — so unless -Push is given, the
    script stops after tagging and prints the command to run.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish-image.ps1 -Repository ghcr.io/svaca33/bugler

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish-image.ps1 -Repository ghcr.io/svaca33/bugler -Push
#>
[CmdletBinding()]
param(
    # The registry repository to tag for, e.g. 'ghcr.io/account/bugler'.
    [Parameter(Mandatory = $true)]
    [string] $Repository,

    # Push after building. Requires `docker login ghcr.io` to have been done already.
    [switch] $Push,

    # Build anyway from a working tree that has uncommitted changes.
    [switch] $AllowDirty
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
$prefixNode = $props.SelectSingleNode('//VersionPrefix')
$prefix = if ($null -eq $prefixNode) { '' } else { $prefixNode.InnerText }
if ([string]::IsNullOrWhiteSpace($prefix)) {
    Stop-WithFailure "No <VersionPrefix> in $propsPath - that file is where the version lives."
}

$baseNode = $props.SelectSingleNode('//VersionPatchBase')
$patchBase = if ($null -eq $baseNode) { '0' } else { $baseNode.InnerText }

Push-Location $repo
try {
    # A dirty tree would be tagged with the count of the last commit while holding something else.
    $dirty = & git status --porcelain
    if ($dirty -and -not $AllowDirty) {
        Write-Host '    uncommitted:' -ForegroundColor Yellow
        $dirty | ForEach-Object { Write-Host "        $_" }
        Stop-WithFailure 'Working tree is not clean - commit first, or pass -AllowDirty.'
    }

    $total = (& git rev-list --count HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($total)) {
        Stop-WithFailure 'Could not count commits - is this a git working tree?'
    }

    # Commits made since this major.minor began, which is what the patch number counts.
    $height = [int] $total - [int] $patchBase
    if ($height -lt 0) {
        Stop-WithFailure "VersionPatchBase ($patchBase) is ahead of the history ($total commits) - was a bump committed on a branch that has since been rewritten?"
    }

    $version = "${prefix}.${height}"
    $tag = "${Repository}:${version}"
    Write-Step "Building $tag (commit $((& git rev-parse --short HEAD).Trim()))"

    & docker build --build-arg "BUILD_HEIGHT=$height" -t $tag .
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
    Write-Host '    Not pushed. Normally a release is a pushed git tag and CI does this; by hand:' -ForegroundColor Yellow
    Write-Host "        gh auth refresh -h github.com -s write:packages   # once; gh has no such scope by default"
    Write-Host "        gh auth token | docker login ghcr.io -u <account> --password-stdin"
    Write-Host "        docker push $tag"
    Write-Host ''
    Write-Host "    Then pin it on the server: BUGLER_IMAGE=$tag in its .env" -ForegroundColor Yellow
    exit 0
}

Write-Step "Pushing $tag"
& docker push $tag
if ($LASTEXITCODE -ne 0) {
    Stop-WithFailure 'docker push failed. Signed in? `gh auth token | docker login ghcr.io -u <account> --password-stdin`, with write:packages on that token.'
}

Write-Step "Published $tag - pin it on the server with BUGLER_IMAGE=$tag"
exit 0
