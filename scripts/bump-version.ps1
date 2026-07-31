#Requires -Version 5.1
<#
.SYNOPSIS
    Starts a new major or minor release line in Directory.Build.props.

.DESCRIPTION
    Major and minor are the two numbers a person decides; patch counts itself. This script raises
    one of them and resets the count, by writing both <VersionPrefix> and <VersionPatchBase>.

    VersionPatchBase is the total commit count at which the new line begins — and because writing
    it is itself a commit, the base is set to one past the current count. That commit therefore
    lands on patch 0: bumping the minor from 0.1.68 gives 0.2.0, and the commit after it 0.2.1.

    Nothing is committed here, and nothing is published. The script edits one file and says what
    to do next.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bump-version.ps1 -Minor

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bump-version.ps1 -Major
#>
[CmdletBinding(DefaultParameterSetName = 'Minor')]
param(
    # 0.1.68 -> 0.2.0
    [Parameter(ParameterSetName = 'Minor')]
    [switch] $Minor,

    # 0.1.68 -> 1.0.0
    [Parameter(ParameterSetName = 'Major')]
    [switch] $Major
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $repo 'Directory.Build.props'

function Write-Step([string] $message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Stop-WithFailure([string] $message) {
    Write-Host "!!! $message" -ForegroundColor Red
    exit 1
}

if (-not $Minor -and -not $Major) {
    Stop-WithFailure 'Say which: -Minor or -Major.'
}

Push-Location $repo
try {
    # The new base counts this bump as its own first commit, so anything else waiting to be
    # committed would land inside the new line while belonging to the old one.
    $dirty = & git status --porcelain
    if ($dirty) {
        Write-Host '    uncommitted:' -ForegroundColor Yellow
        $dirty | ForEach-Object { Write-Host "        $_" }
        Stop-WithFailure 'Working tree is not clean - commit or stash first, so the bump is a commit of its own.'
    }

    $total = [int] (& git rev-list --count HEAD).Trim()
}
finally {
    Pop-Location
}

$text = Get-Content -Path $propsPath -Raw
if ($text -notmatch '<VersionPrefix>(\d+)\.(\d+)</VersionPrefix>') {
    Stop-WithFailure "No <VersionPrefix>major.minor</VersionPrefix> in $propsPath."
}

$currentMajor = [int] $Matches[1]
$currentMinor = [int] $Matches[2]

if ($Major) {
    $newMajor = $currentMajor + 1
    $newMinor = 0
}
else {
    $newMajor = $currentMajor
    $newMinor = $currentMinor + 1
}

$newPrefix = "$newMajor.$newMinor"
# One past the current count: the commit carrying this edit is the first of the new line, and it
# must come out as patch 0.
$newBase = $total + 1

$text = $text -replace '<VersionPrefix>[^<]*</VersionPrefix>', "<VersionPrefix>$newPrefix</VersionPrefix>"
$text = $text -replace '<VersionPatchBase>[^<]*</VersionPatchBase>', "<VersionPatchBase>$newBase</VersionPatchBase>"
Set-Content -Path $propsPath -Value $text -NoNewline -Encoding UTF8

Write-Step "$currentMajor.$currentMinor -> $newPrefix (next commit is ${newPrefix}.0)"
Write-Host ''
Write-Host '    Nothing was committed. Commit this on its own:' -ForegroundColor Yellow
Write-Host "        git add Directory.Build.props"
Write-Host "        git commit -m ""Start $newPrefix"""
Write-Host ''
Write-Host '    Then publish it:' -ForegroundColor Yellow
Write-Host "        powershell -File scripts/publish-image.ps1 -Repository <account>/bugler -Push"
exit 0
