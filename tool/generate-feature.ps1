#Requires -Version 7.0
<#
.SYNOPSIS
    Generates a complete CRUD feature in the shape of the `Car` reference feature.

.DESCRIPTION
    One command writes what AGENTS.md section 3 asks for, in order, so nobody types ~25 files by hand:

      Contracts       LocalizationKeys entries, Create/Update/Get requests
      Domain          the entity (private constructor, Create/Update factories, guards) + <Entity>Errors
      Application     DTO, mapper, Create/Update/Remove commands + handlers + validators,
                      GetById and paged Get queries (cached, culture-aware), CacheTags entry
      Infrastructure  DbSet on IAppDbContext and AppDbContext, EF configuration (xmin, soft delete)
      Api             controller (versioned, policies, idempotent create), Policies + role mapping
      Tests           domain unit tests for every guard
      Docs            sample calls appended to requests/requests.http

    It does NOT write the migration unless you pass -Migrate, and it cannot translate: new keys get
    a `TODO-AR: <english>` value in SharedResource.ar.json that you must replace.

    Field types:  string (required, max 200) · text (bilingual En/Ar, required, max 1000)
                  int · decimal (both must be >= 0) · bool

.PARAMETER Name
    Singular PascalCase entity name, e.g. Trip.

.PARAMETER Fields
    Comma-separated Name:type list, e.g. "Destination:text,Fare:decimal,Seats:int,Code:string".

.PARAMETER Plural
    Plural PascalCase name. Default: derived from -Name (Trip -> Trips, Category -> Categories).

.PARAMETER HardDelete
    Rows are physically deleted. Default: soft delete (ISoftDeletable), like Car.

.PARAMETER Migrate
    Also run `dotnet ef migrations add Add<Plural>` (needs the dotnet-ef tool).

.PARAMETER SkipFormat
    Do not run `dotnet format` (StyleCop SA1210 using-order fix) afterwards.

.PARAMETER DryRun
    List what would be created and edited; write nothing.

.EXAMPLE
    pwsh ./tool/generate-feature.ps1 Trip -Fields "Destination:text,Fare:decimal,Seats:int"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name,

    [Parameter(Mandatory)]
    [string] $Fields,

    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Plural,

    [switch] $HardDelete,
    [switch] $Migrate,
    [switch] $SkipFormat,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# --- discover the project prefix (rename-proof) -------------------------------------------------
$domainDir = Get-ChildItem -Path (Join-Path $root 'src') -Directory -Filter '*.Domain' | Select-Object -First 1
if (-not $domainDir) { throw "No src/*.Domain project found under $root." }
$P = $domainDir.Name -replace '\.Domain$', ''

# --- names ---------------------------------------------------------------------------------------
function ToCamel([string] $s) { $s.Substring(0, 1).ToLowerInvariant() + $s.Substring(1) }
function ToKebab([string] $s) { ([regex]::Replace($s, '(?<!^)([A-Z])', '-$1')).ToLowerInvariant() }
function Humanize([string] $s) { ([regex]::Replace($s, '(?<!^)([A-Z])', ' $1')).ToLowerInvariant() }

function Pluralize([string] $s) {
    if ($s -match '[^aeiou]y$') { return $s.Substring(0, $s.Length - 1) + 'ies' }
    if ($s -match '(s|x|z|ch|sh)$') { return $s + 'es' }
    return $s + 's'
}

$X = $Name
$Xs = if ($Plural) { $Plural } else { Pluralize $X }
if ($X -ceq $Xs) { throw "Singular and plural are both '$X'. Pass -Plural." }
$entityVar = ToCamel $X
$csKeywords = 'abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char', 'checked', 'class', 'const', 'continue', 'decimal', 'default', 'delegate', 'do', 'double', 'else', 'enum', 'event', 'explicit', 'extern', 'false', 'finally', 'fixed', 'float', 'for', 'foreach', 'goto', 'if', 'implicit', 'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace', 'new', 'null', 'object', 'operator', 'out', 'override', 'params', 'private', 'protected', 'public', 'readonly', 'ref', 'return', 'sbyte', 'sealed', 'short', 'sizeof', 'static', 'string', 'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof', 'uint', 'ulong', 'unchecked', 'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while'
if ($entityVar -in $csKeywords) { throw "'$X' becomes the C# keyword '$entityVar' as a variable name. Choose another entity name." }
$kebabPlural = ToKebab $Xs
$tag = (ToKebab $X)
$Xh = (Humanize $X)
$Xh = $Xh.Substring(0, 1).ToUpperInvariant() + $Xh.Substring(1)
$soft = -not $HardDelete

# --- fields --------------------------------------------------------------------------------------
$validTypes = 'string', 'text', 'int', 'decimal', 'bool'
$fieldList = @()
foreach ($raw in ($Fields -split ',')) {
    $item = $raw.Trim()
    if (-not $item) { continue }
    $parts = $item -split ':'
    $fname = $parts[0].Trim()
    $ftype = if ($parts.Count -gt 1) { $parts[1].Trim().ToLowerInvariant() } else { 'string' }
    if ($fname -cnotmatch '^[A-Z][A-Za-z0-9]*$') { throw "Field name '$fname' must be PascalCase." }
    if ($fname -in 'Id', 'IsDeleted', 'DeletedAtUtc', 'DeletedBy', 'CreatedAtUtc', 'CreatedBy', 'LastModifiedUtc', 'LastModifiedBy', 'DomainEvents') {
        throw "Field name '$fname' is reserved by the base types."
    }
    if ($ftype -notin $validTypes) { throw "Field '$fname': type '$ftype' is not one of $($validTypes -join ', ')." }
    if ($fieldList | Where-Object { $_.Name -ceq $fname }) { throw "Field '$fname' is listed twice." }
    $fieldList += [pscustomobject]@{ Name = $fname; Camel = (ToCamel $fname); Type = $ftype; Human = (Humanize $fname) }
}
if ($fieldList.Count -eq 0) { throw 'Give at least one field, e.g. -Fields "Name:string".' }

function Wire($f) {
    # The members that travel in requests, commands and factory calls.
    if ($f.Type -eq 'text') {
        return @(
            [pscustomobject]@{ Pascal = "$($f.Name)En"; Camel = "$($f.Camel)En"; Cs = 'string'; Field = $f; Part = 'En' },
            [pscustomobject]@{ Pascal = "$($f.Name)Ar"; Camel = "$($f.Camel)Ar"; Cs = 'string'; Field = $f; Part = 'Ar' })
    }
    $cs = switch ($f.Type) { 'string' { 'string' } 'int' { 'int' } 'decimal' { 'decimal' } 'bool' { 'bool' } }
    return @([pscustomobject]@{ Pascal = $f.Name; Camel = $f.Camel; Cs = $cs; Field = $f; Part = '' })
}
$members = @($fieldList | ForEach-Object { Wire $_ })
function Domain-Type($f) { if ($f.Type -eq 'text') { 'LocalizedText' } else { (Wire $f)[0].Cs } }
function Dto-Type($f) { if ($f.Type -eq 'text') { 'string' } else { (Wire $f)[0].Cs } }

