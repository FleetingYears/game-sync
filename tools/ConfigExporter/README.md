# Excel 二进制导表工具

使用 Python 3.10 或更新版本，仅依赖标准库。工具读取 `.xlsx`、检查四行表头和数据，再生成 `.bytes`、C# 数据行类及引用的枚举；配置类包含纯 C# 二进制加载与查询接口，不依赖 Unity API。

## 使用

在仓库根目录执行：

```powershell
tools/ConfigExporter/export.cmd
tools/ConfigExporter/export.cmd --check
```

也可以直接执行 `python -X utf8 tools/ConfigExporter/export.py`。Windows 入口使用 PATH 中的 `python`，`--check` 只校验，不写输出。

工具默认路径及命令行中的相对路径均以仓库根目录为基准，通过脚本所在位置确定根目录，不依赖启动工作目录，没有写死机器路径。可覆盖 `--source-root`、`--client-output`、`--server-output`、`--client-code`、`--server-code` 和 `--enums`，绝对路径仅在调用方主动提供时使用。

- 源表：`config/Excels/Client`、`config/Excels/Server`、`config/Excels/Shared`。
- Client 表 → `client/Res/Config/<工作表名>.bytes`。
- Server 表 → `server/Res/Config/<工作表名>.bytes`。
- Shared 表 → 两端各一份，内容相同。
- 客户端 C# → `shared/Runtime/Gen/Client/Config/`；服务端 C# → `shared/Runtime/Gen/Server/Config/`，按 Client/Server/Shared 范围分发。
- 枚举定义：`tools/ConfigExporter/enums.json`。此 JSON 只定义工具的枚举，不是导出的配置数据。

目录不存在时按实际导出需要创建。Client 与 Server 可以有同名表，Shared 不得与任何一端同名；同一端不得有重复表名，大小写不同也视为输出文件名冲突。默认枚举 `ArtResourceType` 固定为 `Sprite=1`、`Prefab=2`。

## Excel 约定

每个非 `_` 开头的工作表是一张独立配置表，工作表名和字段名使用英文字母/下划线开头、后接字母/数字/下划线的标识符，禁止 C# 保留关键字。导表忽略 `_说明` 等说明页及 `~$` 开头的 Excel 临时文件。字段从 A 列连续排列，不能有中间空列。

1. 第 1 行：字段名，例如 `id`、`type`、`asset_path`、`remark`。
2. 第 2 行：类型，例如 `string`、`enum:ArtResourceType`、`string`、`string`。
3. 第 3 行：规则，例如 `key`、`required`、`required`、`optional`。
4. 第 4 行：中文说明，可以留空；不写入二进制。
5. 第 5 行起：实际数据；整行空白跳过。

数据页禁止公式、Excel 错误值和合并单元格；不按样式、颜色、隐藏状态决定是否导出。不支持 `.xls`、字典、嵌套对象、多维数组、日期或表达式。

## 类型

- `string`：保留文本原值。数字形式的 ID 也必须存为文本，保留前导零；仅设置显示格式不能恢复已被 Excel 转成数值的 ID。
- `int`：有符号 32 位整数，范围 -2147483648..2147483647。数值单元格必须是完整整数；文本仅接受十进制整数，可带正负号。
- `long`：有符号 64 位整数，范围 -9223372036854775808..9223372036854775807。绝对值达到 10^15 的值必须以文本填写，避免 Excel 15 位十进制精度造成损失。
- `bool`：`true`、`false`、`1`、`0`，或 Excel 原生布尔单元格；不接受任意非零数值或其他文本。
- `float`：IEEE 754 binary32，先按最近值、偶数舍入转换为单精度，溢出报错，极小值可下溢为零。
- `double`：IEEE 754 binary64。浮点类型允许科学计数法，拒绝 NaN 和 Infinity；用于表现配置，不能作为确定性模拟数学方案。
- `enum:枚举名`：填写 `enums.json` 中的成员名称，严格区分大小写；拒绝数值形式和未知名称。成员值为固定 int32，不自动按顺序编号。
- 一维数组：以上类型加 `[]`，例如 `int[]`、`string[]`、`enum:ArtResourceType[]`。单元格必须是 JSON 数组文本，例如 `[1,2,3]`、`["idle","run"]`、`["Sprite","Prefab"]`；逐项按对应标量规则校验，禁止嵌套数组和对象。

