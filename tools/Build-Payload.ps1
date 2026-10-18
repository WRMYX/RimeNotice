#Requires -Version 7.2
param([Parameter(Mandatory)][string]$OutputDirectory,[string]$Version='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-Build.ps1')
if(!$Version){[xml]$m=Get-Content (Join-Path $rnRoot 'RimeNotice/Package.appxmanifest') -Raw;$Version=[string]$m.Package.Identity.Version}
if($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$'){throw 'Use a four component package version.'}
$v=[version]$Version;if($v.Major -lt 1 -or @($v.Major,$v.Minor,$v.Build,$v.Revision).Where({$_ -gt 65535}).Count){throw 'Invalid MSIX version.'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory $OutputDirectory -Force | Out-Null
$vs=& "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -property installationPath
$msbuild=Join-Path $vs 'MSBuild/Current/Bin/MSBuild.exe'
$hostArgs=@()
if(!(Test-Path $msbuild) -or (Get-Item $msbuild).VersionInfo.FileMajorPart -lt 18 -or $env:RN_BUILD_HOST -eq 'dotnet'){$msbuild=(Get-Command dotnet).Source;$hostArgs=@('msbuild')}
# Fresh output directories avoid carrying stale XBF files between release builds.
$build=Join-Path $rnRoot ('.tasks/build-'+[guid]::NewGuid().ToString('N'))
foreach($item in @(@{Project='RimeNotice/RimeNotice.csproj';Destination=$OutputDirectory;Name='main'},@{Project='tools/RimeNotice.Updater/RimeNotice.Updater.csproj';Destination=(Join-Path $OutputDirectory 'Updater');Name='updater'})){
 & $msbuild @hostArgs (Join-Path $rnRoot $item.Project) /restore /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 "/p:Version=$Version" "/p:OutputPath=$build/$($item.Name)/" /p:WindowsPackageType=None /p:GenerateAppxPackageOnBuild=false /p:AppxPackageSigningEnabled=false /p:WindowsAppSDKSelfContained=true /p:SelfContained=true /p:PublishTrimmed=false /p:PublishReadyToRun=false "/p:PublishDir=$($item.Destination)/" /p:UseSharedCompilation=false /p:BuildInParallel=false /m:1 /v:minimal /nologo
 if($LASTEXITCODE -ne 0){throw "Publish failed: $($item.Project)"}
}
foreach($file in @('RimeNotice.exe','RimeNotice.dll','RimeNotice.pri','coreclr.dll','Microsoft.UI.Xaml.dll','Updater/RimeNotice.Updater.exe','Updater/RimeNotice.Updater.pri','Updater/coreclr.dll','Assets/icon.ico')){if(!(Test-Path (Join-Path $OutputDirectory $file))){throw "Missing payload: $file"}}
[pscustomobject]@{Payload=$OutputDirectory;Version=$Version}