# --- guards / safety -----------------------------------------------------------------------------
$domainFolder = Join-Path $root "src/$P.Domain/$Xs"
if (Test-Path -LiteralPath $domainFolder) { throw "src/$P.Domain/$Xs already exists. Pick another name or remove it first." }
$solution = Get-ChildItem -Path $root -Filter *.slnx -File | Select-Object -First 1

$script:created = [System.Collections.Generic.List[string]]::new()
$script:edited = [System.Collections.Generic.List[string]]::new()
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Fill([string] $template, [hashtable] $map) {
    $t = $template
    foreach ($k in $map.Keys) { $t = $t.Replace($k, [string]$map[$k]) }
    $t = $t.Replace('__P__', $P).Replace('__Xs__', $Xs).Replace('__X__', $X).Replace('__x__', $entityVar)
    $t = $t.Replace('__KEBAB__', $kebabPlural).Replace('__TAG__', $tag).Replace('__Xh__', $Xh)
    $t = [regex]::Replace($t, '\r?\n(\s*\r?\n){2,}', "`n`n")
    $t = [regex]::Replace($t, '\{\n[ \t]*\n', "{`n")
    $t = [regex]::Replace($t, '\n[ \t]*\n([ \t]*\})', "`n`$1")
    return $t.TrimEnd() + "`n"
}

function New-SourceFile([string] $relative, [string] $content) {
    $full = Join-Path $root $relative
    if (Test-Path -LiteralPath $full) { throw "$relative already exists." }
    $script:created.Add($relative)
    if ($DryRun) { return }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $full) | Out-Null
    [IO.File]::WriteAllText($full, $content, $utf8)
}

function Edit-SourceFile([string] $relative, [scriptblock] $transform) {
    $full = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $full)) { throw "$relative not found; cannot edit it." }
    $raw = [IO.File]::ReadAllText($full)
    $crlf = $raw.Contains("`r`n")
    $text = $raw.Replace("`r`n", "`n")
    $changed = & $transform $text
    if ($null -eq $changed -or $changed -ceq $text) { throw "Could not place the edit in $relative (anchor not found)." }
    $script:edited.Add($relative)
    if ($DryRun) { return }
    if ($crlf) { $changed = $changed.Replace("`n", "`r`n") }
    [IO.File]::WriteAllText($full, $changed, $utf8)
}

function Add-Using([string] $text, [string] $ns) {
    $line = "using $ns;"
    if ($text -match "(?m)^$([regex]::Escape($line))\s*$") { return $text }
    $lines = [System.Collections.Generic.List[string]]($text -split "`n")

    # Contiguous runs of using lines (a blank line starts a new group, as .editorconfig requires).
    $runs = [System.Collections.Generic.List[object]]::new()
    $current = $null
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^using [^=(]+;\s*$') {
            if ($null -eq $current) { $current = [System.Collections.Generic.List[int]]::new(); $runs.Add($current) }
            $current.Add($i)
        }
        elseif ($lines[$i] -match '^\s*$') { $current = $null }
        elseif ($lines[$i] -match '^(public|internal|\[|/// )') { break }
        else { $current = $null }
    }
    if ($runs.Count -eq 0) { throw 'No using block found.' }

    # Insert into the group that already holds this project's namespaces, in alphabetical position.
    $run = $runs | Where-Object { @($_ | Where-Object { $lines[$_] -like "using $P.*" }).Count -gt 0 } | Select-Object -First 1
    if (-not $run) { $run = $runs[$runs.Count - 1] }
    $at = $run[$run.Count - 1] + 1
    foreach ($index in $run) {
        $existing = $lines[$index].Trim() -replace '^using |;$', ''
        if ([string]::Compare($existing, $ns, [StringComparison]::OrdinalIgnoreCase) -gt 0 -and $existing -notlike 'System*') {
            $at = $index
            break
        }
    }
    $lines.Insert($at, $line)
    return ($lines -join "`n")
}

function Insert-BeforeLastBrace([string] $text, [string] $block) {
    $idx = $text.LastIndexOf("`n}")
    if ($idx -lt 0) { return $null }
    return $text.Substring(0, $idx) + "`n`n" + $block.TrimEnd() + $text.Substring($idx)
}

# --- shared snippets -----------------------------------------------------------------------------
function Join-Lines($items, [string] $indent = '') { ($items | ForEach-Object { "$indent$_" }) -join "`n" }

$paramsTyped = ($members | ForEach-Object { "$($_.Cs) $($_.Camel)" }) -join ', '
$argsPlain = ($members | ForEach-Object { $_.Camel }) -join ', '

function Request-Args([string] $obj) { ($members | ForEach-Object { "$obj.$($_.Pascal)" }) -join ', ' }

function Value-Expr($f, [string] $mode) {
    # How a wire value becomes the stored value inside the entity.
    switch ($f.Type) {
        'string' { "$($f.Camel).Trim()" }
        'text' { "new LocalizedText($($f.Camel)En.Trim(), $($f.Camel)Ar.Trim())" }
        default { $f.Camel }
    }
}

# =================================================================================================
# 1. Localization keys
# =================================================================================================
$keyRows = [System.Collections.Generic.List[object]]::new()   # key, english
function Add-Key([string] $key, [string] $english) { $keyRows.Add([pscustomobject]@{ Key = $key; English = $english }) }

