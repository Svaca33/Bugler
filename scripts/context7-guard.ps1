#Requires -Version 5.1
<#
.SYNOPSIS
    Enforces the "consult Context7 before working with a library" rule from CLAUDE.md.

.DESCRIPTION
    -Check runs as a PreToolUse hook for Edit/Write. The first edit of library-facing code
    in a session is denied unless Context7 has already been queried. The reminder fires at
    most once per session, so a session can never deadlock - for example when the Context7
    MCP server is unavailable or simply does not know the library.

    -Mark runs as a PostToolUse hook for mcp__context7__* and records that this session
    consulted the docs.

    Every failure is swallowed: the guard must never block work because of itself.
#>
[CmdletBinding()]
param(
    [switch] $Check,
    [switch] $Mark
)

$ErrorActionPreference = 'Stop'

# Paths that mean "you are about to use a library API". Docs, YAML and tooling config stay free.
$guardedExtensions = @('.cs', '.ts', '.tsx', '.css', '.csproj', '.slnx')
$guardedFileNames = @('package.json', 'Directory.Packages.props')

$reason = @'
Context7 has not been consulted in this session and you are about to edit library-facing code.
Call mcp__context7__resolve-library-id and then mcp__context7__query-docs for the library or
framework you are about to use, then retry this edit (see the Context7 rule in CLAUDE.md).
If Context7 does not know the library or the server is unavailable, say so explicitly in your
reply and carry on. This reminder fires at most once per session.
'@

function Get-StateDirectory {
    $path = Join-Path $env:TEMP 'claude-context7-guard'
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
    return $path
}

try {
    $payload = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($payload)) { exit 0 }

    $hook = $payload | ConvertFrom-Json

    $sessionId = $hook.session_id
    if ([string]::IsNullOrWhiteSpace($sessionId)) { exit 0 }
    $sessionId = $sessionId -replace '[^A-Za-z0-9._-]', ''

    $stateDirectory = Get-StateDirectory
    $consulted = Join-Path $stateDirectory "$sessionId.consulted"
    $reminded = Join-Path $stateDirectory "$sessionId.reminded"

    if ($Mark) {
        New-Item -ItemType File -Path $consulted -Force | Out-Null
        exit 0
    }

    if (-not $Check) { exit 0 }

    $filePath = $hook.tool_input.file_path
    if ([string]::IsNullOrWhiteSpace($filePath)) { exit 0 }

    $extension = [System.IO.Path]::GetExtension($filePath)
    $fileName = [System.IO.Path]::GetFileName($filePath)
    if (($guardedExtensions -notcontains $extension) -and ($guardedFileNames -notcontains $fileName)) {
        exit 0
    }

    if ((Test-Path $consulted) -or (Test-Path $reminded)) { exit 0 }

    # Stand down after this one reminder, whatever happens next.
    New-Item -ItemType File -Path $reminded -Force | Out-Null

    $decision = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason.Trim()
        }
    }

    $decision | ConvertTo-Json -Depth 5 -Compress
    exit 0
}
catch {
    # Fail open - a broken guard must never stop work.
    exit 0
}
