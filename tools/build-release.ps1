<#
.SYNOPSIS
    Builds a Hometown Store Access release: runs the unit tests, builds the plugin, packs
    HometownStoreAccess-<version>.zip (BepInEx + the mod) and builds HometownStoreAccessInstaller.exe
    with that zip embedded.

.DESCRIPTION
    Needs the .NET SDK and an installed copy of Hometown Store (the plugin compiles against the game's
    assemblies; set HOMETOWN_STORE_DIR or pass -GameDir when the game is not in the default Steam folder).
    BepInEx is downloaded once from its GitHub release and checked against a pinned SHA-256 hash.

    Output, in dist\:
      HometownStoreAccess-<version>.zip     manual install: extract into the game folder
      HometownStoreAccessInstaller.exe      installer, works offline (the zip is embedded)
      SHA256SUMS.txt

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\build-release.ps1
#>
param(
    [string]$GameDir = "",
    [string]$BepInExZip = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$BepInExVersion = "5.4.23.6"
$BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"
$BepInExSha256 = "6c57a714a892efb4e9e1fef63d608a3391f30ec984031c465404e4ee381a0755"

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

# Version from Directory.Build.props
[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw "No <Version> in Directory.Build.props" }
Write-Host "Building Hometown Store Access $version"

$extra = @()
if ($GameDir) { $extra += "-p:GameDir=$GameDir" }

if (-not $SkipTests) {
    Invoke-Dotnet (@("test", "tests\HometownStoreAccess.Tests\HometownStoreAccess.Tests.csproj", "-c", "Release", "-nologo") + $extra)
}
Invoke-Dotnet (@("build", "src\HometownStoreAccess\HometownStoreAccess.csproj", "-c", "Release", "-nologo") + $extra)
$bin = Join-Path $root "src\HometownStoreAccess\bin\Release\netstandard2.1"

# BepInEx
$cache = Join-Path $root "artifacts\cache"
New-Item -ItemType Directory -Force $cache | Out-Null
if (-not $BepInExZip) {
    $BepInExZip = Join-Path $cache "BepInEx_win_x64_$BepInExVersion.zip"
    if (-not (Test-Path $BepInExZip)) {
        Write-Host "Downloading $BepInExUrl"
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $BepInExUrl -OutFile $BepInExZip -UseBasicParsing
    }
}
$hash = Get-Sha256 $BepInExZip
if ($hash -ne $BepInExSha256) { throw "BepInEx zip hash mismatch: $hash (expected $BepInExSha256)" }

# Stage the files exactly as they go into the game folder
$stage = Join-Path $root "artifacts\stage"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
Expand-Archive -Path $BepInExZip -DestinationPath $stage

$plugin = Join-Path $stage "BepInEx\plugins\HometownStoreAccess"
$licenses = Join-Path $plugin "licenses"
New-Item -ItemType Directory -Force $plugin, $licenses, (Join-Path $plugin "lang") | Out-Null
foreach ($f in @("HometownStoreAccess.dll", "HometownStoreAccess.Core.dll", "prism.dll")) {
    Copy-Item (Join-Path $bin $f) $plugin
}
[IO.File]::WriteAllText((Join-Path $plugin "version.txt"), $version)
Copy-Item (Join-Path $root "README.md") (Join-Path $plugin "README.txt")
Copy-Item (Join-Path $root "LICENSE") (Join-Path $plugin "LICENSE.txt")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $plugin "THIRD-PARTY-NOTICES.txt")
Copy-Item (Join-Path $root "docs\TRANSLATING.md") (Join-Path $plugin "lang\README.txt")
New-Item -ItemType Directory -Force (Join-Path $plugin "translations") | Out-Null
Get-ChildItem (Join-Path $root "translations") -Filter "*.game.txt" | ForEach-Object { Copy-Item $_.FullName (Join-Path $plugin "translations") }
Copy-Item (Join-Path $root "lib\prism\LICENSE-prism-MPL-2.0.txt") $licenses
Copy-Item (Join-Path $root "lib\prism\third-party\nvdaController\lgpl-2.1.txt") (Join-Path $licenses "BepInEx-and-Doorstop-LGPL-2.1.txt")
Copy-Item -Recurse (Join-Path $root "lib\prism\third-party") (Join-Path $licenses "prism-third-party")

# Zip with forward slashes, in a stable order
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$zipPath = Join-Path $dist "HometownStoreAccess-$version.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Add-Type -AssemblyName System.IO.Compression
$fs = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
$zip = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $stage -Recurse -File -Force | Sort-Object FullName | ForEach-Object {
        $rel = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        $entry = $zip.CreateEntry($rel, [IO.Compression.CompressionLevel]::Optimal)
        $in = [IO.File]::OpenRead($_.FullName)
        $out = $entry.Open()
        try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
    }
}
finally {
    $zip.Dispose()
    $fs.Dispose()
}
Write-Host "Wrote $zipPath"

# Installer with the zip embedded, and this version's release notes for its What's new box
$notes = Join-Path $root "docs\release-notes\$version.md"
if (-not (Test-Path $notes)) { throw "Release notes missing: $notes" }
Invoke-Dotnet (@("build", "src\HometownStoreAccess.Installer\HometownStoreAccess.Installer.csproj", "-c", "Release", "-nologo", "-p:ReleaseZip=$zipPath", "-p:WhatsNew=$notes") + $extra)
$exe = Join-Path $root "src\HometownStoreAccess.Installer\bin\Release\net472\HometownStoreAccessInstaller.exe"
Copy-Item $exe $dist -Force
Write-Host "Wrote $(Join-Path $dist 'HometownStoreAccessInstaller.exe')"

$sums = @(
    "$(Get-Sha256 $zipPath)  HometownStoreAccess-$version.zip",
    "$(Get-Sha256 (Join-Path $dist 'HometownStoreAccessInstaller.exe'))  HometownStoreAccessInstaller.exe"
)
[IO.File]::WriteAllLines((Join-Path $dist "SHA256SUMS.txt"), $sums)
$sums | ForEach-Object { Write-Host $_ }
