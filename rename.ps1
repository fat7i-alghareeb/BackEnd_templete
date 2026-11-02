#Requires -Version 7.0
<#
.SYNOPSIS
    Renames this template from "Taxi" to your product name.

.DESCRIPTION
    Replaces the template name everywhere it appears, keeping the casing of each occurrence:

        Taxi -> <Name>      (namespaces, projects, Taxi_Server.slnx, TaxiDb, JWT issuer, docs ...)
        taxi -> <name>      (compose service names, the Redis key prefix, seed e-mails ...)
        TAXI -> <NAME>

    It rewrites file contents first, then renames files and folders (deepest first), so
    src/Taxi.Api/Taxi.Api.csproj becomes src/<Name>.Api/<Name>.Api.csproj.

    It also gives Directory.Build.props a fresh UserSecretsId, so the new project never shares
    local user secrets with the template or with another copy of it.

    Skipped: .git, bin, obj, node_modules, coverage output, LICENSE, this script, and any file that
    looks binary (images, archives, or content containing NUL bytes).

    Run it once, on a fresh copy of the template, before your first commit of the new project.
    Afterwards: `dotnet build`, `dotnet test`, and check `git status`.

    Existing databases are not migrated: the default database becomes <Name>Db, and the outbox
    stores CLR type names, so start from an empty database (docker compose down -v).

.PARAMETER Name
    The new name, PascalCase, letters and digits only, starting with a capital letter (e.g. "Acme").

.PARAMETER Path
    Root of the template. Defaults to the folder this script lives in.

.PARAMETER Force
    Run even when the git working tree has uncommitted changes.

.PARAMETER SkipFormat
    Do not run `dotnet format` afterwards. By default the script re-sorts using directives with
    StyleCop's own fix (SA1210): "<Name>.*" may sort before "Microsoft.*" where "Taxi.*" sorted
    after it, and the build treats that as an error in CI.

.EXAMPLE
    ./rename.ps1 -Name Acme -WhatIf     # preview: lists every file that would change
    ./rename.ps1 -Name Acme             # do it
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$', ErrorMessage = 'Name must be PascalCase letters/digits starting with a capital, e.g. "Acme".')]
    [string] $Name,

    [string] $Path = $PSScriptRoot,

    [switch] $Force,

    [switch] $SkipFormat
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Old = 'Taxi'
if ($Name -ceq $Old) {
    throw "The template is already called '$Old'."
}

$root = (Resolve-Path -LiteralPath $Path).Path
$scriptPath = if ($PSCommandPath) { (Resolve-Path -LiteralPath $PSCommandPath).Path } else { $null }

# --- safety: refuse to run on a dirty tree unless forced ----------------------------------------
if (-not $Force -and (Test-Path -LiteralPath (Join-Path $root '.git'))) {
    $dirty = git -C $root status --porcelain 2>$null
    if ($LASTEXITCODE -eq 0 -and $dirty) {
        throw 'The git working tree has uncommitted changes. Commit or stash them first, or pass -Force.'
    }
}

$excludedDirectories = @('.git', 'bin', 'obj', 'node_modules', 'coverage', 'TestResults', '.vs', '.idea')
$binaryExtensions = @(
    '.png', '.jpg', '.jpeg', '.gif', '.ico', '.webp', '.pdf', '.zip', '.gz', '.7z',
    '.dll', '.exe', '.pdb', '.so', '.dylib', '.woff', '.woff2', '.ttf', '.eot', '.docc', '.flare', '.metadata'
)
$excludedFiles = @('LICENSE')

function Test-Excluded([System.IO.FileSystemInfo] $item) {
    $relative = [System.IO.Path]::GetRelativePath($root, $item.FullName)
    $segments = $relative -split '[\\/]'
    return @($segments | Where-Object { $excludedDirectories -contains $_ }).Count -gt 0
}

function Convert-Name([string] $text) {
    # Ordinal, case-sensitive replacements so each casing maps to its own counterpart.
    return $text.Replace($Old, $Name).Replace($Old.ToLowerInvariant(), $Name.ToLowerInvariant()).Replace($Old.ToUpperInvariant(), $Name.ToUpperInvariant())
}

