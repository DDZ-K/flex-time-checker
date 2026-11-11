# reviewed 版本实际交付测试报告

## 交付标识

- EXE：`publish-reviewed/FlexTimeChecker.exe`，72991939 字节；self-contained win-x64 single-file。
- SHA-256：`392e7bb90214c4db84b9175da4a6a83cd295e56e71dd2f6c92d7710ac37067ef`。
- 源文件/资源/辅助脚本清单与 SHA-256：`MANIFEST.json`（29 项，不包含 bin/obj）。
- 构建：SDK 9.0.308；`net8.0-windows`；缓存 AntdUI 2.4.4；自包含 runtime 8.0.22。
- `reviewed-build.txt`：真实 build 0 警告 / 0 错误；`reviewed-publish.txt`：真实 publish 成功。
- 原 `publish/FlexTimeChecker.exe` 未覆盖，SHA-256 仍为 `c64e3fecc82b9c66420ba8d6099bd0c3a58eda7b3a2df5e704852501805900f8`。开工发现其 900 秒 soak，未终止它或父测试进程，本轮发布/验收均使用独立目录；未把旧 EXE 长测冒充 reviewed 验证。
- 程序、自测、UI、soak 都在本机 Windows 执行；没有测试产线报文、扫码枪或真实共享盘断链。

## 发布 EXE 实际运行

| 项目 | 退出码 | 实际墙钟秒 |
|---|---:|---:|
| `self-test` | 0 | 7.883 |
| `ui-test` | 0 | 25.563 |
| `soak-seconds` | 0 | 121.142 |
| `verify_startup` | 0 | 3.407 |
| `verify_missing_config` | 0 | 3.399 |
| `verify_instance_lock` | 0 | 5.87 |

- 核心/隔离自测：`RESULT total=29 failed=0`。
- UI 验收：4 组全部通过，包含配置保存中关闭等待及真实原子保存读回、无残余临时文件、真实 worker 查询、未来时间禁止、取消/查询中关闭、日志上限、配置编辑/校验、坏配置保留返工、控件等高、196 日志区、圆环 HWND 保持与截图。
- 普通启动测试：真实启动 EXE，第二个进程返回 10（拒绝重复实例），第一个进程定时正常关闭，返工状态有真实写出。
- 测试环境把 DOTNET_ROOT 指向不存在目录，禁止 multilevel lookup；发布 runtimeconfig 的 includedFrameworks 为 NETCore/WindowsDesktop 8.0.22；不是仅构建 DLL 后声称 EXE 可用。
- 最后进程检查：该 checker 目录下的 FlexTimeChecker 进程剩余 0。
- 真实首次启动使用每次新建的隔离目录，确认不是读取历史状态冒充本轮写出；缺配置而有返工样本另测，3 秒正常关闭，无 jobs，原状态逐字节不变。
- `verify_instance_lock.py` 用不持有 Local mutex 的独立 CreateFileW 内核独占锁持有者竞争，真实 EXE 返回 10 且不写状态；释放后可启动，同目录路径别名竞争被拒绝，退出后可重获锁。未实际创建第二个登录会话。
- `test-logs/reviewed/ui-screenshot.png` 为最终真实 UI 截图；扫码/查询等高、两判定卡、BOSCH、圆环和日志布局正常，无明显裁切/重叠。

## 最终短时持久压力/故障测试

```text
PASS UI SOAK elapsedSeconds=120.843 cycles=218 writes=2582 changes=2582 deletes=1083 handlesMin=289 handlesMax=336 privateGrowthBytes=14974976 finalLogLines=200 finalLogChars=10960 remainingJobs=0
```

- 连续真实 WinForms 消息循环、重复查询、每轮新 worker；同一轮后台修改/创建/删除模拟 XML，插入半截 XML、反复创建/删除子目录，最后清理故障文件并验证正常查询恢复。
- CSV 样本 14 行；每 10 秒左右记录句柄、private/working/managed bytes、UI 日志行/字数、剩余任务目录。
- 起始 private bytes：49078272；结束：64053248。
- 曲线原始数据：`test-logs/reviewed/soak-metrics.csv`；日志：`test-logs/reviewed/soak-seconds.txt`。
- 这是约 2 分钟模拟数据短 soak，不是 7 天现场运行；没有据此承诺 7×24 无故障。

## RED → GREEN 留证

