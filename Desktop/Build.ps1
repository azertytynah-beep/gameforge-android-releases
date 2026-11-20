param([string]$UnityData="$env:ProgramFiles\Unity\Hub\Editor\6000.3.28f1\Editor\Data",
      [string]$Out, [switch]$Verify)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if (!$Out) { $Out=Join-Path $root '.local\025\build' }
$Out=[IO.Path]::GetFullPath($Out)
$null=New-Item $Out -ItemType Directory -Force
$up=Join-Path $root 'UnityProject'
$forge=Join-Path $up 'Assets\GameForge'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$package=Join-Path (Split-Path $Out) 'Newtonsoft.Json.13.0.3.zip'
if(!(Test-Path $package)){
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg' -OutFile $package
}
if((Get-FileHash $package).Hash -ne '872FC189E638AB1056555B03AAA38F68BCB54286E221AA646EB1129BABF63C77'){throw 'Paquet JSON inattendu.'}
$archive=[IO.Compression.ZipFile]::OpenRead($package)
try{
    [IO.Compression.ZipFileExtensions]::ExtractToFile($archive.GetEntry('lib/net45/Newtonsoft.Json.dll'),"$Out/Newtonsoft.Json.dll",$true)
    [IO.Compression.ZipFileExtensions]::ExtractToFile($archive.GetEntry('LICENSE.md'),"$Out/Licences.txt",$true)
}finally{$archive.Dispose()}
$net="$env:windir/Microsoft.NET/Framework64/v4.0.30319"
$refs=@('mscorlib','System','System.Core','System.Xml','System.Net.Http','System.IO.Compression','System.IO.Compression.FileSystem','netstandard') | ForEach-Object {"$net/$_.dll"}
$refs+=@('PresentationFramework','PresentationCore','WindowsBase','System.Xaml') | ForEach-Object { if(Test-Path "$net/WPF/$_.dll"){"$net/WPF/$_.dll"}else{"$net/$_.dll"} }
$refs+="$Out/Newtonsoft.Json.dll"
$projectSources=@('ForgeProject','ForgeProjectStore','ForgeProjectService','ForgeProjectService.OmniRoute','ForgeBrief','ForgeBriefDesign','ForgeOmniRoute','ForgeLocalIdeas') | ForEach-Object {"$forge/Editor/Projects/$_.cs"}
$projectSources+=@('RunnerConfigV1','RunnerContracts','RunnerConfigValidator') | ForEach-Object {"$forge/Runtime/Contracts/$_.cs"}
$src=@(Get-ChildItem "$PSScriptRoot/*.cs" | Where-Object Name -ne 'Verification.cs' | ForEach-Object FullName)+$projectSources
$argsCommon=@('/nologo','/nostdlib+','/optimize+','/utf8output','/langversion:latest','/define:GAMEFORGE_DESKTOP') + @($refs | ForEach-Object {"/r:$_"})
$dotnet="$UnityData/NetCoreRuntime/dotnet.exe"
$csc="$UnityData/DotNetSdkRoslyn/csc.dll"
& $dotnet $csc @argsCommon '/target:winexe' "/win32icon:$PSScriptRoot/Studio.ico" "/out:$Out/GameForge AI.exe" @src
if ($LASTEXITCODE) {throw 'Compilation du Studio interrompue.'}
$catalog=Select-String -Path "$forge/Content/Catalog/RunnerCatalog.asset" -Pattern '^  - id: ((?:theme|obstacle)-[a-z0-9-]+)$'
$catalog | ForEach-Object {$_.Matches[0].Groups[1].Value} | Set-Content "$Out/catalogue.txt" -Encoding ascii
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$files=@(git -C $root ls-files -- UnityProject)
$files+=@('UnityProject/Assets/GameForge/Editor/Generation/DesktopGeneration.cs','UnityProject/Assets/GameForge/Editor/Generation/DesktopGeneration.cs.meta')
$fs=[IO.File]::Open("$Out/modele.zip",[IO.FileMode]::Create)
$zip=[IO.Compression.ZipArchive]::new($fs,[IO.Compression.ZipArchiveMode]::Create)
try {foreach($file in $files | Select-Object -Unique){
    $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,"$root/$file",$file.Substring('UnityProject/'.Length))
}}finally{$zip.Dispose();$fs.Dispose()}
if($Verify){
    & $dotnet $csc @argsCommon '/target:exe' '/main:GameForge.Desktop.StudioWindow.Verification' "/out:$Out/Verifier.exe" @src "$PSScriptRoot/Verification.cs"
    if($LASTEXITCODE){throw 'Compilation des vérifications interrompue.'}
}
Write-Output $Out
