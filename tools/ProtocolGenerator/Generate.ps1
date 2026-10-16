param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
$version = '3.36.2'
$packagesRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$protoc = Join-Path $packagesRoot "google.protobuf.tools/$version/tools/windows_x64/protoc.exe"
$source = Join-Path $RepositoryRoot 'protocol/Common/NetworkProbe.proto'
$output = Join-Path $RepositoryRoot 'shared/Runtime/Gen/Protocol'
if (-not (Test-Path -LiteralPath $protoc)) {
    throw "缺少 Google.Protobuf.Tools $version，请先执行 dotnet restore shared/shared.csproj。"
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $protoc "--proto_path=$(Join-Path $RepositoryRoot 'protocol/Common')" "--csharp_out=$output" $source
if ($LASTEXITCODE -ne 0) { throw "protoc 生成失败，退出码：$LASTEXITCODE" }
