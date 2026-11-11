# Flex 报文提取 · C# 重写版 2.0

旧提取器1.3.2的独立重写；不修改旧工程、旧配置或源报文。目标平台：Windows x64，.NET 8，自包含目录发布。**整个 publish 目录一起复制，不能只拿一个 EXE。**

## 四个程序

- `FlexRequestExtractor.exe`：AntdUI 2.4.4 / WinForms，配置、开始/停止、浅色/深色、最近成功复制的文件/站位、状态和有界日志。最近文件显示最多1024字符，新启动为空，空扫描/清理不会抹掉本次运行的最近记录。
- `FlexRequestExtractor.Service.exe`：无界面 Windows Service 宿主。交付时未安装服务、未注册自启。
- `FlexExtractor.Worker.exe`：一次性、Job Object 约束的文件操作进程，不能独立用于提取。
- `FlexExtractor.Tests.exe`：无测试框架依赖的自动化用例、压力测试、短时 soak。

## 首次配置

1. 解压到固定本机目录；打开界面左上角**配置**。
2. 报文根目录填到产线 `Line_xxxx` 层。服务须用有权访问共享盘的账户和 UNC，而非映射盘。
3. 点胶站、组装站分别新增；填写显示名称、站位匹配规则和每站本机保存目录。站位规则与旧版一致：`31`/`0031` 匹配 `0031` 与 `0031_...`，完整名称精确或下划线前缀匹配，`*`/`all` 匹配全部。
4. 每站可**刷新子目录**，树形勾选；勾父包含下层，全部不勾即提取该站全部 `Request*.xml`。刷新也经隔离工作进程；关闭会先取消并等待子进程退出。
5. 高级参数在“时序与资源上限”页，输入框有中文名称、单位和范围，不要求编辑 JSON。
6. 保存后再开始。配置保存取得与引擎相同的独占锁；服务运行时禁止另一个界面保存/刷新/启动。

`config.example.json` 是安全占位示例，不会自动变成运行配置。缺失、损坏、未知字段、重复字段或版本错误不回退默认值启动。

## 旧配置迁移

在配置窗口点**导入旧版…**，只读取旧 JSON 到草稿，检查后保存到新程序旁 `config.json`。**源配置不会修改。** 支持旧 `DispenseItems`/`BindItems`，以及旧 `DispenseStations`/`BindStations`/`Stations` 和 `OutputRoot` 分组默认路径。只在两个 Items 都为空时采用旧站位列表；默认路径为旧 OutputRoot 下“点胶/组装”。保持旧版归一化数值范围。普通加载不会隐式迁移。

## 提取与保留

- 扫描当前本地日期往前 `min(14, max(LookbackDays, KeepDays+1))` 天的 `yyyy/MM/dd`；一个日期目录都没有时按旧版回退根目录。
- 递归仅提取 `Request*.xml`，不自动创建日期/站位目标子目录；不同站位可共用保存目录。
- 保留期限按源 `max(CreationTimeUtc, LastWriteTimeUtc)` 和当前 UTC 的滚动24小时计算。源过期不复制；保留原 creation/mtime，不解析 XML 内业务时间来删文件。
- `OnlyNewAfterStart` 使用首次启动基准减 Grace 秒，creation 和 mtime 都早于基准则跳过。**相同配置重启复用持久基准，不因崩溃/重启丢掉停机期间新文件。配置任一字段改变开始新会话基准。** 状态损坏时关闭失败，不偷偷重置。
- 源稳定检查同时比长度/mtime/creation；复制期间持有拒绝写入/删除的读句柄，并比对前后元数据和完整 SHA-256。写入同目录固定临时文件、flush、禁DTD的完整XML解析通过后才原子发布。
- 目标名始终是 `原Request名截断至72字符__源绝对路径SHA256前16位_内容SHA256前16位.xml`。不同来源或内容变化保留不同版本，**绝不静默覆盖**；目标已存在会校验完整 SHA-256，同长度不等于相同内容。哈希短名冲突但完整哈希不同会报错。
- 本地清理只看各保存目录自己的 `.flex-ownership-v2.json`，仅删除本程序曾发布且指纹未改变的过期文件。既有外来文件永不纳管/删除。清理先于源扫描且是独立工作操作；真正UNC源断网不会阻止本地过期清理，不在GUI探测UNC。**本机源路径及祖先发现重解析点、权限无法确认或文本源/目标重叠时，在清理前及每次删除前关闭失败，宁可不删。** 不要人工修改/删除所有权清单；清单损坏（包括缺少Files、null字段）只阻止该目标并报错，其余健康目标仍清理。
- **人工删除目标后的补拷策略**：保留所有权清单不动。下一轮purge先移除“目标已不存在”的旧记录，随后scan按原有保留期/OnlyNew/子目录选择重新复制。源仍存在且仍满足范围时最终会补拷；批量游标可能延后到后续轮次。只运行scan而未purge时会报“所有权目标记录冲突”，不能绕过事务；源已过期、被移走或不满足OnlyNew则不会补拷。要永久排除某源应改选取范围，而不是反复删目标或编辑清单。