$entityConsts = [System.Collections.Generic.List[string]]::new()
$validationConsts = [System.Collections.Generic.List[string]]::new()
function Add-Const($list, [string] $name, [string] $value) { $list.Add("        public const string $name = `"$value`";") }

Add-Const $entityConsts 'IdRequired' "$X.Id.Required"
Add-Key "$X.Id.Required" "$Xh ID is required"
foreach ($f in $fieldList) {
    switch ($f.Type) {
        { $_ -in 'string', 'text' } {
            $suffix = if ($f.Type -eq 'text') { ' in both languages' } else { '' }
            Add-Const $entityConsts "$($f.Name)Required" "$X.$($f.Name).Required"
            Add-Key "$X.$($f.Name).Required" "$Xh $($f.Human) is required$suffix"
            Add-Const $validationConsts "$X$($f.Name)Required" "Validation.$X$($f.Name).Required"
            Add-Key "Validation.$X$($f.Name).Required" "$Xh $($f.Human) is required."
            Add-Const $validationConsts "$X$($f.Name)TooLong" "Validation.$X$($f.Name).TooLong"
            $max = if ($f.Type -eq 'text') { 1000 } else { 200 }
            Add-Key "Validation.$X$($f.Name).TooLong" "$Xh $($f.Human) must be $max characters or fewer."
        }
        { $_ -in 'int', 'decimal' } {
            Add-Const $entityConsts "$($f.Name)Invalid" "$X.$($f.Name).Invalid"
            Add-Key "$X.$($f.Name).Invalid" "$Xh $($f.Human) is invalid"
            Add-Const $validationConsts "$X$($f.Name)Invalid" "Validation.$X$($f.Name).Invalid"
            Add-Key "Validation.$X$($f.Name).Invalid" "$Xh $($f.Human) must be zero or more."
        }
    }
}
Add-Const $entityConsts 'NotFound' "$X.NotFound"
Add-Key "$X.NotFound" "$Xh not found"

$keysFile = "src/$P.Contracts/Common/LocalizationKeys.cs"
Edit-SourceFile $keysFile {
    param($t)
    $anchor = "    public static class RefreshToken"
    if (-not $t.Contains($anchor)) { return $null }
    $cls = "    public static class $X`n    {`n$($entityConsts -join "`n")`n    }`n`n"
    $t = $t.Replace($anchor, $cls + $anchor)
    $validation = $validationConsts -join "`n"
    if ($validationConsts.Count -gt 0) {
        $idx = $t.LastIndexOf("`n    }`n}")
        if ($idx -lt 0) { return $null }
        $t = $t.Substring(0, $idx) + "`n" + $validation + $t.Substring($idx)
    }
    return $t
}

foreach ($lang in 'en', 'ar') {
    Edit-SourceFile "src/$P.Api/Resources/SharedResource.$lang.json" {
        param($t)
        $body = $t.TrimEnd()
        if (-not $body.EndsWith('}')) { return $null }
        $body = $body.Substring(0, $body.Length - 1).TrimEnd()
        $entries = foreach ($row in $keyRows) {
            $value = if ($lang -eq 'ar') { "TODO-AR: $($row.English)" } else { $row.English }
            $json = $value.Replace('\', '\\').Replace('"', '\"')
            "  `"$($row.Key)`": `"$json`""
        }
        return $body + ",`n" + ($entries -join ",`n") + "`n}`n"
    }
}

# =================================================================================================
# 2. Domain
# =================================================================================================
$ctorParams = ($fieldList | ForEach-Object {
        $t = Domain-Type $_
        "$t $($_.Camel)"
    }) -join ', '
$ctorAssign = Join-Lines ($fieldList | ForEach-Object { "this.$($_.Name) = $($_.Camel);" }) '        '
$createArgs = ($fieldList | ForEach-Object { Value-Expr $_ 'create' }) -join ', '
$props = ($fieldList | ForEach-Object {
        $t = Domain-Type $_
        $init = if ($_.Type -in 'string', 'text') { ' = null!;' } else { '' }
        "    public $t $($_.Name) { get; private set; }$init"
    }) -join "`n`n"
$updateAssign = Join-Lines ($fieldList | ForEach-Object { "this.$($_.Name) = $(Value-Expr $_ 'update');" }) '        '

$guardBlocks = foreach ($f in $fieldList) {
    switch ($f.Type) {
        'string' { "        if (string.IsNullOrWhiteSpace($($f.Camel)))`n        {`n            return $($X)Errors.$($f.Name)Required;`n        }`n" }
        'text' { "        if (string.IsNullOrWhiteSpace($($f.Camel)En) || string.IsNullOrWhiteSpace($($f.Camel)Ar))`n        {`n            return $($X)Errors.$($f.Name)Required;`n        }`n" }
        { $_ -in 'int', 'decimal' } { "        if ($($f.Camel) < 0)`n        {`n            return $($X)Errors.$($f.Name)Invalid;`n        }`n" }
    }
}
$guards = ($guardBlocks | Where-Object { $_ }) -join "`n"

$softIface = if ($soft) { ', ISoftDeletable' } else { '' }
$softProps = if ($soft) {
    "`n    public bool IsDeleted { get; set; }`n`n    public DateTimeOffset? DeletedAtUtc { get; set; }`n`n    public string? DeletedBy { get; set; }`n"
}
else { '' }

$entityTemplate = @'
namespace __P__.Domain.__Xs__;

using __P__.Domain.Common;
using __P__.Domain.Common.Results;

public sealed class __X__ : AuditableEntity__SOFTIFACE__
{
    private __X__()
    {
    }

    private __X__(Guid id, __CTORPARAMS__)
        : base(id)
    {
__CTORASSIGN__
    }

__PROPS__
__SOFTPROPS__
    public static Result<__X__> Create(Guid id, __PARAMS__)
    {
        if (id == Guid.Empty)
        {
            return __X__Errors.IdRequired;
        }

        if (Validate(__ARGS__) is { } error)
        {
            return error;
        }

        return new __X__(id, __CREATEARGS__);
    }

    public Result<Updated> Update(__PARAMS__)
    {
        if (Validate(__ARGS__) is { } error)
        {
            return error;
        }

__UPDATEASSIGN__

        return Result.Updated;
    }

    /// <summary>Shared by Create and Update; runs before any assignment so a rejected update changes nothing.</summary>
    private static Error? Validate(__PARAMS__)
    {
__GUARDS__
        return null;
    }
}
'@
New-SourceFile "src/$P.Domain/$Xs/$X.cs" (Fill $entityTemplate @{
        '__SOFTIFACE__' = $softIface; '__CTORPARAMS__' = $ctorParams; '__CTORASSIGN__' = $ctorAssign
        '__PROPS__' = $props; '__SOFTPROPS__' = $softProps; '__PARAMS__' = $paramsTyped; '__ARGS__' = $argsPlain
        '__CREATEARGS__' = $createArgs; '__UPDATEASSIGN__' = $updateAssign; '__GUARDS__' = $guards
    })

$errorBlocks = [System.Collections.Generic.List[string]]::new()
function Add-ErrorBlock([string] $member, [string] $factory, [string] $keyName, [string] $english) {
    $errorBlocks.Add("    public static readonly Error $member = Error.$factory(`n        code: LocalizationKeys.$X.$keyName,`n        description: `"$english`");")
}
Add-ErrorBlock 'IdRequired' 'Validation' 'IdRequired' "$Xh ID is required."
foreach ($f in $fieldList) {
    switch ($f.Type) {
        'string' { Add-ErrorBlock "$($f.Name)Required" 'Validation' "$($f.Name)Required" "$Xh $($f.Human) is required." }
        'text' { Add-ErrorBlock "$($f.Name)Required" 'Validation' "$($f.Name)Required" "$Xh $($f.Human) is required in both languages." }
        { $_ -in 'int', 'decimal' } { Add-ErrorBlock "$($f.Name)Invalid" 'Validation' "$($f.Name)Invalid" "$Xh $($f.Human) is invalid." }
    }
}
Add-ErrorBlock 'NotFound' 'NotFound' 'NotFound' "$Xh was not found."

$errorsTemplate = @'
using __P__.Contracts.Common;
using __P__.Domain.Common.Results;

namespace __P__.Domain.__Xs__;

public static class __X__Errors
{
__ERRORS__
}
'@
New-SourceFile "src/$P.Domain/$Xs/$($X)Errors.cs" (Fill $errorsTemplate @{ '__ERRORS__' = ($errorBlocks -join "`n`n") })

# =================================================================================================
# 3. Contracts requests
# =================================================================================================
function Request-Props {
    $blocks = foreach ($m in $members) {
        $f = $m.Field
        switch ($f.Type) {
            'string' {
                "    [Required(ErrorMessage = LocalizationKeys.Validation.$X$($f.Name)Required)]`n    [StringLength(200, ErrorMessage = LocalizationKeys.Validation.$X$($f.Name)TooLong)]`n    public string $($m.Pascal) { get; set; } = string.Empty;"
            }
            'text' {
                "    [Required(ErrorMessage = LocalizationKeys.Validation.$X$($f.Name)Required)]`n    [StringLength(1000, ErrorMessage = LocalizationKeys.Validation.$X$($f.Name)TooLong)]`n    public string $($m.Pascal) { get; set; } = string.Empty;"
            }
            'int' { "    [Range(0, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.$X$($f.Name)Invalid)]`n    public int $($m.Pascal) { get; set; }" }
            'decimal' { "    [Range(0, double.MaxValue, ErrorMessage = LocalizationKeys.Validation.$X$($f.Name)Invalid)]`n    public decimal $($m.Pascal) { get; set; }" }
            'bool' { "    public bool $($m.Pascal) { get; set; }" }
        }
    }
    $blocks -join "`n`n"
}
$requestTemplate = @'
using System.ComponentModel.DataAnnotations;
using __P__.Contracts.Common;

namespace __P__.Contracts.Requests.__Xs__;

__DOC__
public class __CLASS__
{
__PROPS__
}
'@
$reqProps = Request-Props
New-SourceFile "src/$P.Contracts/Requests/$Xs/Create$($X)Request.cs" (Fill $requestTemplate @{ '__DOC__' = ''; '__CLASS__' = "Create$($X)Request"; '__PROPS__' = $reqProps })
New-SourceFile "src/$P.Contracts/Requests/$Xs/Update$($X)Request.cs" (Fill $requestTemplate @{
        '__DOC__' = "/// <summary>`n/// Body of <c>PUT /api/v1/$kebabPlural/{id}</c>. Deliberately has no <c>Id</c>: the route is the single`n/// source of the resource identity.`n/// </summary>"
        '__CLASS__' = "Update$($X)Request"; '__PROPS__' = $reqProps
    })

$getRequestTemplate = @'
using System.ComponentModel.DataAnnotations;
using __P__.Contracts.Common;

namespace __P__.Contracts.Requests.__Xs__;

/// <summary>Query string of <c>GET /api/v1/__KEBAB__</c>. <see cref="Search"/> matches the text fields.</summary>
public class Get__Xs__Request
{
    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageInvalid)]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageSizeInvalid)]
    public int PageSize { get; set; } = 20;

    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.SearchTooLong)]
    public string? Search { get; set; }
}
'@
New-SourceFile "src/$P.Contracts/Requests/$Xs/Get$($Xs)Request.cs" (Fill $getRequestTemplate @{})

