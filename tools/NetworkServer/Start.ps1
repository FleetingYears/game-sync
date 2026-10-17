param(
    [int]$Port
)

$ErrorActionPreference = 'Stop'
$minimumUdpPort = 1
$maximumUdpPort = 65535
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $repositoryRoot 'server/server.csproj'

try {
    $serverRunArguments = @('run', '--project', $projectPath, '--', '--network-server')
    if ($PSBoundParameters.ContainsKey('Port')) {
        if ($Port -lt $minimumUdpPort -or $Port -gt $maximumUdpPort) {
            throw '端口必须为 1～65535。'
        }
        $serverRunArguments += $Port.ToString()
        Write-Host "正在使用命令行端口启动网络服务端：UDP $Port"
    }
    else {
        Write-Host '正在使用 Config/NetworkSettings.json 中的端口启动网络服务端。'
    }
    Write-Host '运行中按 Ctrl+C 停止。'
    & dotnet @serverRunArguments
    if ($LASTEXITCODE -ne 0) {
        throw "服务端退出，代码 $LASTEXITCODE。"
    }
}
catch {
    [Console]::Error.WriteLine("网络服务端启动或运行失败：$_")
    exit 1
}
