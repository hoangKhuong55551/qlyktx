# Build ban Release va dong goi file zip de cap nhat len hosting.
# File zip KHONG chua Web.config -> giai nen de len hosting khong lam mat connection string.
# Cach chay: chuot phai file nay -> "Run with PowerShell"
#        hoac: powershell -ExecutionPolicy Bypass -File .\dong-goi-hosting.ps1

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$project = Join-Path $root 'Qlykytucxa\Qlykytucxa.csproj'
$publish = Join-Path $root 'publish'
$zip     = Join-Path $root 'web-capnhat.zip'

# Tim MSBuild cua Visual Studio
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = $null
if (Test-Path $vswhere) {
    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
        -find 'MSBuild\**\Bin\MSBuild.exe' | Where-Object { $_ -notmatch 'Management Studio' } | Select-Object -First 1
    if (-not $msbuild) {
        $msbuild = & $vswhere -all -products * -find 'MSBuild\**\Bin\MSBuild.exe' |
            Where-Object { $_ -notmatch 'Management Studio' } | Select-Object -First 1
    }
}
if (-not $msbuild) { throw 'Khong tim thay MSBuild cua Visual Studio.' }

Write-Host "1/3 Build Release + publish..." -ForegroundColor Cyan
& $msbuild $project -p:Configuration=Release -p:DeployOnBuild=true -p:PublishProfile=Hosting `
    "-p:SolutionDir=$root\" -v:minimal -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build that bai - xem loi o tren.' }

Write-Host "2/3 Dong goi (bo Web.config)..." -ForegroundColor Cyan
$staging = Join-Path $env:TEMP ('ktx-zip-' + [guid]::NewGuid())
Copy-Item $publish $staging -Recurse
Remove-Item (Join-Path $staging 'Web.config') -Force
if (Test-Path $zip) { Remove-Item $zip -Force }
# Tu ghi tung muc voi duong dan dang a/b/c. Compress-Archive / ZipFile cua PowerShell 5.1 ghi a\b\c
# -> trinh giai nen tren hosting coi ca chuoi la ten 1 file, khong tao dung thu muc.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $staging -Recurse -File | ForEach-Object {
        $name = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally {
    $archive.Dispose()
}
Remove-Item $staging -Recurse -Force

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "3/3 Xong: $zip ($size MB)" -ForegroundColor Green
Write-Host "Upload file nay vao thu muc /qlykytucxa tren hosting roi giai nen (ghi de)."