# =================================================================================================
# 4. Application
# =================================================================================================
$feature = "src/$P.Application/Features/$Xs"
$ns = "$P.Application.Features.$Xs"

# DTO + mapper
$dtoParams = (@('Guid Id') + ($fieldList | ForEach-Object {
            $t = Dto-Type $_
            "$t $($_.Name)"
        })) -join ', '
New-SourceFile "$feature/Dtos/$($X)Dto.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Dtos;

public record __X__Dto(__DTOPARAMS__);
'@ @{ '__DTOPARAMS__' = $dtoParams })

$dtoArgs = (@("$entityVar.Id") + ($fieldList | ForEach-Object {
            if ($_.Type -eq 'text') { "language == Languages.Ar ? $entityVar.$($_.Name).Ar : $entityVar.$($_.Name).En" } else { "$entityVar.$($_.Name)" }
        })) -join ",`n            "
New-SourceFile "$feature/Mappers/$($X)Mapper.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Mappers;

using __P__.Application.Features.__Xs__.Dtos;
using __P__.Contracts.Common;
using __P__.Domain.__Xs__;

public static class __X__Mapper
{
    public static __X__Dto ToDto(this __X__ __x__, string language = Languages.Default)
    {
        return new __X__Dto(
            __DTOARGS__);
    }
}
'@ @{ '__DTOARGS__' = $dtoArgs })

# commands
$commandParams = ($members | ForEach-Object { "    $($_.Cs) $($_.Pascal)" }) -join ",`n"
$createArgsFromRequest = Request-Args 'request'

New-SourceFile "$feature/Commands/Create$X/Create$($X)Command.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Commands.Create__X__;

using MediatR;
using __P__.Application.Features.__Xs__.Dtos;
using __P__.Domain.Common.Results;

public record Create__X__Command(
__CMDPARAMS__) : IRequest<Result<__X__Dto>>;
'@ @{ '__CMDPARAMS__' = $commandParams })

New-SourceFile "$feature/Commands/Update$X/Update$($X)Command.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Commands.Update__X__;

using MediatR;
using __P__.Domain.Common.Results;

public record Update__X__Command(
    Guid Id,
__CMDPARAMS__) : IRequest<Result<Updated>>;
'@ @{ '__CMDPARAMS__' = $commandParams })

New-SourceFile "$feature/Commands/Remove$X/Remove$($X)Command.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Commands.Remove__X__;

using MediatR;
using __P__.Domain.Common.Results;

public record Remove__X__Command(Guid Id) : IRequest<Result<Deleted>>;
'@ @{})

