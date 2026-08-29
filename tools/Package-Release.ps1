<#
.SYNOPSIS
    Builds the self-contained handover archives for the association sale tool.

.DESCRIPTION
    Publishes the web project once per runtime identifier, adds the operator
    files from deploy/, stamps a VERSION.txt, and zips each result into dist/.

    The archives are fully self-contained: the target machine needs no .NET
    runtime, only the ICU library on Linux.

.EXAMPLE
    pwsh tools/Package-Release.ps1
    Builds both linux-x64 and win-x64 archives into dist/.

.EXAMPLE
    pwsh tools/Package-Release.ps1 -Runtime linux-x64
    Builds only the Debian archive.
#>
[CmdletBinding()]
param(
    [string[]] $Runtime = @('linux-x64', 'win-x64'),
    [string]   $OutputDir = 'dist',
    [string]   $Version
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'src\AssociationSaleTool.Web\AssociationSaleTool.Web.csproj'
$deployDir = Join-Path $repoRoot 'deploy'
$staging  = Join-Path $repoRoot '.tmp\package'
$outPath  = Join-Path $repoRoot $OutputDir

if ([string]::IsNullOrWhiteSpace($Version)) {
    $sha = (git -C $repoRoot rev-parse --short HEAD).Trim()
    $Version = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd'), $sha
}

# --- Pre-flight: the operator guide no longer covers configuration, so the
# --- shipped appsettings.json must already hold the real event values.
$settings = Get-Content (Join-Path $repoRoot 'src\AssociationSaleTool.Web\appsettings.json') -Raw | ConvertFrom-Json
if ($settings.Admin.SharedPassword -in @('tada', 'change-me', '')) {
    Write-Warning "Admin:SharedPassword is still the placeholder '$($settings.Admin.SharedPassword)'. Set the real password before handing these archives over."
}
Write-Host "Event name  : $($settings.Event.Name)"
Write-Host "Database    : $($settings.Database.SqlitePath)"
Write-Host "Version     : $Version"
Write-Host ""

if (-not (Test-Path $outPath)) { New-Item -ItemType Directory -Path $outPath | Out-Null }

foreach ($rid in $Runtime) {
    Write-Host "=== Publishing $rid ==="
    $stage = Join-Path $staging $rid
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

    dotnet publish $project -c Release -r $rid --self-contained true -o $stage
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid" }

    # Development overrides have no place in a handover archive.
    $devSettings = Join-Path $stage 'appsettings.Development.json'
    if (Test-Path $devSettings) { Remove-Item $devSettings -Force }

    Copy-Item (Join-Path $deployDir 'INSTALLATION.md') $stage
    if ($rid -like 'win-*') {
        Copy-Item (Join-Path $deployDir 'demarrer.bat') $stage
    }
    else {
        Copy-Item (Join-Path $deployDir 'demarrer.sh') $stage
    }

    $stamp = @(
        "Outil de bourse aux jouets"
        "Version     : $Version"
        "Runtime     : $rid"
        "Construit le: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        "Commit      : $(git -C $repoRoot rev-parse HEAD)"
    ) -join "`r`n"
    Set-Content -Path (Join-Path $stage 'VERSION.txt') -Value $stamp -Encoding utf8

    $zip = Join-Path $outPath "bourse-aux-jouets-$rid.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    $sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "-> $zip ($sizeMb MB)"
    Write-Host ""
}

Write-Host "Done. Archives in $outPath"
