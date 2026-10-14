param(
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([string[]]$GitArguments)
    $result = & git @GitArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git 命令失败：git $($GitArguments -join ' ')"
    }
    return $result
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$originalLocation = Get-Location
$originalBranch = $null
$completedBranches = @()
$mergeStarted = $false

try {
    Set-Location -LiteralPath $repoRoot
    $gitRoot = Invoke-Git @('rev-parse', '--show-toplevel')
    if ([IO.Path]::GetFullPath($gitRoot) -ne [IO.Path]::GetFullPath($repoRoot)) {
        throw '工具必须位于目标仓库的 tools/SyncMain 目录。'
    }
    if (Invoke-Git @('status', '--porcelain', '--untracked-files=all')) {
        throw '工作区存在未提交改动，请先提交或自行保存后再同步。'
    }
    foreach ($operation in @('MERGE_HEAD', 'CHERRY_PICK_HEAD', 'REVERT_HEAD', 'rebase-merge', 'rebase-apply')) {
        $operationPath = Invoke-Git @('rev-parse', '--git-path', $operation)
        if (Test-Path -LiteralPath $operationPath) {
            throw "存在未完成的 Git 操作：$operation，请先处理。"
        }
    }
    $originalBranch = Invoke-Git @('symbolic-ref', '--quiet', '--short', 'HEAD')
    $targets = @('dev_state_sync', 'dev_frame_sync')
    foreach ($branch in @('main') + $targets) {
        Invoke-Git @('show-ref', '--verify', '--quiet', "refs/heads/$branch") | Out-Null
    }

    # 使用固定的 main 提交，保证两个分支同步同一份公共内容。
    $mainCommit = Invoke-Git @('rev-parse', 'refs/heads/main')
    if (-not $NoPush) {
        Invoke-Git @('remote', 'get-url', 'origin') | Out-Null
        Invoke-Git @('fetch', 'origin') | Out-Host
        foreach ($branch in $targets) {
            # 仅允许快进吸收远程同名分支，避免遗漏他人提交。
            Invoke-Git @('show-ref', '--verify', '--quiet', "refs/remotes/origin/$branch") | Out-Null
            & git merge-base --is-ancestor "origin/$branch" $branch
            if ($LASTEXITCODE -eq 1) {
                & git merge-base --is-ancestor $branch "origin/$branch"
                if ($LASTEXITCODE -ne 0) {
                    throw "$branch 与远程已分叉，请先手动处理后重试。"
                }
            } elseif ($LASTEXITCODE -ne 0) {
                throw "无法检查 $branch 的远程历史。"
            }
        }
    }

    foreach ($branch in $targets) {
        Invoke-Git @('switch', $branch) | Out-Host
        if (-not $NoPush) {
            Invoke-Git @('merge', '--ff-only', "origin/$branch") | Out-Host
        }
        $mergeStarted = $true
        Invoke-Git @('merge', '--no-edit', '-m', "将 main 公共更新同步到 $branch", $mainCommit) | Out-Host
        $mergeStarted = $false
        $completedBranches += $branch
        Write-Host "已同步 main ($mainCommit) 到 $branch。"
    }

    if (-not $NoPush) {
        # 原子推送确保远程两个分支同时更新，不使用强制推送。
        Invoke-Git @('push', '--atomic', 'origin', 'dev_state_sync:dev_state_sync', 'dev_frame_sync:dev_frame_sync') | Out-Host
        Write-Host '两个 dev 分支已推送到 origin。'
    }
} catch {
    if ($mergeStarted) {
        $mergeHead = & git rev-parse --git-path MERGE_HEAD
        if (Test-Path -LiteralPath $mergeHead) {
            Invoke-Git @('merge', '--abort') | Out-Host
        }
    }
    Write-Host "同步未完成；已完成本地合并的分支：$($completedBranches -join ', ')。"
    throw
} finally {
    try {
        if ($originalBranch) {
            Invoke-Git @('switch', $originalBranch) | Out-Host
        }
    } finally {
        Set-Location -LiteralPath $originalLocation
    }
}