# validators
function Validator-Rules {
    $rules = foreach ($m in $members) {
        $f = $m.Field
        switch ($f.Type) {
            { $_ -in 'string', 'text' } {
                $max = if ($f.Type -eq 'text') { 1000 } else { 200 }
                "        RuleFor(x => x.$($m.Pascal))`n            .NotEmpty().WithMessage(LocalizationKeys.Validation.$X$($f.Name)Required)`n            .MaximumLength($max).WithMessage(LocalizationKeys.Validation.$X$($f.Name)TooLong);"
            }
            { $_ -in 'int', 'decimal' } {
                "        RuleFor(x => x.$($m.Pascal))`n            .GreaterThanOrEqualTo(0).WithMessage(LocalizationKeys.Validation.$X$($f.Name)Invalid);"
            }
        }
    }
    ($rules | Where-Object { $_ }) -join "`n`n"
}
$rules = Validator-Rules
$validatorTemplate = @'
using FluentValidation;
using __P__.Contracts.Common;

namespace __P__.Application.Features.__Xs__.Commands.__VERB____X__;

public sealed class __VERB____X__CommandValidator : AbstractValidator<__VERB____X__Command>
{
    public __VERB____X__CommandValidator()
    {
__IDRULE____RULES__
    }
}
'@
$idRule = "        RuleFor(x => x.Id)`n            .NotEmpty().WithMessage(LocalizationKeys.$X.IdRequired);`n`n"
New-SourceFile "$feature/Commands/Create$X/Create$($X)CommandValidator.cs" (Fill $validatorTemplate @{ '__VERB__' = 'Create'; '__IDRULE__' = ''; '__RULES__' = $rules })
New-SourceFile "$feature/Commands/Update$X/Update$($X)CommandValidator.cs" (Fill $validatorTemplate @{ '__VERB__' = 'Update'; '__IDRULE__' = $idRule; '__RULES__' = $rules })

# handlers
New-SourceFile "$feature/Commands/Create$X/Create$($X)CommandHandler.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Commands.Create__X__;

using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using __P__.Application.Common.Caching;
using __P__.Application.Common.Interfaces;
using __P__.Application.Features.__Xs__.Dtos;
using __P__.Application.Features.__Xs__.Mappers;
using __P__.Domain.Common.Results;
using __P__.Domain.__Xs__;

public class Create__X__CommandHandler(
    IAppDbContext context,
    HybridCache cache,
    ILanguageContext languageContext) : IRequestHandler<Create__X__Command, Result<__X__Dto>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<__X__Dto>> Handle(Create__X__Command request, CancellationToken cancellationToken)
    {
        var __x__Result = __X__.Create(Guid.NewGuid(), __REQARGS__);

        if (__x__Result.IsError)
        {
            return __x__Result.Errors;
        }

        var __x__ = __x__Result.Value;

        this.context.__Xs__.Add(__x__);

        await this.context.SaveChangesAsync(cancellationToken);

        await this.cache.RemoveByTagAsync(CacheTags.__Xs__, cancellationToken);

        return __x__.ToDto(this.languageContext.Language);
    }
}
'@ @{ '__REQARGS__' = $createArgsFromRequest })

New-SourceFile "$feature/Commands/Update$X/Update$($X)CommandHandler.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Commands.Update__X__;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using __P__.Application.Common.Caching;
using __P__.Application.Common.Interfaces;
using __P__.Domain.Common.Results;
using __P__.Domain.__Xs__;

public class Update__X__CommandHandler(IAppDbContext context, HybridCache cache) : IRequestHandler<Update__X__Command, Result<Updated>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Updated>> Handle(Update__X__Command request, CancellationToken cancellationToken)
    {
        var __x__ = await this.context.__Xs__
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (__x__ is null)
        {
            return __X__Errors.NotFound;
        }

        var updateResult = __x__.Update(__REQARGS__);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await this.context.SaveChangesAsync(cancellationToken);

        await this.cache.RemoveByTagAsync(CacheTags.__Xs__, cancellationToken);

        return Result.Updated;
    }
}
'@ @{ '__REQARGS__' = $createArgsFromRequest })

New-SourceFile "$feature/Commands/Remove$X/Remove$($X)CommandHandler.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Commands.Remove__X__;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using __P__.Application.Common.Caching;
using __P__.Application.Common.Interfaces;
using __P__.Domain.Common.Results;
using __P__.Domain.__Xs__;

public class Remove__X__CommandHandler(IAppDbContext context, HybridCache cache) : IRequestHandler<Remove__X__Command, Result<Deleted>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Deleted>> Handle(Remove__X__Command request, CancellationToken cancellationToken)
    {
        var __x__ = await this.context.__Xs__
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (__x__ is null)
        {
            return __X__Errors.NotFound;
        }

        this.context.__Xs__.Remove(__x__);

        await this.context.SaveChangesAsync(cancellationToken);

        await this.cache.RemoveByTagAsync(CacheTags.__Xs__, cancellationToken);

        return Result.Deleted;
    }
}
'@ @{})

# queries
New-SourceFile "$feature/Queries/Get$($X)ById/Get$($X)ByIdQuery.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Queries.Get__X__ById;

using MediatR;
using __P__.Application.Common.Caching;
using __P__.Application.Common.Interfaces;
using __P__.Application.Features.__Xs__.Dtos;
using __P__.Domain.Common.Results;

public record Get__X__ByIdQuery(Guid Id) : ICachedQuery<Result<__X__Dto>>
{
    public string CacheKey => $"__KEBAB__-{this.Id}";

    public string[] Tags => [CacheTags.__Xs__];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
'@ @{})

New-SourceFile "$feature/Queries/Get$($X)ById/Get$($X)ByIdQueryHandler.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Queries.Get__X__ById;

using MediatR;
using Microsoft.EntityFrameworkCore;
using __P__.Application.Common.Interfaces;
using __P__.Application.Features.__Xs__.Dtos;
using __P__.Application.Features.__Xs__.Mappers;
using __P__.Domain.Common.Results;
using __P__.Domain.__Xs__;

public class Get__X__ByIdQueryHandler(IAppDbContext context, ILanguageContext languageContext) : IRequestHandler<Get__X__ByIdQuery, Result<__X__Dto>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<__X__Dto>> Handle(Get__X__ByIdQuery request, CancellationToken cancellationToken)
    {
        var __x__ = await this.context.__Xs__
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (__x__ is null)
        {
            return __X__Errors.NotFound;
        }

        return __x__.ToDto(this.languageContext.Language);
    }
}
'@ @{})

New-SourceFile "$feature/Queries/Get$($X)ById/Get$($X)ByIdQueryValidator.cs" (Fill @'
using FluentValidation;
using __P__.Contracts.Common;

namespace __P__.Application.Features.__Xs__.Queries.Get__X__ById;

public sealed class Get__X__ByIdQueryValidator : AbstractValidator<Get__X__ByIdQuery>
{
    public Get__X__ByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(LocalizationKeys.__X__.IdRequired);
    }
}
'@ @{})

