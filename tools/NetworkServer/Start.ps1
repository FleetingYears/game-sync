param(
    [ValidateRange(1, 65535)]
    [int]$Port = 9050
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $repositoryRoot 'server/server.csproj'

try {
    Write-Host "正在启动 N1 服务端：UDP $Port"
    Write-Host '运行中按 Ctrl+C 停止。'
    & dotnet run --project $projectPath -- --network-server $Port
    if ($LASTEXITCODE -ne 0) {
        throw "服务端退出，代码 $LASTEXITCODE。"
    }
}
catch {
    [Console]::Error.WriteLine("N1 服务端启动或运行失败：$_")
    exit 1
}
