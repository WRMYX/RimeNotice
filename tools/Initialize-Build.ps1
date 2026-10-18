#Requires -Version 7.2
$ErrorActionPreference='Stop'
$rnRoot=Split-Path $PSScriptRoot -Parent
$rnLocal=Join-Path $rnRoot '.tasks/dotnet-10.0.402'
if(Test-Path (Join-Path $rnLocal 'sdk/10.0.402/Sdks')){
 $env:DOTNET_ROOT=$rnLocal;$env:DOTNET_HOST_PATH=Join-Path $rnLocal 'dotnet.exe';$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$rnLocal;$env:MSBuildSDKsPath=Join-Path $rnLocal 'sdk/10.0.402/Sdks';$env:PATH=$rnLocal+';'+$env:PATH
}
$rnInterop=Join-Path $rnRoot '.tasks/NativeUIAutomation.dll'
if(!(Test-Path $rnInterop)){
 New-Item -ItemType Directory (Split-Path $rnInterop -Parent) -Force | Out-Null
 $rnTlb=Get-ChildItem 'C:/Program Files (x86)/Microsoft SDKs/Windows' -Filter TlbImp.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
 if(!$rnTlb){throw 'Windows SDK .NET tools (TlbImp) required.'}
 & $rnTlb (Join-Path $env:WINDIR 'System32/UIAutomationCore.dll') "/out:$rnInterop" /namespace:NativeUIAutomation /silent
 if($LASTEXITCODE -ne 0){throw 'UI Automation interop generation failed.'}
}