function Test-NameInside([string] $text) {
    return $text.IndexOf($Old, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$changedFiles = 0
$renamedItems = 0

# --- 1. file contents ---------------------------------------------------------------------------
$files = Get-ChildItem -LiteralPath $root -Recurse -File -Force |
    Where-Object { -not (Test-Excluded $_) } |
    Where-Object { $binaryExtensions -notcontains $_.Extension.ToLowerInvariant() } |
    Where-Object { $excludedFiles -notcontains $_.Name } |
    Where-Object { $_.FullName -ne $scriptPath }

foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if ($bytes -contains 0) {
        continue # binary
    }

    # Keep a UTF-8 BOM if the file had one (EF Core writes migrations with a BOM).
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = $utf8NoBom.GetString($bytes, $(if ($hasBom) { 3 } else { 0 }), $bytes.Length - $(if ($hasBom) { 3 } else { 0 }))

    if (-not (Test-NameInside $text)) {
        continue
    }

    $updated = Convert-Name $text
    $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName)

    if ($PSCmdlet.ShouldProcess($relative, 'Replace template name in content')) {
        $encoding = if ($hasBom) { [System.Text.UTF8Encoding]::new($true) } else { $utf8NoBom }
        [System.IO.File]::WriteAllText($file.FullName, $updated, $encoding)
    }

    $changedFiles++
}

# --- 2. a fresh UserSecretsId -------------------------------------------------------------------
$buildProps = Join-Path $root 'Directory.Build.props'
if (Test-Path -LiteralPath $buildProps) {
    $props = [System.IO.File]::ReadAllText($buildProps)
    $secretsId = "$($Name.ToLowerInvariant())-$([guid]::NewGuid())"
    $newProps = [regex]::Replace($props, '<UserSecretsId>[^<]*</UserSecretsId>', "<UserSecretsId>$secretsId</UserSecretsId>")

    if ($newProps -ne $props -and $PSCmdlet.ShouldProcess('Directory.Build.props', "Set UserSecretsId to $secretsId")) {
        [System.IO.File]::WriteAllText($buildProps, $newProps, $utf8NoBom)
    }
}

# --- 3. file and folder names, deepest first so parents are renamed last ------------------------
$items = Get-ChildItem -LiteralPath $root -Recurse -Force |
    Where-Object { -not (Test-Excluded $_) } |
    Where-Object { $_.FullName -ne $scriptPath } |
    Where-Object { Test-NameInside $_.Name } |
    Sort-Object { $_.FullName.Length } -Descending

foreach ($item in $items) {
    $newName = Convert-Name $item.Name
    if ($newName -ceq $item.Name) {
        continue
    }

    $relative = [System.IO.Path]::GetRelativePath($root, $item.FullName)
    if ($PSCmdlet.ShouldProcess($relative, "Rename to $newName")) {
        Rename-Item -LiteralPath $item.FullName -NewName $newName
    }

    $renamedItems++
}

# --- 4. re-sort using directives (the new name may sort differently than "Taxi") ----------------
$solution = Join-Path $root "$($Name)_Server.slnx"
if (-not $WhatIfPreference -and -not $SkipFormat -and (Test-Path -LiteralPath $solution)) {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        Write-Host 'Re-sorting using directives (dotnet format, StyleCop SA1210) ...'
        dotnet format analyzers $solution --diagnostics SA1210 --severity info
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet format failed; run it yourself: dotnet format analyzers $($Name)_Server.slnx --diagnostics SA1210 --severity info"
        }
    }
    else {
        Write-Warning "dotnet is not on PATH. Before building with -warnaserror, run: dotnet format analyzers $($Name)_Server.slnx --diagnostics SA1210 --severity info"
    }
}

# --- summary ------------------------------------------------------------------------------------
$verb = if ($WhatIfPreference) { 'Would update' } else { 'Updated' }
Write-Host ''
Write-Host "$verb $changedFiles file(s) and rename $renamedItems file(s)/folder(s): '$Old' -> '$Name'." -ForegroundColor Green

if (-not $WhatIfPreference) {
    Write-Host ''
    Write-Host 'Next:'
    Write-Host "  dotnet build $($Name)_Server.slnx"
    Write-Host "  dotnet test $($Name)_Server.slnx"
    Write-Host '  docker compose down -v   # the database is now ' -NoNewline
    Write-Host "$($Name)Db; start from an empty one"
    Write-Host '  git status               # review, then commit'
}
