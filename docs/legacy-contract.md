# Flex 旧版功能契约与独立验收清单

## 0. 基线、证据与边界

- 本文是**需求核查**，不是新实现评审。基线目录：`E:/Download/AI/Flex 点胶时间查询`；检查器 `1.3.6`，提取器界面/Core/服务 `1.3.2`。版本由各 `.csproj` 的 `Version` 确认，不以历史日志推断。
- 已静态读取 README、CONTEXT、两个 AppConfig、全部业务/UI/入口 C# 源文件、示例配置、服务说明及安装/卸载 PowerShell。未启动旧软件、未运行旧自测、未查询真实共享盘或报文、未读取实际 `config.json` 或服务运行日志，不涉及凭据。
- 下文路径相对上述旧项目；`文件:行号 / 方法` 可直接定位。缩写：`C/`=`FlexTimeChecker/`；`E/`=`FlexRequestExtractor/`；`K/`=`FlexRequestExtractor.Core/`；`S/`=`FlexRequestExtractor.Service/`。
- 证据优先级：实际入口调用及业务实现 > 自测断言 > README > CONTEXT 历史描述。README 有简化，不能照文字重新发明行为。
- 区分三类：**保持**＝正常产线语义必须保持；**披露修正**＝为稳定/安全可修，但必须在迁移说明及测试中明确旧新差异；**旧缺陷**＝不是要求新实现复现故障。

## 1. 检查器查询契约（保持）

### 1.1 输入、范围与查询链

证据：`C/RequestXmlSearch.cs:9-69 / Find`、`:71-191 / FindHousing, FindMaterial, ScanFolder`；UI 入口为 `C/MainForm.cs:235-259 / Query`。

1. 扫码先 Trim；空码核心返回 `扫码为空`，UI 空码只改状态为 `请先扫码`，不启动搜索、不清掉旧判定。
2. 按配置顺序分别取点胶站、组装站；只递归 `Request*.xml`，不是所有 XML，不是名称/文本模糊包含。Windows 文件系统模式下工作；代码比较扫码时 OrdinalIgnoreCase。
3. **先将扫码当 Housing 在全部点胶目录查询**。命中即结束，不再尝试同码物料。不能调整成“先组装”或将两个来源混在一起选最新。
4. Housing 未命中，才到组装站匹配物料，取组装报文自己的 Housing；再用该 Housing 回点胶站找时间。组装时间不是点胶时间。
5. 无点胶及组装目录：提示先配置；仅有点胶且未命中：提示若是 PCB/Cover 请配置组装站；组装物料已命中但点胶缺失：Found=false，保留 Housing、MaterialCode、BindFilePath，明确“点胶站未找到”。失败不得记返工或显示可以。
6. 物料若绑定多个 Housing，**先选最新有效组装报文，再查该 Housing**。选中的 Housing 没有点胶报文时，不回退到更老的组装绑定。
7. 缺失目录按 profile.Name 收集，失败错误尾部显示不可用名称；回查点胶时可能重复收集同一名称。成功不要求显示缺失目录。

### 1.2 XML 的真实匹配范围

证据：`C/RequestXmlSearch.cs:194-252 / TryReadRequest, IsSchemaAttribute`。

- 用 XmlReader 完整读取；DTD 禁止；忽略注释、空白、处理指令；源文件以 FileShare.ReadWrite 打开。解析异常、访问失败、坏时间、缺 Housing 均忽略该文件，不以文件修改时间补点胶时间。
- 识别的是**精确大小写的 reader.Name** `header`、`partProcessed`、`item`，属性名 `timeStamp`、`identifier` 也是精确名。不能宣称旧版支持任意命名空间：带前缀的元素名不会命中；没有前缀但有默认 namespace 的 reader.Name 仍可能命中。改用 LocalName/不区分大小写会扩大接受范围，需披露。
- Housing 来自 `partProcessed/@identifier`，Trim 后整码 OrdinalIgnoreCase 比较。不搜文件名，不做 substring。
- 时间来自 `header/@timeStamp`，用当前环境的 DateTimeOffset.TryParse；读取结束需 Housing 非空且 stamp != default。重复 header/partProcessed 会覆盖先前值；遇任意不可解析 header 立即 false；item 值则跨文件累计。没有结构关联约束，不要求 item 属于特定 array/event。
- 物料不止 identifier/matID/value：**任意 item 属性值**都能命中，唯独跳过精确属性名 `name`、`dataType`、`typeNo`、`typeVar`、`batch`、`class`、`vendor`、`qty`。每个值 Trim 后整码 OrdinalIgnoreCase。不要将重写限制到三个白名单属性，也不要扩成元素文本搜索。
- 不检查结果 PASS/FAIL、事件种类、站位 XML 字段，也不按条码长度/格式校验。加入这些过滤会改产量和查询结果，不是纯稳定性优化。

### 1.3 “最新”与 tie（须独立测）

证据：`C/RequestXmlSearch.cs:79-93,105-119,136-190`。