枚举定义不得重名或重复数值；修改已有枚举的数值会改变配置语义，应保持已发布数值稳定。

## 字段规则

- `key`：每表必须且只能有一个，仅支持标量 `string` 或 `int`；必填、唯一，字符串主键禁止首尾空白并区分大小写。
- `required`：值不能为空；第 3 行规则留空也按 required 处理。显式空数组 `[]` 是有效值。
- `optional`：空白时字符串为 `""`，数值为 `0`，bool 为 false，数组为空数组。可选标量枚举必须声明数值为 0 的成员。没有 null 或可空类型。

除了上述空值默认规则，不自动裁剪字符串或修改大小写。非空字符串中的空白会保留。数值和枚举不允许首尾空格。

错误信息包含源文件、工作表、单元格、字段及原值；源表无法解析、表级结构错误等不对应单一单元格的异常显示文件/表级原因。

## C# 生成规则

每张表生成一个 sealed 数据行类，例如 `art_resource` → `ArtResourceConfig.cs`，命名空间为 `GameSync.Config`。表名按下划线分段，首字母大写后拼接，再加 Config；多个表生成同名类或文件时拒绝导出。

类带 `[System.Serializable]`，保留 Excel 列顺序，生成 public 大驼峰属性，使用 private set 防止外部修改 Key 或标量配置，例如 id → Id、asset_path → AssetPath。Excel 和二进制字段名保持原样，生成的读取逻辑按列顺序及此命名映射赋值。string 初始化为空字符串，数组初始化为空数组；数值、bool 和枚举使用 C# 默认值。字段中文说明转为 XML 注释，转义特殊字符，并逐行添加注释前缀。代码中的默认值仅用于新建对象，读取时执行结构和值校验。

标量类型与 Excel 对应，数组使用 T[]。每端只生成实际引用的枚举，同一枚举仅一份，固定值来自 enums.json。共享表字段结构和枚举值在两端相同；配置类新增 public const string BytesPath，按对应端输出实际相对路径，例如 client/Res/Config/art_resource.bytes。服务端路径相应为 server/Res/Config，允许覆盖输出参数，但不能跨盘生成不可表达的相对路径。BytesPath、Load、LoadFromFile、Get、TryGet、GetAll、Find、FindAll、IsLoaded、EnsureLoaded、ConfigBinaryReader 为保留成员名，字段转为大驼峰后不得重名。字段与枚举成员均转为 PascalCase，转换后重名会拒绝生成。普通枚举使用简短类型名，仅被同名成员遮蔽时使用 global:: 全局限定。类型/文件名禁止冲突，字段不能与所在类同名，枚举成员不能与枚举类型同名。

生成文件使用 UTF-8、LF，无时间戳或机器路径，使用 C# 7.3 可用语法和 System 基础类型，不依赖 Unity API。不将实际配置行硬编码进 C#；生成类包含加载、字典索引及查询逻辑，共用 shared/Runtime/Config/ConfigBinaryReader.cs 解码器，并生成 ConfigManager.Generated.cs 加载清单；生成文件不手改。

客户端生成到 shared/Runtime/Gen/Client/Config，服务端生成到 shared/Runtime/Gen/Server/Config。生成文件分别由 UNITY_5_3_OR_NEWER 与其反向条件包裹，Unity 只编译客户端配置，普通 .NET 工程只编译服务端配置；同名表及生成清单不会同时参与编译。共享源表分别生成到两端目录，二进制输出位置保持不变。

## 双端统一加载入口

手写入口位于 shared/Runtime/Config/ConfigManager.cs，继承同一工程中的 Singleton<T>，不由导表覆盖；本端 ConfigManager.Generated.cs 实现实例分部方法 LoadGenerated。调用 ConfigManager.Instance.LoadAll()，清单按表名稳定排序。