New-SourceFile "$feature/Queries/Get$Xs/Get$($Xs)Query.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Queries.Get__Xs__;

using __P__.Application.Common.Caching;
using __P__.Application.Common.Interfaces;
using __P__.Application.Common.Models;
using __P__.Application.Features.__Xs__.Dtos;
using __P__.Domain.Common.Results;

/// <summary>One page of __Xs__, newest first. <paramref name="Search"/> matches the text fields.</summary>
public record Get__Xs__Query(
    int Page = Paging.DefaultPage,
    int PageSize = Paging.DefaultPageSize,
    string? Search = null) : ICachedQuery<Result<PaginatedList<__X__Dto>>>
{
    public string CacheKey =>
        $"__KEBAB__-p{this.Page}-s{Paging.ClampPageSize(this.PageSize)}-q{this.Search?.Trim().ToLowerInvariant()}";

    public string[] Tags => [CacheTags.__Xs__];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
'@ @{})

$stringFields = @($fieldList | Where-Object { $_.Type -eq 'string' })
$searchBlock = if ($stringFields.Count -gt 0) {
    $cond = ($stringFields | ForEach-Object { "e.$($_.Name).ToLower().Contains(term)" }) -join ' || '
    "        if (!string.IsNullOrWhiteSpace(request.Search))`n        {`n            var term = request.Search.Trim().ToLower();`n            query = query.Where(e => $cond);`n        }`n"
}
else { '' }
New-SourceFile "$feature/Queries/Get$Xs/Get$($Xs)QueryHandler.cs" (Fill @'
namespace __P__.Application.Features.__Xs__.Queries.Get__Xs__;

using MediatR;
using Microsoft.EntityFrameworkCore;
using __P__.Application.Common.Interfaces;
using __P__.Application.Common.Models;
using __P__.Application.Features.__Xs__.Dtos;
using __P__.Application.Features.__Xs__.Mappers;
using __P__.Domain.Common.Results;
using __P__.Domain.__Xs__;

public class Get__Xs__QueryHandler(IAppDbContext context, ILanguageContext languageContext)
    : IRequestHandler<Get__Xs__Query, Result<PaginatedList<__X__Dto>>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<PaginatedList<__X__Dto>>> Handle(Get__Xs__Query request, CancellationToken cancellationToken)
    {
        var query = this.context.__Xs__.AsNoTracking();

__SEARCH__
        var page = await PaginatedList<__X__>.CreateAsync(
            query.OrderByDescending(e => e.CreatedAtUtc).ThenBy(e => e.Id),
            request.Page,
            request.PageSize,
            cancellationToken);

        return page.Map(__x__ => __x__.ToDto(this.languageContext.Language));
    }
}
'@ @{ '__SEARCH__' = $searchBlock })

New-SourceFile "$feature/Queries/Get$Xs/Get$($Xs)QueryValidator.cs" (Fill @'
using FluentValidation;
using __P__.Contracts.Common;

namespace __P__.Application.Features.__Xs__.Queries.Get__Xs__;

public sealed class Get__Xs__QueryValidator : AbstractValidator<Get__Xs__Query>
{
    public Get__Xs__QueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageInvalid);

        // No upper bound: anything above Paging.MaxPageSize is clamped, not rejected.
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageSizeInvalid);

        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage(LocalizationKeys.Validation.SearchTooLong);
    }
}
'@ @{})

# cache tag, DbSet, policies (edits)
Edit-SourceFile "src/$P.Application/Common/Caching/CacheTags.cs" {
    param($t)
    Insert-BeforeLastBrace $t "    /// <summary>Everything derived from the <c>$Xs</c> table.</summary>`n    public const string $Xs = `"$tag`";"
}

Edit-SourceFile "src/$P.Application/Common/Interfaces/IAppDbContext.cs" {
    param($t)
    $anchor = '    Task<int> SaveChangesAsync'
    if (-not $t.Contains($anchor)) { return $null }
    $t = $t.Replace($anchor, "    public DbSet<$X> $Xs { get; }`n`n$anchor")
    Add-Using $t "$P.Domain.$Xs"
}

Edit-SourceFile "src/$P.Infrastructure/Data/AppDbContext.cs" {
    param($t)
    $anchor = '    public DbSet<RefreshToken> RefreshTokens'
    if (-not $t.Contains($anchor)) { return $null }
    $t = $t.Replace($anchor, "    public DbSet<$X> $Xs => this.Set<$X>();`n`n$anchor")
    Add-Using $t "$P.Domain.$Xs"
}

Edit-SourceFile "tests/$P.Application.UnitTests/TestDoubles/TestAppDbContext.cs" {
    param($t)
    $setAnchor = '    public DbSet<RefreshToken> RefreshTokens'
    $modelAnchor = '        modelBuilder.Entity<RefreshToken>()'
    if (-not $t.Contains($setAnchor) -or -not $t.Contains($modelAnchor)) { return $null }
    $t = $t.Replace($setAnchor, "    public DbSet<$X> $Xs => this.Set<$X>();`n`n$setAnchor")
    $owned = ($fieldList | Where-Object { $_.Type -eq 'text' } | ForEach-Object { "            e.OwnsOne(x => x.$($_.Name));" }) -join "`n"
    $softFilter = if ($soft) { "`n            e.HasQueryFilter(x => !x.IsDeleted);" } else { '' }
    $ownedBlock = if ($owned) { "`n$owned" } else { '' }
    $model = "        modelBuilder.Entity<$X>(e =>`n        {`n            e.HasKey(x => x.Id);$ownedBlock$softFilter`n        });`n`n"
    $t = $t.Replace($modelAnchor, $model + $modelAnchor)
    Add-Using $t "$P.Domain.$Xs"
}

