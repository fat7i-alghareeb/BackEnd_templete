#Requires -Version 7.0
<#
.SYNOPSIS
    Claude Code PreToolUse hook (see .claude/settings.json) that quietens `dotnet build` and
    `dotnet test`.

.DESCRIPTION
    Claude pays for every line a command prints. A plain `dotnet build` / `dotnet test` prints a
    restore line and a line per project that nobody needs. This hook appends `--nologo -v q`, which
    leaves warnings, errors, failed tests and the final summary.

    It rewrites nothing else: no change when a verbosity is already given, and a compound command
    (`&&`, `;`, `|`, redirects, `$`, backticks) is left exactly as written.

    Reads the hook payload (JSON) on stdin; writes the hook response (JSON) on stdout.
#>
$ErrorActionPreference = 'Stop'

function Write-NoChange { Write-Output '{}'; exit 0 }

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json -AsHashtable
}
catch {
    Write-NoChange
}

$toolInput = if ($payload -is [System.Collections.IDictionary]) { $payload['tool_input'] } else { $null }
$command = if ($toolInput -is [System.Collections.IDictionary]) { $toolInput['command'] } else { $null }

if ($command -isnot [string]) { Write-NoChange }

$trimmed = $command.Trim()

if ($trimmed -notmatch '^dotnet (build|test)(\s|$)') { Write-NoChange }
if ($trimmed -match '[;&|<>`$]') { Write-NoChange }
if ($trimmed -match '(^|\s)(-v|--verbosity|-verbosity)(\s|:|=|$)') { Write-NoChange }

$extra = if ($trimmed -match '(^|\s)(--nologo|-nologo)(\s|$)') { ' -v q' } else { ' --nologo -v q' }

$updated = @{}
foreach ($key in $toolInput.Keys) { $updated[$key] = $toolInput[$key] }
$updated['command'] = $trimmed + $extra

@{
    hookSpecificOutput = @{
        hookEventName = 'PreToolUse'
        updatedInput  = $updated
    }
} | ConvertTo-Json -Depth 6 -Compress