## 资源与故障边界

- UI/服务同一个固定本地 `%ProgramData%\FlexExtractor.Sol.v2\engine.lease` 文件锁；没有用户自定义锁路径、没有线程归属 Mutex。失败即拒绝运行。
- 第一个进程创建专用ACL目录，仅当前用户、SYSTEM和Administrators全权。若由SYSTEM首次创建，普通UI账户可能需要管理员事先授予该**专用账户**修改权限；服务用其他域账户时同理。禁止给Everyone/Users/Authenticated Users写权限，否则拒绝启动。不要改成共享盘状态目录。
- 同时最多一个文件工作进程；所有 UNC 枚举/open/复制发生在子进程，超时/取消终止 Job，最多5秒确认退出。Job禁止子代进程，父死则杀子。工作进程还持有复制的引擎锁句柄，即便异常杀不掉也不能让下一实例与它并行；无法确认退出会保留本进程锁并进入致命故障。
- 每批最多 `MaxFilesPerPass` 份源文件；游标在危险文件IO之前持久推进，超时不永久饿死后面的文件，下轮完整遍历会重试。目录动态变化可能让一个循环先清空失效游标再重扫。扫描在进入子目录前跳过确定与选择无关的分支；选中目录的ACL/枚举故障按目录报错并继续健康同级分支，不用移动文件游标掩盖目录永久饥饿。勾父含后代、不勾=全部保持不变。
- 无永久handled缓存；目录递归上限64层；发现子目录上限128个、单相对路径256字符；配置64KiB；工作请求/响应64KiB；单结果最多32条错误，单条512字符；UI日志50–2000行；日志文件最多3×1MiB；单目标所有权清单20,000条/8MiB，达到上限报错停止新增，**不是无限扩容**。
- 源/目标相等及互为父子、可本机检查的源及祖先重解析点/junction/symlink被拒绝；保存目录仅允许本机磁盘。正常站位共用同一个目标目录受支持。**UNC别名、DFS、映射盘及共享回环到本机保存目录的物理同源不能靠离线文本校验识别**；现场必须确认源共享与保存树物理分离，不得用不同别名指向同一树。源和保存树须由可信管理员维护，勿运行时恶意交换路径；不能承诺抵御有本机管理员权限的攻击者。
- 磁盘满、共享锁、权限、半XML、超过大小上限等不会出现最终半成品XML；错误计数和日志可见，后续批次继续重试。致命内部异常不会伪装“正常”。

## Windows Service（未安装）

先用同目录界面保存配置并关闭，才交给现场管理员决定是否部署。服务名为 `FlexRequestExtractor`。安装前必须停用旧版提取器和旧服务；旧版的Global Mutex与新版文件锁**不是同一个互斥域**，因此不能让旧版与新版同时运行。

手动安装示例（**本项目没有执行这些命令**）：

```powershell
sc.exe create FlexRequestExtractor binPath= '"C:\FlexExtractor\FlexRequestExtractor.Service.exe"' start= demand
sc.exe start FlexRequestExtractor
sc.exe stop FlexRequestExtractor
```

改配置前停服务，再打开服务同目录GUI保存，关闭GUI，再启动服务。服务不运行WinForms、不弹消息框、不自动修复坏配置；启动配置错误会让宿主失败退出，需查看服务宿主错误/更正配置。账户网络权限、SCM恢复策略、现场ACL由现场管理员验收。

## 构建与验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1
.\publish\FlexExtractor.Tests.exe
.\publish\FlexRequestExtractor.exe --self-test
.\publish\FlexRequestExtractor.Service.exe --self-test
.\publish\FlexExtractor.Tests.exe --stress
.\publish\FlexExtractor.Tests.exe --soak 120
```

自测仅创建程序目录下 `selftest-data`，不读取运行配置、不碰产线共享盘、不装服务。`--self-test` 依赖随包 Tests EXE，并写 `selftest-ui.log` / `selftest-service.log`。测试会打开自有测试窗口、访问一次本机127.0.0.1的随机不存在UNC共享。测试目录可在无程序运行后删除。

实际证据、TDD RED/GREEN与验收边界见 `artifacts/`、`docs/ARCHITECTURE.md`、`docs/VALIDATION.md`。**没有实跑7天，不等于已认证7×24稳定。**

## 独立审查修订交付

本次修订运行目录为 `release-reviewed/`，不要混用原 `release/`；源码和运行压缩包采用 `reviewed` 名称。逐项RED/GREEN、最新完整回归、短时压力及900秒soak的实际结果见 `docs/REVIEWED-VALIDATION.md` 和 `artifacts/reviewed/`。旧验证记录保留为历史，不代表修订版结果。
