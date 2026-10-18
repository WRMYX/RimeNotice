#Requires -Version 7.2
param([string]$OutputDirectory = '', [string]$Version = '', [string]$PublishedDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Version) {
    [xml]$manifest = Get-Content (Join-Path $projectRoot 'RimeNotice/Package.appxmanifest') -Raw
    $Version = [string]$manifest.Package.Identity.Version
}
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$') { throw 'Version must have four numeric components.' }
$parsedVersion = [version]$Version
if ($parsedVersion.Major -lt 1 -or $parsedVersion.Major -gt 65535 -or $parsedVersion.Minor -gt 65535 -or $parsedVersion.Build -gt 65535 -or $parsedVersion.Revision -gt 65535) { throw 'Invalid package version.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $projectRoot ('.tasks\portable-clean-' + [Guid]::NewGuid().ToString('N'))
$bundle = Join-Path $stage 'RimeNotice'
$runtime = $bundle
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
# WinUI XAML targets concatenate filenames with OutputPath; the separator is required.
$build = (Join-Path $stage 'build') + [IO.Path]::DirectorySeparatorChar
if ($PublishedDirectory) {
 $PublishedDirectory=[IO.Path]::GetFullPath($PublishedDirectory)
 foreach($item in Get-ChildItem -LiteralPath $PublishedDirectory){Copy-Item -LiteralPath $item.FullName -Destination $runtime -Recurse -Force}
} else {
 & (Join-Path $PSScriptRoot 'Build-Payload.ps1') -OutputDirectory $runtime -Version $Version | Out-Host
}
# The main project includes the optional Office integration payload in every channel.
# WinUI MUI folders are native resources, so managed satellite filtering alone is insufficient.
$keepLanguages = @('en-US', 'zh-CN', 'zh-Hans', 'zh')
foreach ($directory in Get-ChildItem -LiteralPath $runtime -Directory) {
    if (-not (Get-ChildItem -LiteralPath $directory.FullName -File | Where-Object { $_.Name -like '*.mui' -or $_.Name -like '*.resources.dll' })) { continue }
    try { $culture = [Globalization.CultureInfo]::GetCultureInfo($directory.Name) } catch { continue }
    if (-not $culture.Name -or $directory.Name -in $keepLanguages) { continue }
    $resolved = [IO.Path]::GetFullPath($directory.FullName)
    if (-not $resolved.StartsWith($runtime + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
foreach ($file in Get-ChildItem -LiteralPath $runtime -File -Recurse | Where-Object { $_.Extension -eq '.pdb' -or $_.Name -eq '使用说明.txt' }) {
    Remove-Item -LiteralPath $file.FullName -Force
}
foreach ($required in @('Updater/RimeNotice.Updater.exe', 'Updater/RimeNotice.Updater.pri', 'Updater/Microsoft.UI.Xaml.dll', 'Updater/coreclr.dll', 'RimeNotice.exe','coreclr.dll','Microsoft.UI.Xaml.dll','Assets\RimeNotice-Logo.svg')) {
    if (-not (Test-Path -LiteralPath (Join-Path $runtime $required))) { throw "Missing runtime file: $required" }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zip = Join-Path $OutputDirectory ('RimeNotice-' + $Version + '-Portable-x64.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($bundle, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
[pscustomobject]@{ Archive = $zip; Stage = $bundle; SizeMB = [Math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1) }