- 每个目录内部：并行解析，ConcurrentBag 收集，按 DateTimeOffset 的真实时间 `MaxBy(time)`，不是文件时间、文件名、枚举顺序或最先命中。并行度 clamp(CPU,2,8)。
- **同一目录内时间完全相同没有稳定次序保证**：ConcurrentBag + 并行枚举的首个最大值可不确定。不能声称有旧文件名 tie-break。新实现若增加确定性，应显式写出规则（例如路径排序），作为歧义收敛而非完全逐项复刻。
- 每个目录胜出后转 `LocalDateTime`；跨目录用严格 `>` 比较，所以相等时保留**较早配置目录**。通常等价比较同一绝对时间；本机 DST 重叠时刻存在 LocalDateTime 排序歧义，若改 UTC 排序需披露。
- Housing 直扫使用获胜点胶站的等待值。物料查询成功时 `Profile=bindHit.Profile ?? dispenseHit.Profile`，实际使用获胜**组装站**的拆除/清胶分钟，而不是点胶站分钟；FilePath 仍是获胜点胶文件。
- 计数 FilesScanned 在跨 profile 更新最佳值时取当时累计值；直扫返回的统计不一定包含后续未胜出的 profile。物料最终合并时才用最终累计值。修正统计口径可以，不要用该值做业务条件。

## 2. 等待边界与时间显示

证据：`C/WaitRules.cs:5-9`；`C/MainForm.cs:308-327 / ApplyResult`、`:349-357 / FormatSpan`；旧自测 `C/Program.cs:254-288`。

- elapsed = 本机 DateTime.Now − 解析出的点胶 LocalDateTime，按精确 TotalMinutes 判定，不先取整。
- **可以拆除：elapsed < max(0, DemolishMinutes)**。
- **可以清胶：elapsed > max(0, ClearGlueMinutes)**。
- 恰等于任一阈值时，该项不可以。不是 `>=` 等待达到即可拆除；拆除是“早期窗口”，清胶是“晚期窗口”。不强制两个阈值有大小关系；人为设成清胶小于拆除时中间区间可能两项都可以，禁止擅自排序/交换阈值。
- 旧版未来点胶时间产生负 elapsed，依公式可能显示“拆除可以”；FormatSpan 却把负数显示成 `0分`。这是应披露的时钟异常修正候选（建议异常态不放行），不能悄悄改变后声称完全相同。
- 判定及详细时间**只在查询成功时计算一次**。顶部时钟每秒刷新，但旧结果不会随时间自动跨阈值变化。新增实时刷新是行为变化，应说明。
- 正常详细区显示输入链、点胶时间、本机时间、已过时间和剩余清胶时间。精度显示到秒，判定仍保留原精度。清胶恰等阈值会显示还需零时间但仍不可以。

## 3. 检查器配置全量迁移

证据：`C/AppConfig.cs:8-22,24-111`、`C/FolderProfile.cs:5-49`；示例 `C/config.example.json`。

| 字段 | 类默认值 / 归一化 / 迁移含义 |
|---|---|
| MessageFolder | `E:\Download\AI\Flex 点胶时间查询\报文文件夹`；空白回此默认。仅单目录历史迁移及旧未调用引擎使用。不要把开发机硬编码路径冒充用户生产配置。 |
| DemolishMinutes | 顶层默认120，负数归0；迁移到默认 profile。 |
| ClearGlueMinutes | 顶层默认240，负数归0；迁移到默认 profile。 |
| MaxReadBytes | 默认262144，最少4096；**当前 RequestXmlSearch 不使用**，不应据此截断 XML。 |
| FileExtensions | 默认 `.txt,.log,.csv,.xml,.json,.dat`；null/空数组还原；补点、小写、去重；**当前查询不使用**，仍只扫 Request*.xml。 |
| Folders | 默认空；null/空数组先从顶层生成一条 `默认目录` 点胶配置，随后逐条 Normalize、剔除空 MessageFolder。保持列表顺序、不按名字排序、不去重。 |
| Folders[].Name | 构造默认 `新目录`；空白变 `未命名目录`，否则 Trim。 |
| Folders[].MessageFolder | 默认空、Trim；不转绝对路径、不修 UNC；空路径 profile 在 Normalize 后剔除。 |
| Folders[].Role | 默认 Dispense；Trim 后 Bind（忽略大小写）、包含“组装”或“绑定”、或 Assembly（忽略大小写）变 Bind，其余一律 Dispense。 |
| Folders[].DemolishMinutes / ClearGlueMinutes | 默认120/240，各自负数归0，无引擎上限。UI 编辑器限制0..100000（打开大值再保存会夹断）。 |

其他迁移要点：

