# Gera os arquivos de um release em publish:
#   WINbackup-<versão>.exe               versão portátil: um arquivo só, se extrai sozinho ao abrir
#   WINbackup-<versão>-win-x64.zip       pasta com o app, para quem prefere extrair
# Uso: powershell -File scripts\publish.ps1 [-Configuration Teste] [-Out pasta]
# -Configuration Teste gera a mesma coisa sem pedir administrador (para testar sem UAC).
param([string]$Configuration = 'Release', [string]$Out = '')

$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot '..\WINbackup' | Resolve-Path
$project = Join-Path $app 'WINbackup.csproj'
if (-not $Out) { $Out = Join-Path $PSScriptRoot '..\publish' }
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = Resolve-Path $Out

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$name = "WINbackup-$version"
$work = Join-Path ([IO.Path]::GetTempPath()) "winbackup-publish-$PID"

# Pasta (para o .zip)
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:DebugType=none -o "$work\folder\WINbackup" -nologo -v q
if ($LASTEXITCODE) { throw 'dotnet publish (pasta) falhou' }
$zip = Join-Path $Out "$name-win-x64.zip"
Compress-Archive -Path "$work\folder\WINbackup" -DestinationPath $zip -CompressionLevel Optimal -Force

# Um arquivo só (portátil)
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:DebugType=none `
    -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o "$work\single" -nologo -v q
if ($LASTEXITCODE) { throw 'dotnet publish (arquivo único) falhou' }
$exe = Join-Path $Out "$name.exe"
Copy-Item "$work\single\WINbackup.exe" $exe -Force

Remove-Item -Recurse -Force $work
foreach ($f in $exe, $zip) {
    '{0}  {1:N1} MB  SHA-256 {2}' -f (Split-Path $f -Leaf), ((Get-Item $f).Length / 1MB), (Get-FileHash $f -Algorithm SHA256).Hash
}
