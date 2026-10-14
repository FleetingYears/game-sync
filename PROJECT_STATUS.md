# 项目进度

最后更新：2026-10-14。长期开发约定见 AGENTS.md。

## 已完成

- 建立统一 Git 仓库，客户端 client/ 与服务端 server/ 共用仓库。
- 创建公开 GitHub 仓库：https://github.com/FleetingYears/game-sync，主分支为 main。
- 已从 main 创建 dev_state_sync 和 dev_frame_sync，本地分支均跟踪对应 GitHub 分支；分支开发与合并规则见 AGENTS.md。
- 配置 Unity/.NET 忽略规则，保留源码、必要资源和 .meta，排除缓存与编译产物。
- 建立 Unity 工程，当前版本为 2022.3.62f1c1；场景目前为默认 SampleScene。
- 建立 C# 控制台服务端，目前仅输出 Hello, World!。
- 完成计划第 1 步：安装 .NET SDK 10.0.402，将服务端升级到 net10.0；构建与运行验证通过。
- 添加根目录 Rider 运行配置 .run/Server.run.xml（启动服务端），通过 scripts/run-server.cmd 调用 dotnet run；适用于 Windows，需安装 .NET 10 SDK 和启用 Rider Shell Script 插件。脚本实际运行输出 Hello, World!、退出码 0；配置 XML 可解析，Rider 内识别和点击运行尚未验证。
- 导入 Tiny Dungeon 的 132 张独立静态图片及 CC0 授权，移除重复图集、示例地图和预览说明。
- 添加编辑器导入脚本：Sprite/Single、16 PPU、Point、关闭 Mip Maps、无压缩；Unity 已生成资源 .meta 文件。
- 添加项目级 AGENTS.md，明确帧同步必须使用确定性数学和模拟逻辑。
- 添加 tools/SyncMain 同步工具：将本地 main 固定提交分别合并到两个 dev，默认 fetch 后原子推送 origin；支持 -NoPush，恢复原分支，拒绝脏工作区与远程分叉，冲突时中止当前合并。2026-10-14 使用 Windows PowerShell 5.1 在临时仓库验证正常/重复同步、分叉合并与方案隔离、脏工作区拒绝、冲突恢复和本地裸仓库原子推送，均通过；未对本项目两个 dev 执行同步或推送。

## 当前状态与限制

- server/server.csproj 的目标框架为 net10.0；本机 SDK 为 10.0.402，Microsoft.NETCore.App 运行时为 10.0.13，原 SDK 9.0.300 仍保留。
- 本机 Rider 为 2026.2.3.1；已核对官方 .NET 10 支持说明，尚未在 Rider 内实际构建或调试。
- 尚未实现网络连接、消息协议、房间管理、固定 Tick、移动战斗、状态同步、帧同步、预测、校正或回滚。
- 尚未建立共享协议/模拟类库或确定性数学库。
- 已明确公共、状态同步与帧同步三条功能分支流；Shared/StateSync/FrameSync 的代码目录和程序集隔离尚未实施，具体约定见 AGENTS.md。
- 尚无数学、协议或模拟自动化测试；同步工具已在临时 Git 仓库执行集成场景验证，未验证联网游戏运行和跨平台确定性。
- 素材与导入设置已发布到远程 main，文件树与本地一致；提交为 e52f56a。项目说明与子计划文档随独立提交维护。

## 后续计划

1. 已完成：核对 SDK 并升级服务端目标框架，构建与运行确认（验证记录见子计划）。
2. 搭建 2D 双人俯视角 Demo 的本地原型，先实现移动，再加入子弹、生命值、死亡重生；此步骤不代表已联网。
3. 建立客户端/服务端共享的数据结构、协议和独立模拟层。
4. 实现整数/定点数数学，明确精度、舍入、溢出规则及所需数学函数；先验证边界和可重复性，并让帧同步模拟使用该数学实现。
5. 实现状态同步：固定 Tick、输入上传、权威状态广播、插值，随后加入预测和校正。
6. 实现帧同步：输入组帧、确定性模拟、录制回放、按帧状态哈希；随后按需要加入预测和回滚。
7. 模拟延迟、抖动和丢包，对比两种同步方案并验证跨平台一致性。

## 子计划状态

子计划文件记录目标、拆解、进度和验收；总文件保留整体状态。

- 1. [服务端框架升级](docs/status/01-server-framework/STATUS.md)
- 2. [2D 双人 Demo 本地原型](docs/status/02-local-demo/STATUS.md)
- 3. [共享协议与独立模拟层](docs/status/03-shared-simulation/STATUS.md)
- 4. [确定性数学与模拟基础](docs/status/04-deterministic-math/STATUS.md)
- 5. [状态同步](docs/status/05-state-sync/STATUS.md)
- 6. [帧同步与回滚](docs/status/06-lockstep/STATUS.md)
- 7. [网络扰动验证与方案对比](docs/status/07-network-validation/STATUS.md)

## 维护方式

- 完成一项功能或修改技术版本后更新此文件，只记录已经发生的事实。
- 验证结果应注明执行方式及结果，未执行的检查不能记为通过。
- 计划可以随学习进展调整；不要把计划写成已实现能力。