- 检查器配置在**可执行进程所在目录**（优先 Environment.ProcessPath，退回 AppContext.BaseDirectory），不是当前工作目录（`C/AppConfig.cs:24-39`）。
- JSON 属性名不分大小写，允许注释和 trailing comma，写入缩进，忽略 null 属性。Load 全部吞异常回默认；Save Normalize 后直接覆盖，非原子、无备份。
- Folders 非空时不会从顶层补漏；如果初始列表含空目录，过滤后可变空，本次 Normalize 不重新生成默认目录。下一次 Normalize 又会迁移顶层默认。删除全部目录并保存会重新出现顶层默认目录，是旧行为陷阱，不是“保存空配置可靠”。
- 示例点胶等待为30/240，组装120/240；这不是类默认。示例两条路径为空，首次 Normalize 剔除后无有效目录。
- 旧 `DispenseSearch`（`C/DispenseSearch.cs:21-180`）保留了文件名/文本/正则时间引擎，但所有入口和自测只调用 RequestXmlSearch；全仓调用搜索未见 DispenseSearch.Find 调用。迁移保留其旧配置字段兼容即可，**不要恢复 TXT/LOG 模糊查询作为新产线能力**。SearchResult 类型恰好仍定义在此文件顶部。

## 4. 返工、产量、归属与持久化

证据：`C/ReworkTracker.cs:7-195`；`C/MainForm.cs:223-232,269-327,330-341`；`C/ReworkBoard.cs:56-154,262-270,308-352`。

- 每个点胶 profile 一张圆环；**不是按日、不是总历史累计、不是 XML 文件数**。Produced = 该点胶目录递归有效 Request XML 中去重 Housing 数，使用与查询相同解析器，时间无效也不计产量。不按报文 timeStamp 的日期过滤。
- 扫码成功并通过 UI 的时间/profile 校验后，就自动记一片返工；不论拆除或清胶是否可以，也不需要另点返工按钮。未知码/仅绑定成功而无点胶不记。
- 返工以 Housing OrdinalIgnoreCase 去重：同 Housing 再扫、改扫其 PCB/Cover，不加第二次。同 Housing 在不同点胶目录分别可以有记录，不能改成全局去重。
- **归属看 result.FilePath（点胶 XML），不是 result.Profile**（物料时是组装站）。TryRecord 按点胶配置顺序找首个 FileBelongsTo 的目录，用该目录做归属。重叠父子路径时可能归到较早父目录，而不是查询获胜 profile；禁止用组装 profile 记点胶返工。若为消歧义改归属规则须披露并迁移历史状态。
- FileBelongsTo 用 GetFullPath 后目录分隔边界判断，不是裸字符串前缀（防止 `station1` 误包 `station10`）。不是文件真实物理路径/链接解析。
- `rework.json` 在检查器 AppDir；结构为 Date + HousingsByFolder（key=Path.GetFullPath(folder)，value=Housing 列表）。Load 重建大小写不敏感字典并对列表去重；无效/损坏文件回空。Date 只读写元数据，**不会过零点清空**。
- Snapshot 重新扫 live Housing，已记录值与 live 相交并永久写回修剪结果。最后一份 Housing 报文被删后产量和返工都掉；删除其中重复 XML 而其他同 Housing 仍在则不掉。之后报文恢复只恢复产量，须重扫才再次成为返工。
- 不可访问目录旧版可能得到空 live 并清掉返工；部分枚举异常还可能使后台任务失败。新实现建议“不可访问不等于真实删除”，但应披露此安全修正，并区分缺失/成功空目录。
- 同一路径可配置多条 profile：不去重，每条圆环可能显示同一产量和同一返工。重命名不改存储 key；路径变化相当于另一站；移除配置不会主动删除未引用的历史键。尾随分隔符未专门统一，历史 key 迁移需小心。
- 快照刷新：首次 Shown、每60秒、查询结束/记录后、配置保存后；后台 seq 仅防陈旧 UI，不防 JSON 并发覆盖。旧版未做进程单例，多个检查器及并发 Snapshot/TryRecord 可能丢写。
- UI：good=max(0,Produced−Rework)，橙色返工、蓝色良品；无产量返工率“—”，零返工“0%”，否则一位小数；极小非零返工扇区有8度最低视觉面积，非精确比例。数字不变不重绘，数字变化复用卡片更新，**不整排 Clear 重建**（1.3.6 防闪屏契约）。

## 5. 提取器配置全量迁移

证据：`K/AppConfig.cs:8-176`、`K/StationTarget.cs:5-24`、`K/PathUtil.cs:5-29`。