共享库不引用 Unity API。服务端默认从 AppContext.BaseDirectory 加载，server.csproj 保持 .bytes 在构建/发布目录的 server/Res/Config。客户端 Assets/Editor/EditorResourceLoader.cs 注册根目录获取函数，通过 Application.dataPath 获取当前仓库根目录；业务调用不传路径。当前客户端发布布局尚未实现。服务端入口尚未接入 Program。

多表按顺序加载，失败时停止；失败表保留旧数据，已加载的其他表不回滚。客户端与服务端通过本地源码包和 ProjectReference 引用同一份 shared 工程，详见 shared/README.md。

## 加载与查询

生成配置类同时表示单条记录并提供静态表接口。例如：

```csharp
ConfigManager.Instance.LoadAll();
var mage = ArtResourceConfig.Get("tiny_dungeon_tile_0084");
ArtResourceConfig config;
bool found = ArtResourceConfig.TryGet("tiny_dungeon_tile_0084", out config);
var all = ArtResourceConfig.GetAll();
var first = ArtResourceConfig.Find(c => c.Type == ArtResourceType.Sprite);
var sprites = ArtResourceConfig.FindAll(c => c.Type == ArtResourceType.Sprite);
```

- `LoadFromFile(string projectRoot)`：调用方提供仓库根目录，组合 BytesPath 后读取文件。不存在或读取失败时保留旧配置。
- `Load(byte[] bytes)`：可供 Unity 将 TextAsset/其他加载系统取得的数据传入；不自动复制或导入 Unity 资源。
- `IsLoaded`：是否成功加载过配置；查询前必须显式加载，未加载查询抛 InvalidOperationException。
- `Get(key)`：字典按 Key 查询，平均 O(1)；没有匹配项抛 KeyNotFoundException。
- `TryGet(key, out config)`：平均 O(1)，不存在时返回 false 且 config 为 null。string Key 区分大小写，null Key 按 Dictionary 规则抛 ArgumentNullException。
- `GetAll()`：返回 IReadOnlyList，包含所有记录，保持导出顺序。
- `Find(Predicate<T>)`：O(n)，返回首个匹配项，无匹配项返回 null。
- `FindAll(Predicate<T>)`：O(n)，返回全部匹配项的只读列表。条件不能为 null，条件中抛出的异常由调用方处理。

配置类直接使用 m_Index 字典和 m_All 列表成员，不使用嵌套类或额外数据容器。加载时先构建临时数组、字典和只读列表，全部校验通过后在同一把锁内替换两个成员，失败保留旧数据；按 Key 查询和获取列表也在锁内取一致数据，条件回调在锁外执行。重载后已持有的旧列表/记录引用仍表示旧快照，不会自动更新。公开属性均为 private set，Key 不能被外部修改；数组仍是约定的 T[]，不要修改数组元素，以保持配置只读使用。

读取器校验 magic、版本、来源/表名、字段数/顺序/类型/标志、主键索引、枚举名称及固定值；校验字符串与数组长度、严格 UTF-8、bool 编码、有限浮点值、必填字符串、Key 空白/重复和尾部多余字节。字典的字符串比较使用 StringComparer.Ordinal。当前同步加载设计，不包含异步配置加载、美术资源实例化或热更新调度。

后续按用户要求不新增测试源代码；当前验证使用构建、静态文件检查和实际 Editor 验证。先前新增的测试源文件已移除。

## 二进制协议 GSCF v1

整数、长度、浮点数统一小端。`u32` 为无符号 32 位整数，`i32`/`i64` 为有符号整数；字符串编码为 `u32 UTF-8 字节数 + 字节内容`，没有 BOM 或结尾零字符。主键列索引从 0 开始。

文件依次写入：

1. 4 字节 ASCII `GSCF`。
2. `u16` 版本，当前为 1。
3. 字符串：来源范围 Client/Server/Shared。
4. 字符串：工作表名。
5. `u32` 字段数、`u32` 行数、`u32` 主键列索引。
6. 按 Excel 列顺序写字段描述。
7. 按主键排序写每行的字段值。

