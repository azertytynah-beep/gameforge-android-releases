param([string]$Build=(Join-Path (Split-Path $PSScriptRoot) '.local\025\build'), [switch]$Portable)
$ErrorActionPreference='Stop'
# Utilise les outils Windows Installer et makecab présents sur Windows.
$dir=[IO.Path]::GetFullPath($Build)
$files=[ordered]@{StudioExe='GameForge AI.exe';JsonDll='Newtonsoft.Json.dll';TemplateZip='modele.zip';Catalogue='catalogue.txt';Licence='Licences.txt'}
foreach($name in $files.Values){if(!(Test-Path -LiteralPath (Join-Path $dir $name) -PathType Leaf)){throw "Fichier manquant : $name"}}
if($Portable){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $output=Join-Path $dir 'GameForge-AI-Studio-Windows.zip'
    $zip=[IO.Compression.ZipFile]::Open($output,[IO.Compression.ZipArchiveMode]::Create)
    try{
        foreach($name in $files.Values){
            $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $dir $name),"GameForge AI/$name")
        }
        $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,"$PSScriptRoot/Portable.txt",'GameForge AI/Lisez-moi.txt')
    }finally{$zip.Dispose()}
    $hash=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$output.sha256","$hash  $([IO.Path]::GetFileName($output))`n")
    Write-Output $output
    return
}
$work=Join-Path (Split-Path $dir) ('msi-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item $work -ItemType Directory
$ddf=@('.OPTION EXPLICIT','.Set CabinetNameTemplate=studio.cab','.Set Cabinet=on','.Set Compress=on','.Set CompressionType=MSZIP','.Set DiskDirectoryTemplate=.')
foreach($f in $files.GetEnumerator()){ $ddf+=('"'+$dir+'\'+$f.Value+'" '+$f.Key) }
# Le fichier de directives de makecab utilise la page de codes locale.
[IO.File]::WriteAllLines("$work/studio.ddf",$ddf,[Text.Encoding]::Default)
Push-Location $work
try{
    $null=& "$env:windir/System32/makecab.exe" /F studio.ddf
    if($LASTEXITCODE -or !(Test-Path studio.cab)){throw 'Échec de la création du cabinet.'}
    $wi=New-Object -ComObject WindowsInstaller.Installer
    $msi="$work/GameForge-AI-Studio.msi"
    $db=$wi.OpenDatabase($msi,3)
    function SQL([string]$query){$v=$db.OpenView($query.Replace('~',[char]96));try{$v.Execute()}finally{$v.Close()}}
    function Table($name,$columns,$key){SQL ("CREATE TABLE ~$name~ ("+$columns+" PRIMARY KEY ~$key~)")}
    function Row($name,$columns,[object[]]$values){
        $literals=@($values | ForEach-Object { if($_ -is [int]){[string]$_}else{"'"+($_ -replace "'","''")+"'"}})
        $cols=@($columns.Split(',') | ForEach-Object {"~$_~"})
        SQL ("INSERT INTO ~$name~ ("+($cols -join ',')+") VALUES ("+($literals -join ',')+")")
    }
    Table Property '~Property~ CHAR(72) NOT NULL, ~Value~ CHAR(0) NOT NULL LOCALIZABLE' Property
    foreach($p in ([ordered]@{ProductName='GameForge AI Studio';ProductVersion='0.2.0';ProductLanguage='1036';Manufacturer='GameForge AI';ProductCode='{F3DE67BF-77FD-4512-B780-C919FA70D761}';UpgradeCode='{1C63AE28-2E9C-480F-B044-4CCA4B0DA449}';ARPNOMODIFY='1'}).GetEnumerator()){Row Property 'Property,Value' @($p.Key,$p.Value)}
    Table Directory '~Directory~ CHAR(72) NOT NULL, ~Directory_Parent~ CHAR(72), ~DefaultDir~ CHAR(255) NOT NULL LOCALIZABLE' Directory
    Row Directory 'Directory,DefaultDir' @('TARGETDIR','SourceDir')
    Row Directory 'Directory,Directory_Parent,DefaultDir' @('LocalAppDataFolder','TARGETDIR','.')
    Row Directory 'Directory,Directory_Parent,DefaultDir' @('UserPrograms','LocalAppDataFolder','Programs')
    Row Directory 'Directory,Directory_Parent,DefaultDir' @('APPDIR','UserPrograms','GFStudio|GameForge AI Studio')
    Row Directory 'Directory,Directory_Parent,DefaultDir' @('ProgramMenuFolder','TARGETDIR','.')
    Table Component '~Component~ CHAR(72) NOT NULL, ~ComponentId~ CHAR(38), ~Directory_~ CHAR(72) NOT NULL, ~Attributes~ SHORT NOT NULL, ~Condition~ CHAR(255), ~KeyPath~ CHAR(72)' Component
    Row Component 'Component,ComponentId,Directory_,Attributes,KeyPath' @('Studio','{B994E932-2351-48CE-8ADF-16B306998D77}','APPDIR',0,'StudioExe')
    Table Feature '~Feature~ CHAR(38) NOT NULL, ~Feature_Parent~ CHAR(38), ~Title~ CHAR(64) LOCALIZABLE, ~Description~ CHAR(255) LOCALIZABLE, ~Display~ SHORT, ~Level~ SHORT NOT NULL, ~Directory_~ CHAR(72), ~Attributes~ SHORT NOT NULL' Feature
    Row Feature 'Feature,Title,Level,Attributes' @('Studio','GameForge AI',1,0)
    SQL 'CREATE TABLE ~FeatureComponents~ (~Feature_~ CHAR(38) NOT NULL, ~Component_~ CHAR(72) NOT NULL PRIMARY KEY ~Feature_~,~Component_~)'
    Row FeatureComponents 'Feature_,Component_' @('Studio','Studio')
    Table File '~File~ CHAR(72) NOT NULL, ~Component_~ CHAR(72) NOT NULL, ~FileName~ CHAR(255) NOT NULL LOCALIZABLE, ~FileSize~ LONG NOT NULL, ~Version~ CHAR(72), ~Language~ CHAR(20), ~Attributes~ SHORT, ~Sequence~ SHORT NOT NULL' File
    $i=0
    foreach($f in $files.GetEnumerator()){
        $i++;$filename='F'+$i+'.bin|'+$f.Value
        Row File 'File,Component_,FileName,FileSize,Attributes,Sequence' @($f.Key,'Studio',$filename,[int](Get-Item "$dir/$($f.Value)").Length,16384,$i)
    }
    Table Media '~DiskId~ SHORT NOT NULL, ~LastSequence~ SHORT NOT NULL, ~DiskPrompt~ CHAR(64) LOCALIZABLE, ~Cabinet~ CHAR(255), ~VolumeLabel~ CHAR(32), ~Source~ CHAR(72)' DiskId
    Row Media 'DiskId,LastSequence,Cabinet' @(1,$i,'#studio.cab')
    $stream=$db.OpenView('INSERT INTO _Streams (Name,Data) VALUES (?,?)')
    $rec=$wi.CreateRecord(2);$rec.StringData(1)='studio.cab';$rec.SetStream(2,"$work/studio.cab");$stream.Execute($rec);$stream.Close()
    Table Shortcut '~Shortcut~ CHAR(72) NOT NULL, ~Directory_~ CHAR(72) NOT NULL, ~Name~ CHAR(128) NOT NULL LOCALIZABLE, ~Component_~ CHAR(72) NOT NULL, ~Target~ CHAR(72) NOT NULL, ~Arguments~ CHAR(255), ~Description~ CHAR(255) LOCALIZABLE, ~Hotkey~ SHORT, ~Icon_~ CHAR(72), ~IconIndex~ SHORT, ~ShowCmd~ SHORT, ~WkDir~ CHAR(72)' Shortcut
    Row Shortcut 'Shortcut,Directory_,Name,Component_,Target,WkDir' @('StudioLink','ProgramMenuFolder','GFStudio|GameForge AI Studio','Studio','[#StudioExe]','APPDIR')
    foreach($tableName in @('InstallExecuteSequence','AdminExecuteSequence')){
        Table $tableName '~Action~ CHAR(72) NOT NULL, ~Condition~ CHAR(255), ~Sequence~ SHORT' Action
        $seq=if($tableName -eq 'InstallExecuteSequence'){ 'CostInitialize','FileCost','CostFinalize','InstallValidate','InstallInitialize','ProcessComponents','UnpublishFeatures','RemoveShortcuts','RemoveFiles','InstallFiles','CreateShortcuts','RegisterUser','RegisterProduct','PublishFeatures','PublishProduct','InstallFinalize' }else{'CostInitialize','FileCost','CostFinalize','InstallValidate','InstallInitialize','InstallAdminPackage','InstallFiles','InstallFinalize'}
        $order=0;foreach($action in $seq){$order+=100;Row $tableName 'Action,Sequence' @($action,$order)}
    }
    $summary=$db.SummaryInformation(20)
    $summary.Property(2)='Installation Database';$summary.Property(3)='GameForge AI Studio';$summary.Property(4)='GameForge AI'
    $summary.Property(7)='Intel;1036';$summary.Property(9)='{'+[Guid]::NewGuid().ToString().ToUpper()+'}'
    $summary.Property(14)=200;$summary.Property(15)=10;$summary.Persist();$db.Commit()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($db)
    Copy-Item $msi "$dir/GameForge-AI-Studio.msi" -Force
}finally{Pop-Location}