| 字段 | 默认值 | Normalize / 含义 |
|---|---|---|
| WatchRoot | 空 | PathUtil.Normalize；源报文根，空不能扫描。 |
| OutputRoot | 空 | Normalize；空变 AppDir/报文缓存。是旧迁移和新条目默认路径基础，不是强制所有条目写入此处。 |
| DispenseItems / BindItems | 空列表 | 新式每站列表；Station、SaveFolder、SubFolders 各自归一化；空站号剔除；不对条目整体去重。 |
| DispenseStations / BindStations | 空列表 | 历史角色站号列表，Trim、去空、OrdinalIgnoreCase去重。仅两类 Items **同时为空**时迁移。 |
| Stations | 空列表 | 更早历史列表；若两类 Items 和角色列表都未产生条目，则全部迁成点胶站。 |
| PollIntervalSec | 1 | clamp 1..60；整轮完成后再等待，不是固定每秒整点触发。 |
| LookbackDays | 2 | clamp(max(原值,KeepDays+1),1,14)。KeepDays=14 时规范值为14，但扫描器自身仍 max(...,15)，实际可能看15天。 |
| KeepDays | 1 | <=0改1，再 clamp 1..14；滚动时长，不按日历零点。 |
| OnlyNewAfterStart | false | 是否过滤会话前旧文件，详见第7节。 |
| NewFileGraceSec | 2 | clamp 0..30。 |
| MinFileBytes | 16 | 最少1，没有最大上限。 |
| SizeStableChecks | 1 | clamp 0..8；0跳过大小稳定检查。 |
| SizeStableIntervalMs | 120 | clamp 0..2000；0不睡眠仍按次数刷新比较。 |
| MaxLogLines | 500 | clamp 50..2000；仅界面日志使用，服务另有固定500行裁剪。 |
| Items[].Station | 空 | Trim；数值补零仅在匹配阶段，不改存储字符串。 |
| Items[].SaveFolder | 空 | PathUtil.Normalize；空根据角色补 OutputRoot/点胶 或 OutputRoot/组装。不自动加站号或日期层。 |
| Items[].SubFolders | 空列表 | NormalizeList：Trim，反斜杠转正斜杠，去首尾 `/`，去空、OrdinalIgnoreCase去重；`.`保留。详见第6节。 |

迁移顺序与兼容性：