- 共 33 个真实 RED/GREEN 成对日志；脚本逐个验证 RED 存在 FAIL/AssertionError、对应 GREEN 无失败。
- 本轮审查修复 11 对（25…35）；25=m:header 冒认，26=重复业务 XML，27=未扫描返工保留，28=真实缺配置恢复，29=3 类导入错误类型异常，30=旧 JSON 注释/尾逗号，31=null/空 Folders 回退，32=Role.Trim，33=配置写入等待中关闭，34=内核文件锁竞争，35=历史站保留后写入限额。
- 33 最终 RED 为真实关闭时窗口提前销毁断言失败，不是仅“缺少方法”；测试等待点放行后执行真实本机原子保存并读回。前缀-only/查询不可提前清胶等为额外回归断言，不伪称各自先 RED。16 的历史测试仅针对无前缀 item 上的 namespace 属性，不表示兼容带前缀业务元素。
- 每个新增主行为先跑缺失/错误行为失败，再补实现；初始缺失类型使用反射断言，因此 RED 不是编译错误冒充业务失败。
- `19-junction-regression.txt`、`22-ui-close-regression.txt` 是已有行为的补充回归，不伪称这些测试先失败。
- 曾发现并修复：XML 限额未实施、坏 XML 未归类、按钮 WaveSize 导致“控件等高但画面不等高”、UI 测试缺真实 Application.Run 消息循环、worker 异常明细丢失、遗留任务目录无硬上限、旧配置 null 站位/类型触发空引用、坏配置恢复时统计误清返工。这些问题的失败证据未删。

## 明确差异与剩余风险

- **P1 已修复：** XML 恢复旧 `XmlReader.Name` 精确匹配，无前缀默认 namespace 仍接受，m:header/m:partProcessed/m:item 不可覆盖业务字段；重复合法 header/partProcessed 新增明确 fail-closed 安全差异，避免歧义报文放行。
- **P1 已修复：** 返工从原 state copy，仅实际扫描且 Complete=true 的站裁剪。空配置/暂删站/扫描不完整保留记录，恢复站位后继续显示；只有完整扫描证明报文消失才下降。缺 config 但有 rework 启动恢复模式而非空配置统计。
- **P2 已修复：** 顶层 string/巨大整数分钟、数字 MessageFolder 变为 ConfigDialog 已捕获的明确校验失败；仅显式旧入口允许 JSON 注释/尾逗号，新 schema 始终严格。非空 Folders 优先，null/空列表回退显式顶层目录，Role 先 Trim 后映射；无路径不猜旧工程，未知/空角色拒绝而非旧版静默点胶。
- **P2 已修复：** 配置备份与保存纳入 active task，退出会等待；同 data-dir 的 `.instance.lock` 文件由跨会话 Windows 内核独占句柄保护，读配置前获取，文件不删除、退出仅释放句柄；不同数据目录可独立运行。
- 保留的历史站同样计入 32 站 / 20 万条返工上限，写前校验，超限保留原文件，不会写出自身下一次无法加载的状态；需人工备份并核查历史站，不能无界保留。
- 原工程/提取器未改；源码是独立 Core + WinForms 新实现，没有复制旧查询/返工核心。
- 新版更保守：目录/文件不完整、异常 XML、junction、超限会 fail-closed，不像旧版忽略后仍放行；未来时间禁止并解释。各项阈值边界保持原业务规则。
- 旧 v1.3.6 配置只能显式选取导入；旧返工 JSON 不自动迁移。新版本自身返工持久化并随完整快照的报文消失下降。
- 程序/配置/返工/任务状态必须可靠本机盘；普通 UNC/网络盘拒绝作为状态目录。系统把 AppData 或 data-dir 经重解析点重定向到网络的情况未完整防护，可能仍造成持久化 I/O 阻塞，不支持这种部署。
- 普通报文查询可以保留旧 UNC 配置，I/O 在可结束的 worker 中。测试实际结束了模拟永久挂起子进程，并覆盖超时、取消、旧 generation 拒收、并发 gate；未实际复现现场 UNC 内核级卡死。
- 若 Windows 无法确认 worker 死亡，gate 不再放开，避免不断生成孤儿；这种极端 OS 故障未实际触发。若 jobs 清理受阻达到 4 项，会明确停止任务，需要人工排查。
- 忙时扫码不排队，等待就绪后重扫；配置不能与查询/统计重叠。所有日常数据量必须在 README 限额内。
- 运行包使用可用缓存的 .NET 8.0.22，不声称最新安全补丁；上线前需更新受控 runtime pack 后重跑验证。
- 没有注册服务、自启、生产数据输出或 Git push。未进行七天测试、真实扫码枪与产线互锁验证。
- 未实测两个人工登录会话；独立内核文件锁竞争已实测，不能冒称第二会话 UI 已验证。配置关闭等待本机持久化不保证经恶意重解析到网络的 I/O 可终止，这种部署不支持。
- 所有修改限于 checker；旧工程仅只读核对旧语义。此工作目录不是 Git 仓库，未伪造 Git diff/提交。原历史日志保留，本轮原始输出在 test-logs/reviewed/；重跑按 README 执行 verify_reviewed.py 后检查进程，再执行 make_report.py。
