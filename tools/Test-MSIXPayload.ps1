#Requires -Version 7.2
[CmdletBinding(DefaultParameterSetName='Directory')]
param(
 [Parameter(Mandatory,ParameterSetName='Directory')][string]$PackageDirectory,
 [Parameter(Mandatory,ParameterSetName='Package')][string]$PackagePath,
 [string]$ExpectedName='', [string]$ExpectedPublisher='', [string]$ExpectedVersion='',
 [switch]$AllowLegacyResourceIndex
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$audit=Join-Path $root ('.tasks/payload-validation-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $audit -Force | Out-Null
$sdk=Get-ChildItem 'C:/Program Files (x86)/Windows Kits/10/bin' -Directory | Where-Object {$_.Name -match '^10\.0\.' -and (Test-Path (Join-Path $_.FullName 'x64/makepri.exe'))} | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1
if(!$sdk){throw 'Windows SDK MakePRI is required.'}
if($PackagePath){
 $PackagePath=[IO.Path]::GetFullPath($PackagePath)
 $PackageDirectory=Join-Path $audit 'unpacked'
 & (Join-Path $sdk.FullName 'x64/makeappx.exe') unpack /p $PackagePath /d $PackageDirectory > (Join-Path $audit 'unpack.log') 2>&1
 if($LASTEXITCODE -ne 0){throw 'Package unpack failed.'}
}
$PackageDirectory=[IO.Path]::GetFullPath($PackageDirectory)
[xml]$manifest=Get-Content -LiteralPath (Join-Path $PackageDirectory 'AppxManifest.xml') -Raw
$identity=$manifest.Package.Identity
if($ExpectedName -and $identity.Name -ne $ExpectedName){throw 'Package name mismatch.'}
if($ExpectedPublisher -and $identity.Publisher -ne $ExpectedPublisher){throw 'Package publisher mismatch.'}
if($ExpectedVersion -and $identity.Version -ne $ExpectedVersion){throw 'Package version mismatch.'}
if($identity.ProcessorArchitecture -ne 'x64'){throw 'Unexpected package architecture.'}
if($manifest.Package.Applications.Application.Executable -ne 'RimeNotice.exe' -or $manifest.Package.Applications.Application.EntryPoint -ne 'Windows.FullTrustApplication'){throw 'Unexpected application entry point.'}
foreach($name in @('RimeNotice.exe','RimeNotice.dll','RimeNotice.runtimeconfig.json','RimeNotice.deps.json','resources.pri','coreclr.dll','hostfxr.dll','hostpolicy.dll','Microsoft.UI.Xaml.dll','Microsoft.UI.Xaml.Controls.dll','Microsoft.WindowsAppRuntime.dll','Assets/icon.ico','Updater/RimeNotice.Updater.exe','Updater/RimeNotice.Updater.dll','Updater/RimeNotice.Updater.pri','Updater/coreclr.dll')){
 if(!(Test-Path -LiteralPath (Join-Path $PackageDirectory $name) -PathType Leaf)){throw "Missing package file: $name"}
}
# Check the resource identity and essential XAML names, including embedded XBF resources.
$dump=Join-Path $audit 'resources.xml'
& (Join-Path $sdk.FullName 'x64/makepri.exe') dump /if (Join-Path $PackageDirectory 'resources.pri') /of $dump | Out-Null
if($LASTEXITCODE -ne 0){throw 'PRI dump failed.'}
[xml]$pri=Get-Content -LiteralPath $dump -Raw
$maps=@($pri.SelectNodes("//*[local-name()='ResourceMap']") | Where-Object {$_.primary -eq 'true'})
if($maps.Count -ne 1){throw 'Expected one primary PRI resource map.'}
$mapName=[string]$maps[0].name
if(!$AllowLegacyResourceIndex -and $mapName -ne $identity.Name){throw "PRI identity mismatch: '$mapName' versus '$($identity.Name)'."}
foreach($name in @('App.xbf','MainWindow.xbf','DesignResources.xbf')){
 if(!@($maps[0].SelectNodes(".//*[local-name()='NamedResource']") | Where-Object {$_.name -eq $name}).Count){throw "Missing XAML resource: $name"}
}
# Both programs must have all of their declared managed/native runtime assets.
foreach($item in @(@{Dir=$PackageDirectory;Name='RimeNotice'},@{Dir=(Join-Path $PackageDirectory 'Updater');Name='RimeNotice.Updater'})){
 $assemblyVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $item.Dir ($item.Name+'.dll'))).Version.ToString(4)
 if($assemblyVersion -ne $identity.Version){throw "Executable/package version mismatch ($($item.Name)): $assemblyVersion versus $($identity.Version)."}
 $deps=Get-Content -LiteralPath (Join-Path $item.Dir ($item.Name+'.deps.json')) -Raw | ConvertFrom-Json -AsHashtable
 foreach($target in $deps.targets.Values){foreach($library in $target.Values){foreach($kind in @('runtime','native','resources')){
  if(!$library.ContainsKey($kind)){continue}
  foreach($asset in $library[$kind].Keys){
   if($asset.EndsWith('/_._')){continue}
   $path=if($kind -eq 'resources'){$asset}else{[IO.Path]::GetFileName($asset)}
   if(!(Test-Path -LiteralPath (Join-Path $item.Dir $path) -PathType Leaf)){throw "Missing declared dependency ($($item.Name)): $asset"}
  }
 }}}
 # The embedded WinRT activation manifest must not reference missing local DLLs.
 $exeManifest=Join-Path $audit ($item.Name+'.manifest')
 & (Join-Path $sdk.FullName 'x64/mt.exe') "-inputresource:$(Join-Path $item.Dir ($item.Name+'.exe'));#1" "-out:$exeManifest" | Out-Null
 if($LASTEXITCODE -ne 0){throw 'Cannot inspect executable activation manifest.'}
 [xml]$activation=Get-Content -LiteralPath $exeManifest -Raw
 foreach($file in $activation.SelectNodes("//*[local-name()='file']")){
  if(!(Test-Path -LiteralPath (Join-Path $item.Dir $file.name) -PathType Leaf)){throw "Missing activation DLL ($($item.Name)): $($file.name)"}
 }
}
$result=[pscustomobject]@{PackageName=[string]$identity.Name;Publisher=[string]$identity.Publisher;Version=[string]$identity.Version;ResourceIndexName=$mapName;LegacyResourceIndex=[bool]$AllowLegacyResourceIndex;ContentChecks='Passed';RuntimeChecks='Not run';AuditDirectory=$audit}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $audit 'result.json')
$result
