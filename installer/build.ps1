# Build a self-contained WhalePet release and its Inno Setup installer.
# Run from anywhere:  pwsh -File installer\build.ps1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'desktop\WhalePet\WhalePet.csproj'
$publish = Join-Path $root 'dist\installer\publish'
$output = Join-Path $root 'dist\installer'
$iss = Join-Path $PSScriptRoot 'WhalePet.iss'
$version = '1.1.0'

$isccCandidates = @(
    $env:INNO_SETUP_ISCC,
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 7\ISCC.exe'
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    throw '未找到 ISCC.exe。请安装 Inno Setup 6.3 或更高版本，或设置 INNO_SETUP_ISCC。'
}

# Only recreate the publish directory beneath dist\installer.
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $root 'dist\installer'))
$fullPublish = [IO.Path]::GetFullPath($publish)
if (-not $fullPublish.StartsWith($expectedRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "拒绝清理非构建目录：$fullPublish"
}
Remove-Item -LiteralPath $publish -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publish -Force | Out-Null
New-Item -ItemType Directory -Path $output -Force | Out-Null

Write-Host '==> dotnet publish (self-contained, single file, win-x64)'
& dotnet publish $project -c Release -r win-x64 --self-contained true `
    -o $publish `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败：exit $LASTEXITCODE" }

$required = @(
    (Join-Path $publish 'WhalePet.exe'),
    (Join-Path $publish 'Live2DWeb\index.html'),
    (Join-Path $publish 'Live2DWeb\app.js'),
    (Join-Path $publish 'Live2DWeb\models\seethrough_output\seethrough_output.model3.json'),
    (Join-Path $publish 'Live2DWeb\models\seethrough_output\seethrough_output.4096\texture_00.png')
)
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "发布输出缺少文件：$file"
    }
}

Write-Host '==> ISCC'
& $iscc "/DAppVersion=$version" $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup 编译失败：exit $LASTEXITCODE" }

$setup = Join-Path $output "WhalePet-Setup-$version-x64.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw "未生成安装包：$setup" }
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
$item = Get-Item -LiteralPath $setup
Write-Host ''
Write-Host "Setup: $($item.FullName)"
Write-Host "Size:  $($item.Length) bytes"
Write-Host "SHA256: $hash"
