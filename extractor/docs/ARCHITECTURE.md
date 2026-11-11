# 架构与只读基线

## 基线核对

只读查看原工程 `README.md`、`CONTEXT.md`、`SERVICE-README.md`，`FlexRequestExtractor.Core/*.cs`、GUI与Service源码/csproj。旧提取器基线1.3.2，检查器1.3.6未改。原源码确认：

| 行为 | 1.3.2基线 / 本次实现 |
|---|---|
| 日期目录 | 本地日期 `yyyy/MM/dd` 回溯，至少KeepDays+1，最多14；没有日期目录则回退WatchRoot |
| 匹配 | 精确、token+下划线前缀、数字补4位、`*`/`all` |
| 默认新站 | 点胶0031；组装0061 |
| 子目录 | 相对路径树；父包含后代；未选全部；`.`只表示站位根文件 |
| 输出 | 每站SaveFolder；不自动按日期/站名分目录；允许站共享目标 |
| 保留 | `max(creation,mtime)`，滚动KeepDays×24小时；默认1天 |
| OnlyNew | `max(creation,mtime) >= 会话基准 - NewFileGraceSec`；本次持久保存基准 |
| 默认参数 | Poll=1秒、Lookback=2、Keep=1、Grace=2秒、MinBytes=16、StableChecks=1、StableInterval=120ms、MaxLogLines=500 |
| 迁移 | 两组Items都空才回退旧站位列表，最后回退Stations；OutputRoot下点胶/组装 |

新的安全约束是有意变更，不冒充旧版行为：坏配置/未知字段fail-closed；完整XML/DTD禁用；内容Hash；碰撞名；只清理持久自有文件；拒绝路径重叠/重解析点；仅本机目标；显式资源上限。旧版UI偶有“刷新后不允许全部不勾”的缺陷不保留，采用README约定的“未勾选=全部”。

## 组件

- **Core**：配置/迁移、路径策略、原子本地文件、持久所有权清单、Scanner、OwnerLease、Job Object、WorkerRunner、有界日志、Engine/会话。
- **Worker**：只执行一次操作：`purge`、`scan`、`discover`；stdin JSON、stdout JSON，无旁路文件IPC。启动后先等完整请求；没有父进程复制的锁句柄则不能进行文件IO。
- **UI**：AntdUI按钮 + 原生布局/树/数字输入；参数都在配置窗口；UI每500ms拉取一次快照和有界日志，避免每文件BeginInvoke队列洪泛。
- **Service**：Microsoft.Extensions.Hosting.WindowsServices 8.0.1，独立EXE、不引用WinForms；与UI使用相同Core和同目录config.json。启动/致命错误重抛且返回非零，不假装正常。
- **Tests**：Console断言，无xUnit/NUnit依赖；真实本机文件、独立工作进程、真实Job/句柄/ACL、真实WinForms消息循环；自测注入测试配置，不读取部署配置。

## 独占和停止协议

1. UI/Service先打开固定ProgramData专用本机ACL目录的`engine.lease`，FileShare.None，失败不运行。
2. 父创建带KILL_ON_JOB_CLOSE和ActiveProcessLimit=1的Job，启动只等stdin的Worker。
3. Job分配成功，并把同一个引擎锁句柄DuplicateHandle到Worker后，才通过stdin发送有界请求。Worker验证该句柄确实对应约定状态目录的lease。
4. Worker在该Job内不得派生另一个进程；父崩溃关Job，Worker终止。若系统故障导致进程暂时终止不了，Worker仍持有同一个独占文件对象，下一实例也无法取得引擎锁。
5. 超时或停止先TerminateJobObject，再最多5秒WaitForExit。只有确认停止后父才释放owner。无法确认则Poison并保持静态强引用，UI进入不可再次启动的致命故障。
6. Engine的Run只用有界异步管道/等待，不把不可取消UNC操作放进线程池，不用Task.WhenAny遗留网络线程。文件锁无Mutex线程归属问题。

## 复制事务与删除

- `.flex-copy-v2.tmp` 是每目标一个固定、可重用临时文件，避免被kill后随机临时文件无限增长。
- 源检查大小/mtime/creation写稳；打开Read+FileShare.Read，禁止并发写/删除；流式复制、flush、源再Hash和元数据核对、禁DTD完整XML读取。
- 目标名字含源路径和内容hash短码；存在时读取完整SHA-256，内容不同拒绝覆盖。源同长同mtime但内容改变也产生新版本。
- 所有权清单先持久写pending，再无覆盖File.Move原子发布，最后写committed。若第二次清单提交失败/崩溃，后续purge核对完整指纹后恢复pending；未发布pending可移除。
- Purge只对清单内已发布、creation/mtime/hash未改变的过期文件File.Delete；从不递归删除保存目录，不枚举源文件作为删除前提。修订版在任何删除前验证本机源路径及祖先无重解析点、文本路径无重叠；真正UNC源则完全不网络探测，保留离线清理。外来文件即使名为Request且过期也不删。每目标清理故障隔离并报告，损坏目标不改，其余健康目标继续。
- 本地manifest只允许本目标目录下的Request文件名，记录数/大小有上限，坏格式拒绝操作。

## 扫描进展

每源文件危险IO之前写固定游标。批量上限到达结束进程，下批从游标后继续；到枚举末尾清空游标，下一个循环重试之前锁住/半成品文件。游标文件失效时可能先做一遍寻找并清空，再重扫，不会永久锁在一个报文。目录枚举中的系统级长卡仍由整个工作进程超时隔离。

修订版进入目录前根据站位命中项的选择并集判断是否相关：完全不选=全站，父选包含后代，选中深层时只经过必要祖先；确定无关的兄弟目录不读取。枚举器创建和惰性MoveNext的预期IO/权限/重解析错误局限在当前目录并计入有界结果，健康同级继续。目录错误不写文件游标，不以跳过错误目录永久隐藏故障；下一轮仍重试选中故障分支。

## 安全假设与明确边界

- 提取器不是抵抗本机管理员/恶意目录竞态的安全沙箱；保存树、源路径和ACL由可信管理员管理。静态重解析点检查不等于对恶意实时替换路径的完整抗竞态证明。
- 源和目标文本规范化路径重叠被拒绝；**UNC共享别名、DFS、另一主机共享映射到本机同一物理目录的别名重叠未验证**。现场须保证源共享与本机保存磁盘物理分离，不能仅用不同别名绕过此要求。
- 独占域是重写版UI/Service；与旧1.3.2的Global Mutex不同。升级必须停旧版。
- 清单每目录上限20,000，系统吞吐量、文件规模、XML体积需要现场容量验证；达到上限会报警停新增，不允许无限缓存。
- Windows Service的SCM安装、域账户授权、恢复策略由现场管理员验收；开发机未安装/注册。

## 可复用验证流程（限定写在本工程）

逐个垂直行为：先只加一个失败断言→保存真实RED日志→最小实现→完整回归GREEN日志。生产改动后重新publish、在干净目录重跑UI/Service自测；真实文件压力与短soak记录实际秒数/内存/句柄/日志字节；最后按PID捕获真正发布EXE窗口、目检，并校验发布文件SHA-256。不能用计划/假输出/长达7天的推断替代实际运行证据。