每个字段描述依次为：字段名字符串、`u8` 基础类型代码、`u8` 标志、枚举名字符串、`u32` 枚举成员数，再逐个写入成员名称字符串和 `i32` 成员值。非枚举字段的枚举名为空、成员数为 0。成员按名称排序。

基础类型代码：string=1、int=2、long=3、bool=4、float=5、double=6、enum=7。标志 bit0 表示数组，bit1 表示 optional，其余位保留为 0；主键通过文件头的索引指定。

行数据没有单独行长度：string 使用上述长度前缀；int/enum 为 i32，long 为 i64，bool 为单字节 0/1，float/double 分别为 4/8 字节 IEEE 754。数组先写 `u32` 元素数，再逐个写标量值；没有 null 标志。

int 主键按有符号数值升序，string 主键按 UTF-16 序数升序，与后续 .NET 的 Ordinal 排序对应。文件不含时间戳或机器路径；相同配置和枚举重复导出得到相同字节。生成的运行时读取器校验结构、版本与长度，分配前用剩余数据长度验证行数及数组数量。

## 输出保护与当前边界

先校验所有表并生成代码及二进制，再暂存两端四个输出目录，最后替换；任一目录替换遇到文件占用等 I/O 异常时尝试恢复旧目录。异常恢复失败会保留并报告备份路径。系统断电或强制结束进程不属于可自动保证回滚的范围，可从同级 `Config.backup-*` 目录恢复。

输出目录的 `.config-exporter.json` 仅登记工具管理的文件，用于安全替换及清理已删除/重命名源表的旧产物。Unity 为已登记生成脚本创建的对应 .meta 会在导出时保留，已移除脚本的 .meta 一并清理。不要手动往输出目录放其他文件或子目录；发现未登记文件、目录或链接时工具拒绝覆盖。两端输出与源表路径不得相同或互相包含。

本工具只校验通用配置内容，不调用 Unity AssetDatabase，也不检查 Sprite/Prefab 类型与资源路径是否对应实际资产。`art_resource` 保留无后缀路径与字符串 ID；美术资源检查和 Sprite/Prefab 加载另行实现；配置 .bytes 的加载与查询已实现。当前二进制位于 `client/Res/Config`，不在 Unity Assets 内，因此尚不代表已经进入 Unity 构建。

## 已执行验证

2026-10-15 使用 Python 3.12 执行 17 项 unittest，通过标量及数组二进制回读、数值边界、非法值、空值规则、主键排序/重复、Excel 公式/合并拒绝、表名冲突、两端分发、重复导出、旧文件清理、校验失败保护与模拟第二端 I/O 失败回滚。

项目 `art_resource.xlsx` 的 132 条 Sprite 配置已实际导出为客户端 `.bytes` 并独立解码核对；项目目前无 Server/Shared 源表，服务端输出目录只有管理标记。新增 C# 类型/文件名冲突、XML 注释转义及两端代码分发测试；实际配置类与覆盖全部标量/数组的夹具以 C# 7.3 编译通过（0 警告、0 错误），服务端 dotnet build 通过。编译因沙箱外 NuGet 用户配置读取权限失败后，通过授权的构建命令完成。新增临时 C# 7.3 运行测试，39 项检查通过：实际 art_resource 文件加载、全部标量/数组、两种 Key 查询、条件查询、只读属性/列表、重载及版本/magic/截断/重复 Key/非法 UTF-8/bool/枚举/长度的失败保护。未执行 Unity 导入或跨平台读取。

2026-10-15：源表新增 Unit Prefab，当前 133 条配置已实际导出；客户端脚本放在 Assets/Scripts，ConfigManager 和 ResourceManager 继承 Singleton<T>。服务端构建与真实 Unity 程序集兼容编译均为 0 警告、0 错误；Unity 导入、Console 和实际资源读取尚未验证。

ConfigBinaryReader 位于 shared/Runtime/Config，由双端共用，不由导表工具生成或覆盖；工具模板目录只保留表结构相关模板。

生成的私有成员使用 m_ 加大驼峰命名，成员变量均带中文注释。
