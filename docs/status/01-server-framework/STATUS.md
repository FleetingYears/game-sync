# 计划 1：服务端框架升级  状态：未开始；最后更新：2026-10-14。  ## 目标  安装或确认 .NET 10 SDK，将控制台服务端升级到 net10.0，并验证可构建、可运行。  ## 当前进度  现有 server.csproj 仍为 net9.0，Program.cs 仅输出 Hello, World!；未验证 .NET 10 SDK。  ## 依赖与边界  无；保留现有工程，不重新创建项目。  ## 任务拆解 
- [ ] 检查 dotnet --list-sdks，确认 SDK 与 Rider 支持情况
- [ ] 将 server.csproj 目标框架改为 net10.0
- [ ] 执行依赖还原与构建，处理实际出现的兼容问题
- [ ] 运行控制台程序并确认退出码和输出
- [ ] 在总进度和本文件记录实际版本与验证结果

## 验收方式

dotnet build server/server.csproj 成功；dotnet run --project server/server.csproj 正常输出并退出。

## 验证记录

暂无本计划的实际验证记录。执行后记录日期、环境、命令/步骤、结果和限制。

## 风险与待确认

实现时根据实际需求补充；未确认的设计或结果不能写成已完成。

## 维护

每完成一部分功能，在提交前核对本文件、根目录 AGENTS.md 和 PROJECT_STATUS.md，更新勾选状态、验证记录与过时描述。