Edit-SourceFile "src/$P.Application/Common/Security/Policies.cs" {
    param($t)
    $block = "    /// <summary>Read $($Xs.ToLowerInvariant()). Granted to <see cref=`"Roles.Manager`"/> and <see cref=`"Roles.User`"/>.</summary>`n    public const string CanRead$Xs = nameof(CanRead$Xs);`n`n    /// <summary>Create, update and delete $($Xs.ToLowerInvariant()). Granted to <see cref=`"Roles.Manager`"/>.</summary>`n    public const string CanManage$Xs = nameof(CanManage$Xs);"
    Insert-BeforeLastBrace $t $block
}

Edit-SourceFile "src/$P.Api/DependencyInjection.cs" {
    param($t)
    $anchor = '.AddPolicy(Policies.CanManageUsers, policy => policy.RequireRole(Roles.Manager));'
    if (-not $t.Contains($anchor)) { return $null }
    $replacement = ".AddPolicy(Policies.CanManageUsers, policy => policy.RequireRole(Roles.Manager))`n            .AddPolicy(Policies.CanRead$Xs, policy => policy.RequireRole(Roles.Manager, Roles.User))`n            .AddPolicy(Policies.CanManage$Xs, policy => policy.RequireRole(Roles.Manager));"
    $t.Replace($anchor, $replacement)
}

# =================================================================================================
# 5. Infrastructure configuration
# =================================================================================================
$cfgLines = foreach ($f in $fieldList) {
    switch ($f.Type) {
        'string' { "        builder.Property(e => e.$($f.Name))`n               .IsRequired()`n               .HasMaxLength(200);" }
        'text' { "        builder.OwnsOne(e => e.$($f.Name), value =>`n        {`n            value.ToJson();`n            value.Property(v => v.En).IsRequired();`n            value.Property(v => v.Ar).IsRequired();`n        });" }
        'decimal' { "        builder.Property(e => e.$($f.Name))`n               .IsRequired()`n               .HasPrecision(18, 2);" }
        default { "        builder.Property(e => e.$($f.Name))`n               .IsRequired();" }
    }
}
$cfgSoft = if ($soft) {
    "`n`n        builder.Property(e => e.IsDeleted)`n               .HasDefaultValue(false);`n`n        builder.Property(e => e.DeletedBy)`n               .HasMaxLength(450);"
}
else { '' }
New-SourceFile "src/$P.Infrastructure/Data/Configurations/$($X)Configuration.cs" (Fill @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using __P__.Domain.__Xs__;

namespace __P__.Infrastructure.Data.Configurations;

public class __X__Configuration : IEntityTypeConfiguration<__X__>
{
    public void Configure(EntityTypeBuilder<__X__> builder)
    {
        builder.HasKey(e => e.Id);

__PROPS____SOFT__

        builder.UseXminConcurrencyToken();
    }
}
'@ @{ '__PROPS__' = ($cfgLines -join "`n`n"); '__SOFT__' = $cfgSoft })

# =================================================================================================
# 6. Controller
# =================================================================================================
New-SourceFile "src/$P.Api/Controllers/$($Xs)Controller.cs" (Fill @'
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using __P__.Api.Infrastructure.Idempotency;
using __P__.Application.Common.Models;
using __P__.Application.Common.Security;
using __P__.Application.Features.__Xs__.Commands.Create__X__;
using __P__.Application.Features.__Xs__.Commands.Remove__X__;
using __P__.Application.Features.__Xs__.Commands.Update__X__;
using __P__.Application.Features.__Xs__.Dtos;
using __P__.Application.Features.__Xs__.Queries.Get__X__ById;
using __P__.Application.Features.__Xs__.Queries.Get__Xs__;
using __P__.Contracts.Requests.__Xs__;

namespace __P__.Api.Controllers;

[Route("api/v{version:apiVersion}/__KEBAB__")]
[ApiVersion("1.0")]
[Authorize(Policy = Policies.CanRead__Xs__)]
public sealed class __Xs__Controller(ISender sender) : ApiController
{
    private readonly ISender sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<__X__Dto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("Get__Xs__")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Get__Xs__([FromQuery] Get__Xs__Request request, CancellationToken ct)
    {
        var result = await this.sender.Send(new Get__Xs__Query(request.Page, request.PageSize, request.Search), ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(__X__Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("Get__X__")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Get__X__(Guid id, CancellationToken ct)
    {
        var result = await this.sender.Send(new Get__X__ByIdQuery(id), ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpPost]
    [ProducesResponseType(typeof(__X__Dto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EndpointName("Create__X__")]
    [Idempotent]
    [Authorize(Policy = Policies.CanManage__Xs__)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Create__X__([FromBody] Create__X__Request request, CancellationToken ct)
    {
        var result = await this.sender.Send(new Create__X__Command(__REQARGS__), ct);

        return result.Match(
            __x__ => this.CreatedAtAction(nameof(this.Get__X__), new { version = this.RequestedApiVersion, id = __x__.Id }, __x__),
            this.Problem);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EndpointName("Update__X__")]
    [Authorize(Policy = Policies.CanManage__Xs__)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Update__X__(Guid id, [FromBody] Update__X__Request request, CancellationToken ct)
    {
        var result = await this.sender.Send(new Update__X__Command(id, __REQARGS__), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EndpointName("Remove__X__")]
    [Authorize(Policy = Policies.CanManage__Xs__)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Remove__X__(Guid id, CancellationToken ct)
    {
        var result = await this.sender.Send(new Remove__X__Command(id), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }
}
'@ @{ '__REQARGS__' = $createArgsFromRequest })

# =================================================================================================
# 7. Domain tests
# =================================================================================================
function Valid-Literal($m) {
    switch ($m.Field.Type) {
        'string' { '"Sample"' }
        'text' { if ($m.Part -eq 'En') { '"Sample"' } else { '"عينة"' } }
        'int' { '1' }
        'decimal' { '1.5m' }
        'bool' { 'true' }
    }
}
function Valid-Const($m) {
    $v = Valid-Literal $m
    switch ($m.Cs) {
        'string' { "    private const string Valid$($m.Pascal) = $v;" }
        'int' { "    private const int Valid$($m.Pascal) = $v;" }
        'decimal' { "    private const decimal Valid$($m.Pascal) = $v;" }
        'bool' { "    private const bool Valid$($m.Pascal) = $v;" }
    }
}
function Test-Args([string] $overridePascal, [string] $overrideValue) {
    ($members | ForEach-Object { if ($_.Pascal -eq $overridePascal) { $overrideValue } else { "Valid$($_.Pascal)" } }) -join ', '
}

$testConsts = ($members | ForEach-Object { Valid-Const $_ }) -join "`n"
$validArgs = ($members | ForEach-Object { "Valid$($_.Pascal)" }) -join ', '
$assertLines = foreach ($f in $fieldList) {
    if ($f.Type -eq 'text') {
        "Assert.Equal(Valid$($f.Name)En, result.Value.$($f.Name).En);"
        "Assert.Equal(Valid$($f.Name)Ar, result.Value.$($f.Name).Ar);"
    }
    else {
        "Assert.Equal(Valid$($f.Name), result.Value.$($f.Name));"
    }
}
$assertValues = Join-Lines $assertLines '        '

$invalidTests = foreach ($m in $members) {
    $f = $m.Field
    if ($f.Type -eq 'bool') { continue }
    $bad = switch ($f.Type) { 'int' { '-1' } 'decimal' { '-1m' } default { '"   "' } }
    $code = if ($f.Type -in 'int', 'decimal') { "$($f.Name)Invalid" } else { "$($f.Name)Required" }
    "    [Fact]`n    public void Create_Fails_When$($m.Pascal)IsInvalid()`n    {`n        var result = $X.Create(Guid.NewGuid(), $(Test-Args $m.Pascal $bad));`n`n        Assert.True(result.IsError);`n        Assert.Equal(LocalizationKeys.$X.$code, result.TopError.Code);`n    }"
}

$firstChecked = $members | Where-Object { $_.Field.Type -ne 'bool' } | Select-Object -First 1
$untouchedTest = ''
if ($firstChecked) {
    $bad = switch ($firstChecked.Field.Type) { 'int' { '-1' } 'decimal' { '-1m' } default { '"   "' } }
    $prop = if ($firstChecked.Field.Type -eq 'text') { "$($firstChecked.Field.Name).$($firstChecked.Part)" } else { $firstChecked.Field.Name }
    $untouchedTest = "`n`n    [Fact]`n    public void Update_LeavesTheEntityUntouched_WhenAnInputIsInvalid()`n    {`n        var $entityVar = $X.Create(Guid.NewGuid(), $validArgs).Value;`n`n        var result = $entityVar.Update($(Test-Args $firstChecked.Pascal $bad));`n`n        Assert.True(result.IsError);`n        Assert.Equal(Valid$($firstChecked.Pascal), $entityVar.$prop);`n    }"
}

New-SourceFile "tests/$P.Domain.UnitTests/$Xs/$($X)Tests.cs" (Fill @'
using __P__.Contracts.Common;
using __P__.Domain.__Xs__;

using Xunit;

namespace __P__.Domain.UnitTests.__Xs__;

public class __X__Tests
{
__CONSTS__

    [Fact]
    public void Create_ReturnsTheEntity_WhenAllInputsAreValid()
    {
        var result = __X__.Create(Guid.NewGuid(), __VALIDARGS__);

        Assert.True(result.IsSuccess);
__ASSERTS__
    }

    [Fact]
    public void Create_Fails_WhenIdIsEmpty()
    {
        var result = __X__.Create(Guid.Empty, __VALIDARGS__);

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.__X__.IdRequired, result.TopError.Code);
    }

__INVALID__

    [Fact]
    public void Update_Succeeds_WhenAllInputsAreValid()
    {
        var __x__ = __X__.Create(Guid.NewGuid(), __VALIDARGS__).Value;

        var result = __x__.Update(__VALIDARGS__);

        Assert.True(result.IsSuccess);
    }__UNTOUCHED__
}
'@ @{ '__CONSTS__' = $testConsts; '__VALIDARGS__' = $validArgs; '__ASSERTS__' = $assertValues; '__INVALID__' = ($invalidTests -join "`n`n"); '__UNTOUCHED__' = $untouchedTest })

# =================================================================================================
# 8. requests.http
# =================================================================================================
function Json-Value($m) {
    switch ($m.Field.Type) {
        'string' { '"Sample"' }
        'text' { if ($m.Part -eq 'En') { '"Sample"' } else { '"عينة"' } }
        'int' { '1' }
        'decimal' { '9.5' }
        'bool' { 'true' }
    }
}
$jsonBody = ($members | ForEach-Object { "  `"$(ToCamel $_.Pascal)`": $(Json-Value $_)" }) -join ",`n"
$http = @"

### ---------------------------------------------------------------------------
### $Xs (generated by tool/generate-feature.ps1; edit the sample values)

### Create $X → 201 + Location: /api/v1/$kebabPlural/{guid}
# @name create$X
POST {{baseUrl}}/api/v1/$kebabPlural
Authorization: Bearer {{token}}
Content-Type: application/json
Accept-Language: {{lang}}

{
$jsonBody
}

@$($entityVar)Id = {{create$X.response.body.id}}

### List $Xs (paged, cached per language)
GET {{baseUrl}}/api/v1/$kebabPlural
Authorization: Bearer {{token}}
Accept-Language: {{lang}}

### Get one (cached)
GET {{baseUrl}}/api/v1/$kebabPlural/{{$($entityVar)Id}}
Authorization: Bearer {{token}}
Accept-Language: {{lang}}

### Update → 204, evicts the "$tag" cache tag
PUT {{baseUrl}}/api/v1/$kebabPlural/{{$($entityVar)Id}}
Authorization: Bearer {{token}}
Content-Type: application/json

{
$jsonBody
}

### Delete → 204
DELETE {{baseUrl}}/api/v1/$kebabPlural/{{$($entityVar)Id}}
Authorization: Bearer {{token}}
"@
Edit-SourceFile 'requests/requests.http' { param($t) $t.TrimEnd() + "`n" + $http.Replace("`r`n", "`n").TrimEnd() + "`n" }

# =================================================================================================
# 9. Format + migration + summary
# =================================================================================================
$verb = if ($DryRun) { 'Would create' } else { 'Created' }
Write-Host ''
Write-Host "$verb $($script:created.Count) file(s):" -ForegroundColor Green
$script:created | ForEach-Object { Write-Host "  + $_" }
Write-Host "$(if ($DryRun) { 'Would edit' } else { 'Edited' }) $($script:edited.Count) file(s):" -ForegroundColor Green
$script:edited | ForEach-Object { Write-Host "  ~ $_" }

if ($DryRun) { return }

if (-not $SkipFormat -and $solution -and (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host ''
    Write-Host 'Sorting using directives (dotnet format, StyleCop SA1210) ...'
    dotnet format analyzers $solution.FullName --diagnostics SA1210 --severity info --no-restore 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning "dotnet format failed; run: dotnet format analyzers $($solution.Name) --diagnostics SA1210 --severity info" }
}

if ($Migrate) {
    Write-Host ''
    Write-Host "Adding migration Add$Xs ..."
    dotnet ef migrations add "Add$Xs" -p "src/$P.Infrastructure" -s "src/$P.Api"
    if ($LASTEXITCODE -ne 0) { Write-Warning 'Migration failed (is the dotnet-ef tool installed? dotnet tool install --global dotnet-ef).' }
}

Write-Host ''
Write-Host 'Next steps' -ForegroundColor Yellow
Write-Host "  1. Translate the TODO-AR values in src/$P.Api/Resources/SharedResource.ar.json  ($($keyRows.Count) keys: $X.*, Validation.$X*)."
if (-not $Migrate) { Write-Host "  2. dotnet ef migrations add Add$Xs -p src/$P.Infrastructure -s src/$P.Api   (then read the migration)" }
Write-Host "  3. dotnet build $($solution.Name) -c Debug   (must be 0 warnings), then dotnet test tests/$P.Domain.UnitTests"
Write-Host "  4. Add the real business rules to src/$P.Domain/$Xs/$X.cs and its tests; add integration tests (copy CarsTests)."
