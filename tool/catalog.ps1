#Requires -Version 7.0
<#
.SYNOPSIS
    Finds what already exists for a word, so nothing is written twice.

.DESCRIPTION
    Searches the C# sources (not migrations, bin or obj) for type names and localization keys
    that contain the word, and prints one short line per hit.

    Run it BEFORE creating a command, query, DTO, error, localization key or helper:

        pwsh ./tool/catalog.ps1 trip
        pwsh ./tool/catalog.ps1 password -Max 20

    A match means: reuse it or add a parameter. Two near-duplicates, or about to write a second
    copy, is a question for the owner.

.PARAMETER Word
    Part of a name, case-insensitive.

.PARAMETER Max
    Lines per section (default 15).
#>
param(
    [Parameter(Mandatory, Position = 0)]
    [string] $Word,

    [int] $Max = 15
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$sources = Get-ChildItem -Path src, tests -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/](Migrations|bin|obj)[\\/]' }

function Rel([string] $path) { [IO.Path]::GetRelativePath($root, $path).Replace('\', '/') }

$typeRx = [regex]'^\s*(?:public|internal)\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(?:record\s+struct|record|class|interface|enum|struct)\s+(\w+)'
$types = foreach ($file in $sources) {
    $n = 0
    foreach ($line in [IO.File]::ReadLines($file.FullName)) {
        $n++
        $m = $typeRx.Match($line)
        if ($m.Success -and $m.Groups[1].Value -like "*$Word*") {
            '{0,-45} {1}:{2}' -f $m.Groups[1].Value, (Rel $file.FullName), $n
        }
    }
}

$keysFile = Get-ChildItem -Path src -Recurse -Filter LocalizationKeys.cs -File | Select-Object -First 1
$keys = if ($keysFile) {
    Select-String -Path $keysFile.FullName -Pattern 'const string (\w+) = "([^"]+)"' |
        Where-Object { $_.Matches[0].Groups[2].Value -like "*$Word*" -or $_.Matches[0].Groups[1].Value -like "*$Word*" } |
        ForEach-Object { $_.Matches[0].Groups[2].Value }
}

function Show([string] $title, $items) {
    $list = @($items | Where-Object { $_ })
    if ($list.Count -eq 0) { return }
    Write-Host "== $title ($($list.Count))"
    $list | Select-Object -First $Max | ForEach-Object { Write-Host "  $_" }
    if ($list.Count -gt $Max) { Write-Host "  ... $($list.Count - $Max) more (use -Max)" }
}

Show 'types' $types
Show 'localization keys' $keys

if (-not ($types -or $keys)) {
    Write-Host "nothing named like '$Word' exists yet"
}