- 只要任一新式 Items 已非空，就**完全忽略**两个旧角色列表及 Stations 的补充迁移；即使新式内容后续因空 Station 被全部过滤也不回头迁移。
- 完成后重建 DispenseStations/BindStations 为各 Items 的去重 Station，Stations 为两者并集；这些旧字段仍会被写出。不能未经说明删字段或把全部历史站号解释为组装。
- AllItems 为点胶列表在前、组装在后；AllSaveFolders 从各条有效 SaveFolder 按 OrdinalIgnoreCase 去重，不额外折叠路径尾分隔符。
- JSON 属性名不分大小写，但**不允许注释及尾逗号**（与检查器不同）。Load 解码异常回新默认，Normalize 在反序列化 try/catch 外，null 条目等仍可能抛。Save 直接覆盖 config.json。
- 配置路径为 **AppContext.BaseDirectory/config.json**，界面/服务同目录才能共用；不是服务系统当前目录。
- PathUtil：Trim，`/`转`\`，单个开头反斜杠补成 UNC 两个；UNC 体内重复反斜杠折叠、去尾斜杠；非UNC用 GetFullPath（相对路径依进程工作目录），失败保留原串。单反斜杠补UNC是旧便利行为也有误解风险；不得拿检查器路径归一化套用此规则而不说明。
- 配置 UI 只编辑 WatchRoot、站位/各自保存路径/子目录、轮询、保留天、OnlyNew；其余隐藏字段通过 Clone 保留。新增默认站号点胶0031、组装0061，不代表无配置时引擎自动监控这些站（`E/ConfigDialog.cs:181-195`）。

## 6. 源目录、站位及子目录

证据：`K/ExtractEngine.cs:141-199,263-280`；`K/StationRules.cs:5-30`；`K/StationSubFolders.cs:5-109`；`E/ConfigDialog.cs:310-470`。

### 6.1 日期根与站位

- WatchRoot 应填到 Line 层。按**本机今日**向前 lookback 个日期构造 `yyyy/MM/dd`，存在才枚举；从今天到旧日，不排序 station/file。
- **只要任意一个日期目录存在，就不回退根目录**；全都不存在才把 WatchRoot 当 station 目录的父目录，枚举其直接子目录作为站位。不是全根任意深度发现站位，也不是能兼容填到 station 本身。
- 每日期目录只看直接 station 子目录，然后对选中站位递归 Request*.xml。
- StationRules：token Trim；`*` 或 all（不分大小写）全匹配；否则 folderName 与 token 完全相等或以 `token_` 开头（忽略大小写）；可 int.Parse 的 token 再用 D4 补零做相等/下划线前缀匹配。`31` 可命中 `0031_...`，不可随意 substring 命中 `131`。
- 同一源 station 可匹配多条点胶和组装配置，逐条处理；一个文件可以复制到多个目标目录。点胶目标先于组装目标，不能强制角色互斥。

### 6.2 子目录筛选的边界

- SubFolders=null/空列表表示**全站位所有层，包括 station 根文件**，不是“全不选就不拷”。
- 有选择时以文件父目录相对于 station 的路径匹配；选 `partProcessed` 包含该目录和 `partProcessed/ES 2` 等后代，不包含 `partProcessed2`；忽略大小写。
- 配置 `.` **只代表 station 根文件**，不代表全部后代；根文件只有选择根标记才命中。空项被 NormalizeList 去掉，因此不能用空字符串持久表示根。
- Discovery 聚合近期匹配 station 的所有后代目录，无需目录包含 Request XML；返回大小写不敏感排序后的相对路径，不返回 `.`。同样在无任何日期目录时才回退 root。
- UI 只点击“刷新”才发现，不自动发现。刷新保留仍存在的精确勾选项；未发现的历史勾选丢出列表，再保存可能变更筛选。父目录勾选不会自动把所有子项打勾，但核心会包含后代。
- **README“未勾选仍全拷”只符合核心/空列表**：UI 一旦列表非空且一个都没勾，保存会阻止；列表本身为空才可保存空列表表示全站。发现列表又不含根 `.`，所以普通 UI 无法新选“仅根文件”。重写若改进 UX，应保留核心空列表语义，说明 UI 差别。
- 旧筛选不拒绝 `..`、绝对样式或链接跳转；新实现做 containment/reparse point 防护属于安全修正，须说明，不可把正常嵌套父选包含子层的行为一并禁掉。

## 7. 复制、OnlyNew、去重与保留期

证据：`K/ExtractEngine.cs:46-57,141-425`；旧自测 `K/SelfTest.cs:28-102,104-188`。

### 7.1 每轮与复制门槛

1. ScanOnce 先验证 WatchRoot 非空、有站位、有保存目录、源根可访问；错误立即返回，**不会执行本地 purge**。因此共享盘断开时旧版不会继续清缓存。把保留期清理独立出来是有外部可见影响的修正，需要说明。
2. 尝试创建全部保存目录（错误吞掉）；按日期/站位/文件及匹配条目遍历。只认 Request 前缀和 .xml 后缀（TryCopy 又做 OrdinalIgnoreCase 校验）。不解析/校验 XML，源报文不完整或不是合法 XML 也可能复制。
3. handled key=`source完整枚举路径 + "|" + destDir`，值为源 LastWriteTimeUtc.Ticks；已有 prev>=ticks 静默忽略，不增加 skipped。不是文件内容哈希，不是源文件名全局去重。
4. 先过保留期门槛，再 OnlyNew，再 MinFileBytes，再大小稳定，再目标同名长度检查，然后复制。过期、OnlyNew过滤、目标长度相等均写 handled 并增 Skipped；过小/不稳定不写 handled，后续轮重试。
5. 大小稳定检查：每次等配置间隔后 Refresh，长度变/消失则返回；只看长度不看 mtime、不保证文件已写完。不稳定文件下一轮重试。源读取允许写共享，目标 FileMode.Create+FileShare.None，直接覆盖，不是临时文件原子替换。
6. 目标是 `SaveFolder/源文件名`，**扁平，不加日期/站号/子目录**；FileName保持不变。目标存在同长度就 skipped，不管内容/时间是否相同；不同长度覆盖。跨日期/站位/子目录同名有碰撞风险，无稳定“最新覆盖”保证（今天先扫，旧日期后扫）。
7. 复制后尽力继承源 LastWriteTime、CreationTime（失败吞掉），不修改源文件。成功记录 handled、Copied、LastFile、LastStation，日志角色及站位；复制异常增加 Failed，每轮可再试，LastError 不等于逐文件失败。
8. **会话内删除目标不会补拷**：源 mtime 没变则 handled 拦截，即使目标消失。旧自测明确断言 recopyAfterDelete=false。重启/PrepareSession 清 handled 后才重新考虑；同一源 mtime 回退或不变的内容更新也不重试。不能把“自动补齐删除文件”无声当纯稳定性优化。
9. ScanSnapshot 的 Copied/Skipped/Failed/Deleted 是**会话累计**，CopiedNames/DeletedNames 是本轮列表；验证失败的快照只有 Error、数值默认0，并不代表引擎累计值归零。每次 Start/PrepareSession 才重置所有计数、最近项、错误与会话起点。

### 7.2 OnlyNew 精确定义

- 默认 false；true 不等于“仅文件创建晚于启动”。cutoff=sessionStart−max(0,NewFileGraceSec)，使用源本地 CreationTime 和 LastWriteTime。
- **只有 CreationTime < cutoff 且 LastWriteTime < cutoff 才跳过**；任一时间恰等边界或更新都可进入下一门槛。创建老、修改新仍能拷。
- OnlyNew 跳过写入 handled；运行期间切开关不保证已跳过源重新进入（正式 UI 禁止运行中修改配置）。每次开始重建会话起点；停启不是延续旧水位。
- 它是内存会话窗口，不是磁盘持久 checkpoint，不是以 XML header 时间过滤。

### 7.3 保留期的时间依据与删除边界

- cutoff=**DateTime.Now−KeepDays天**，滚动24小时倍数；有效时刻取 `max(FileInfo.CreationTime, LastWriteTime)`（本机文件时间）。只有该较新值**严格小于** cutoff 才算过期；等于仍保留。
- 源复制前用同一规则过滤过期；本地清理用本地两个文件时间。**不读 XML header，不按目录日期、不按复制入库时间、不只用 mtime**。新拷入但源两个时间均旧的文件可被过滤；新建但 XML 很旧也可能保留。
- PurgeExpired 递归所有配置 SaveFolder 中 Request*.xml，**不限定由本程序复制、不限定选中站号、不限定当前 SubFolders**。非 Request 文件不删；过期文件删除失败吞异常且不增 Deleted。不删空目录。
- 多条相同路径大体以 AllSaveFolders 去重，父子目标仍可重复遍历。正常扫描源不可用时不 purge；PurgeExpired 公共方法单独调用不检查源。
- 保存路径若指向源、源祖先/子目录或用户其他报文区，旧版可能删真实源或其他应用 Request XML。这是最高优先级安全风险，**应拒绝危险路径并披露新增约束**，不能以“源只读”掩盖旧实现清理目标的破坏性。

## 8. 提取服务、互斥、生命周期

证据：`K/ExtractEngine.cs:18,59-139,427-463`；`S/Program.cs:9-22`；`S/Worker.cs:11-92`；`SERVICE-README.md`；`E/INSTALL-SERVICE.ps1`、`E/UNINSTALL-SERVICE.ps1`。

- 服务名 `FlexRequestExtractor`；可执行文件 `FlexRequestExtractor.Service.exe`。Host 配置 ContentRootPath=AppContext.BaseDirectory；服务无界面，与 GUI 引用相同 Core。
- **互斥的产品契约是 GUI 与服务只能一个引擎监控，甚至安装不同目录也共用一个名字**：`Global\FlexRequestExtractor.Engine`，失败退 `Local\FlexRequestExtractor.Engine`，再失败旧版竟然允许启动（fail-open）。不能改为按配置路径各跑一个而不披露。
- 旧 mutex 只看 new Mutex 的 createdNew，不对已存在但无持有者的 mutex 执行 WaitOne；Global退Local不能保证跨 session，线程所有权及 Stop 的 ReleaseMutex 吞异常有隐患。应改可靠同一全局锁、权限错误 fail-closed、所有权生命周期一致，属于应披露修正。
- Start 已运行返回true；否则取得锁→重置会话→长运行 worker立即 ScanOnce。单轮完成后等 PollIntervalSec，等待段约100ms检查取消；扫描/大小稳定/复制期间没有取消检查。
- Stop 先置 IsRunning=false，取消，最多等2秒后就 Dispose token并释放mutex，即使 worker仍在复制。新引擎可能与旧 worker重叠。**等待停止真正完成后才释放锁**是安全修正；不能因保留2秒表象而牺牲互斥。
- Worker 捕获扫描异常记“扫描失败”，继续下一轮；同一个 ScanSnapshot.Error 只变化时记日志，恢复后清 LastError。逐文件复制失败不一定进入 LastError。
- 服务启动记录日志；加载不到完整配置每5秒重试；拿不到引擎锁每5秒重试。拿到锁后每秒等待运行状态，直到停止。**运行中不热加载 config**；要停服务→GUI保存→关GUI→启服务。GUI可在服务运行时只编辑配置，但不要启动第二引擎。
- 服务日志位置 AppDir/logs/service.log，带日期时间；控制台也写。每20次写日志才检查，超过500行裁到最后500；不是 MaxLogLines 配置控制，不是每天新文件。
- 安装脚本需管理员；已有服务先停再删，重新创建 Automatic，设置失败恢复三次 restart/5000、reset86400；**安装不直接启动**。卸载停止并删除服务，不删除配置、缓存或日志。脚本重建服务可能丢失先前服务登录账户等设置，重写部署说明应提醒。
- 服务访问共享盘使用 UNC、有权限的服务登录账户；不能依赖交互用户映射盘，也不能内置/日志输出密码。开发机默认不安装服务、不注册开机自启。

## 9. UI 与交付约定

证据：`README.md:19-35,48-60`；`C/MainForm.cs`、`C/FolderProfilesDialog.cs`、`C/ReworkBoard.cs`；`E/MainForm.cs`、`E/ConfigDialog.cs`；两份 `Branding.cs`；入口/项目文件。

### 检查器

- Windows .NET8 WinForms，旧版 AntdUI2.4.4；中文版，PerMonitorV2；独立可运行exe、不依赖Python（CONTEXT交付要求）。打包自包含/资源及服务脚本由交付实际验证，不能仅编译通过就称交付完成。
- 启动最大化；白浅色工位界面，左上“配置”、深色BOSCH标，旁边无多余标题/副标题；主界面不暴露报文路径，等待分钟仅在配置里编辑。
- 扫码框禁输入法，回车/查询按钮同一入口，搜索框与按钮同高；查询中禁止按钮重复提交，busy时后续 Query忽略，**没有扫码队列**。输入框未禁用但完成后全选并重新聚焦。
- 两大卡标题“能否拆除/能否清胶”；初始待查询，搜索中，未命中未知；成功“可以”绿/“不可以”红。时间明细、最近记录区、站位返工圆环是功能组成，不可只交一个输入框和文本结果。
- 配置分点胶/组装，新增、编辑、删除、取消/保存；编辑可变角色、名字、目录、两项分钟；只要求目录非空，不要求当前可访问，未给名字取目录末段。外层使用克隆，取消不落盘；保存写 config。
- 查询后台异常没有完整 finally/错误派发，可能永久busy；关闭窗体后BeginInvoke、配置中途变动及返工后台异常有竞态；日志无上限，计时器/Font/移除卡片的资源释放也有长跑隐患。修复不应改变正常扫码优先级与判定阈值。

### 提取器

- 深色GUI，左上配置，手动“开始提取/停止提取”，不会打开即自动监控；两张统计卡已复制/失败，最近文件/站位/跳过/过期删除、受MaxLogLines限制的日志。
- 每次开始重新Load配置；GUI本引擎运行时不允许开配置；关闭窗口停止GUI引擎，不是最小化到托盘、不替代服务安装。
- 配置点胶/组装分别列表，新增/编辑（含双击）/删除，各站独立保存路径；站位编辑刷新多层子目录；路径/站号必填。主配置至少一条站位及源根非空，不要求共享盘此时可访问。
- 两工具logo/icon嵌入程序集，不能要求用户依赖原工程绝对路径找图片。

## 10. 必须公开的安全/稳定性修正清单

以下不是允许随意改业务，而是应在新项目 README/变更说明明确“旧行为 → 新行为 → 依据/迁移影响”，并独立测试：

| 优先级 | 旧问题及定位 | 推荐修正 / 不可偷改的边界 |
|---|---|---|
| P0 | 清理目标与源可重叠，且递归删所有Request文件（K/ExtractEngine:213-258） | 规范化、边界比较、拒绝源/目标危险重叠；对既有混用目标明确阻止/提示，不能默认删除不属于程序的文件。改变为仅删受管文件必须声明保留期覆盖范围变化。 |
| P0 | mutex fail-open、跨session退化及停机2秒即释放（K/ExtractEngine:81-103,427-460） | 可靠全局互斥，真正停止再释放；错误可见，不默许双引擎。维持全机GUI/服务单监控契约。 |
| P0 | 直接覆盖目标可能被检查器读到半文件；同长度忽略不同内容（K/ExtractEngine:341-363,420-425） | 可用同目录临时文件+原子提交；同名冲突采取明确策略并记录，不能悄悄改为以mtime/哈希覆盖而仍称逐项相同。源始终只读。 |
| P1 | config/rework直接写，损坏静默回默认，并发丢记录（C/AppConfig:41-69；C/ReworkTracker:158-191；K/AppConfig:49-72） | 原子保存、备份/错误提示、并发串行化、保留未知字段策略。坏配置不得静默启动危险默认目录清理。历史格式和站位顺序迁移保持。 |
| P1 | 不可访问被当无Housing永久清返工（C/ReworkTracker:32-72,116-137） | 将成功扫描空与失败分开；失败保留上次有效记录并显示异常，真实删除后仍应掉量。 |
| P1 | 惰性枚举try范围不覆盖遍历、部分并行异常逃出（C/RequestXmlSearch:123-174；C/ReworkTracker:121-137；K/ExtractEngine:160-199） | 按目录隔离权限/消失/网络异常，保留已得到的有效结果并报告部分失败，不把异常静默当完整未命中。防链接环路但保持正常递归。 |
| P1 | 未来时间仍可能“拆除可以”（C/MainForm:308-322） | 若加时钟偏差异常不放行，明确阈值及允许偏差；不要悄悄把所有负elapsed夹0后当正常。 |
| P1 | OnlyNew/handled 不补拷已删目标、自测特意断言（K/SelfTest:163-188） | 原则上保持；若变更为缺失自愈需作为产品行为更改说明，而非无声修正。过期清理与手工删除都需覆盖。 |
| P2 | 同时间tie、父子目录归属、同名覆盖不确定 | 各自定规则、列差异；不能改最新时间优先、跨profile相等先配置优先、物料用组装分钟。 |
| P2 | 长跑日志/字体/控件未释放、退出回调与busy异常 | 限流日志、释放资源、统一取消/异常回UI；保持1.3.6圆环不整排重建和正常扫码响应。 |

**不得以“更稳定”为名改变**：拆除 `<` / 清胶 `>`、先Housing后物料链、最新有效报文选择、物料组装分钟、返工按点胶FilePath归属、Housing去重、随当前文件存活不跨午夜清零、扁平目标、不动源、父选包含子层、空SubFolders全站、max(创建/修改)保留期、OnlyNew双时间AND判旧及宽限边界、GUI不自动开始、GUI/服务互斥。

## 11. 给父代理/其他AI的独立验收矩阵

以下是**应验证项，不表示本文已执行**。所有夹具仅放新项目测试临时区，绝不指向旧实际配置、源报文或共享盘。

### 检查器核心与迁移

- [ ] 原旧自测覆盖项全保留：Housing直扫、identifier物料、matID、value/Bottom、未知码、仅组装无点胶、分别30/240及15/100等待值；参考 `C/Program.cs:237-288`。
- [ ] 整码/Trim/大小写，Request筛选、递归、坏XML/坏时间/DTD/多header/前缀namespace/非白名单item属性、被排除属性不命中。
- [ ] 同码同时是Housing与物料，必须Housing优先；多个组装绑定先选最新且点胶缺失不回退。
- [ ] 多点胶最新获胜；跨目录同时间配置顺序获胜；同目录tie新规则写清；时间offset转换验证（不要依赖运行机正好UTC+8）。
- [ ] 两阈值前一瞬、恰等、后一瞬，零阈值、阈值逆序、未来时间政策；禁止只测整数29/30/240/241分钟。
- [ ] 顶层旧单目录迁移、空Folders、全空路径、中文/Assembly Role、空名字、负数、隐藏字段保留、注释/尾逗号兼容；不恢复未调用文本引擎。
- [ ] 产量同Housing多文件只算一，重复扫码/扫物料只记一次，不同站可分别记；物料分钟来自组装但返工归点胶；未找到不记。
- [ ] 删除最后报文掉产量/返工，保留另一重复报文不掉；跨午夜不清；删除后恢复不自动恢复返工；仅临时不可访问的新政策测试。
- [ ] rework.json迁移路径key、大小写、重叠目录、重复profile、损坏/并发写处理；旧自测产量4/返工2和另站2/1，删Housing两份后1/0（`C/Program.cs:290-315`）。

### 提取核心、迁移与服务

- [ ] 原旧自测断言复现：分点胶/组装独立目录、站号31与0021、非Request/非选站不拷、无日期/站号目标子层、OnlyNew过滤、删除目标不重拷、旧源过滤/旧目标purge、父目录过滤与发现（`K/SelfTest.cs:163-188`）。
- [ ] 新Items优先级、两类历史Stations迁移、仅一类Items已有时不补另一类、空Station清理后不再回迁、所有隐藏字段和上限、Keep14/实际lookback15差异。
- [ ] 源标准日期根/全无日期回退/部分日期存在不回退/跨午夜日期轮换/站位通配all或*及下划线边界。
- [ ] SubFolders空全站、`.`仅根、父包含后代不包含相似前缀、斜杠与大小写、UI刷新后未勾选禁止保存和历史勾选丢失政策。
- [ ] OnlyNew创建旧修改新/创建新修改旧/二者旧/恰等cutoff/宽限；保留期同四象限及严格小于边界，XML时间与文件时间故意冲突确认不读header。
- [ ] 大小不足、持续写入、同长度内容更改、源mtime不变/回退、同名跨站/跨日冲突、复制失败重试、只读源未变；新原子提交保证不会暴露半文件。
- [ ] 源断开是否继续purge的旧新差异、递归删除范围、源目标同/父子/链接路径拒绝、非Request文件始终不删。
- [ ] 统计累计与本轮列表区分、重启会话清零/水位重置、日志限制、长时间任务取消、停止未完成不放锁、GUI与服务/两个服务全局竞争、权限失败不双跑。
- [ ] 服务自测入口/无UI、配置缺失5秒重试、运行不热加载、日志路径与裁剪、安装不自动start、卸载不删数据；不要在开发机为测试真实注册服务。

### UI、交付与测试可信度

- [ ] 检查器最大化、主界面无路径无副标题、扫码回车与复焦、两张判定大卡、时间明细/日志、圆环只更新不闪屏；配置取消不落盘。
- [ ] 提取器手动开始/停止、运行禁配置、每站独立路径及子目录刷新、统计与日志、关闭真正停止。
- [ ] 交付可运行文件、资源、示例配置、服务脚本/说明，且没有实际生产配置、绝对开发机路径当默认生产值、凭据、真实条码样本外泄。
- [ ] 检查器旧 --self-test 在AppDir写 selftest-result.txt，测试读取固定2026样本且断言本地小时19/18；非+08环境可能失败。提取器旧自测只证明复制逻辑，写的是多根XML片段，**不能据其成功证明检查器能解析或端到端可用**（`K/SelfTest.cs:28-36,65-67`）。
- [ ] 新验收需要合法XML的提取→本地查询→物料映射→分钟判定→返工统计→清理后掉量端到端合成夹具；旧两个自测都没有配置迁移、mutex竞争、真实服务安装/权限或GUI自动化覆盖，不能将“旧selftest通过”当完整等价结论。

## 12. 核查结论

最容易被“重写得更合理”而改错的是：**拆除严格早于窗口；物料用组装站分钟但返工归点胶文件；返工随本地Housing存在、不按日期；保留期看创建/修改较新值；OnlyNew判旧用两个时间AND；目标删了会话内不补拷；子目录空全选与UI禁止空勾选的差异**。最严重的稳定性/安全边界是**目标清理误删源/其他报文、锁fail-open/提前释放、非原子落盘和配置/返工并发丢写**。上述应明确披露修正；其他正常产线语义不可借机重定义。
