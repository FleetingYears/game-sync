# 开发工具

## 将 main 同步到两个 dev 分支

在仓库根目录执行，或双击 tools/SyncMain/sync-main.cmd：

```powershell
.\tools\SyncMain\sync-main.cmd
```

默认先 fetch origin，依次将本地 main 的同一个提交合并到 dev_state_sync 和 dev_frame_sync，最后原子推送两个 dev 分支到 origin，并恢复原分支。远程有新提交时，先快进本地同名 dev；若本地与远程分叉则停止。不会自动拉取或推送 main，运行前请确认本地 main 包含要同步的公共提交。

只更新本地、不访问远程：

```powershell
.\tools\SyncMain\sync-main.cmd -NoPush
```

要求已安装 Git、工作区干净、处于具名分支，且 main 与两个 dev 分支均存在。默认推送还要求 origin 中存在两个同名 dev 分支，并已配置推送权限。不要在 Unity 正使用该工程时切换分支。

合并冲突会自动中止当前合并并恢复原分支；已成功合并的其他分支保留。合并失败时不会开始推送。推送失败时保留本地合并结果，可处理认证或远程历史后重试。原子推送不受远程支持时会失败，不降级为分别推送。

工具只执行 main → 两个 dev 的合并，不在两个 dev 之间合并，不使用 rebase、cherry-pick 或强制推送。生成的合并提交信息使用中文。
