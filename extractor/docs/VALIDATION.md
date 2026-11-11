# 实际验证记录与验收边界

> 本文为原release历史证据，不代表独立审查修订版；**最新reviewed结果请看 [REVIEWED-VALIDATION.md](REVIEWED-VALIDATION.md)**。原900秒请求后在约340秒因Harness断源迁移遇到共享占用退出，失败历史保留；不能据此宣称产品崩溃或900秒完成。

## 已执行（不是计划）

| 项目 | 真实结果 | 证据 |
|---|---|---|
| 自包含发布 | UI/Service/Worker/Tests 全部 exit=0，net8.0-windows / win-x64，携带本机可用的.NET 8.0.22运行库 | `artifacts/publish-exit-codes.txt`、`49-final-publish.log` |
| 发布版UI入口自测 | **48/48通过，exit=0** | `artifacts/release-selftest-ui.log` |
| 发布版Service入口自测 | **48/48通过，exit=0** | `artifacts/release-selftest-service.log` |
| 真实文件夹压力/故障恢复 | 630份源文件；20份坏半XML+10份独占锁；点胶/组装共用目标；首轮60次可见故障（两角色），修复后630份全部复制、630份自有过期全部删除，外来文件及630份源文件保留；**46.105秒，exit=0** | `artifacts/48-release-stress.log` |
| 最终核心短soak | **实际120.880秒**，119次采样；父进程private基线13,262,848字节、峰值22,237,184字节（漂移8.55859375MiB）；句柄基线284、峰值340（+56）；35次新复制、18次可见故障；UI日志13行；磁盘日志1个共1739字节；exit=0 | `artifacts/47-release-soak.log` |
| UNC隔离 | 访问本机127.0.0.1随机不存在共享，超时/错误有界，后续本机扫描恢复 | 自测`TestMissingLoopbackUncIsBoundedAndRecovers` |
| 杀进程与取消 | 真正挂住Worker、2秒超时后终止；停止稳定等待不晚写；杀父进程后Worker退出且锁可重取；不同线程释放文件锁正常 | 相关Worker/ParentDeath/Engine自测 |
| 安全删除 | 离线purge、坏清单fail-closed、改变指纹文件不删、外来Request文件不删、pending崩溃恢复、目标junction拒绝 | Ownership/Recovery/Reparse自测 |
| UI视觉 | **实际启动发布EXE**，按自有PID/窗口截取浅色、深色、配置、高级、站位编辑；发现保留标签裁切后补RED/GREEN、修复并重新发布/截图 | `ui-main-light.png`、`ui-main-dark.png`、`ui-config.png`、`ui-advanced.png`、`ui-station.png` |
| 服务负向启动 | 未安装，直接启动无config.json的Service EXE，实际非零退出3762504530（未处理FileNotFound异常由宿主报告），未创建引擎 | `artifacts/service-missing-config.log` |

最终soak之后仅加强一个测试输入（未知字段测试显式带SchemaVersion），重新发布/再次运行两个48项自测；核心与soak时完全同SHA-256，证据 `artifacts/soak-binary-continuity.json`。实际最终核心SHA-256：

`7e1c6b368de6e65ece2b38297a65417feb8cc8194d858a2c445b76e29c7056c2`

## TDD证据

`artifacts/01-*` 起为按行为保存的真实日志。配置、迁移、重叠路径、递归Request复制、完整XML、同名同长碰撞、所有权清理、分层选择、OnlyNew、批量游标、稳定检查、重解析点、跨线程独占锁、隔离Worker、离线purge、子目录发现、幂等计数、有界日志、引擎重启、UI、Service、ACL、崩溃恢复等逐个RED→GREEN。后续审计补发现了源游标、空子目录、目标leaf junction、长标签、服务错误退出、自测配置隔离、旧路径归一化、缺schema等边界，仍各自记录失败后再修。

初次测试有编译错误/不恰当断言时保留原日志，另存真正失败断言的red/retry与green，不将编译失败冒充有效TDD。回归审计用例用于验证已实现行为，第一次通过不冒充“新增行为的RED”。

## 未验收 / 不能声称

1. **没有实跑7天、7×24；只有上述实际短soak，不能外推为长期可靠性认证。**
2. 没访问真实产线共享盘，没有真实网络拔线/域账号掉票/服务器SMB故障测试。已执行的网络探针仅本机随机不存在UNC；本机断源用目录移走/恢复模拟；真正长挂用实际被Job约束的hang进程验证。
3. 没安装或启动Windows SCM服务、没注册自启；尚未验证SCM恢复策略、域账户/跨用户目录权限和现场共享访问。仅Service EXE自测/负向console启动及Core后台引擎实跑。
4. 没把真实磁盘写满；已验证独占锁、坏XML、权限目录策略、取消、大小超限、坏清单；磁盘满走同一可见IO失败路径，但**物理磁盘满未实测**。
5. 没对恶意管理员实时交换路径做抗竞态证明；UNC别名/DFS/共享映射到同一物理源目标的重叠未验收。现场须让源与保存磁盘物理分离，可信管理员维护路径，不能配置不同别名指向同一树。
6. 只有当前Windows 11 x64上构建/执行，未在Windows 10现场、离线无.NET的另一台干净机器、非管理员域账号、杀毒白名单/代码签名条件下验收。携带完整自包含运行库与win-x64 PE已检查，但不冒充另一机器执行证明。
7. 未在Visual Studio GUI中打开/构建；实际工具链是.NET SDK9.0.308，目标.NET8。库版本使用现有成熟依赖，不宣称.NET8.0.22为当前最新安全补丁。
8. 20,000条/8MiB每目录所有权上限、默认16MiB文件上限、128个刷新子目录、每批200份等是显式容量边界。高于该产量的现场需进一步容量设计，不应直接无限调高而不做负载测试。
9. 未与旧版同时运行：旧版Global Mutex与本版文件锁不是同一互斥域，部署必须停止旧提取器/服务。

## 交付与范围

- 源码仅新 `extractor/` 下；未改旧工程/配置、未改checker、未git commit/push。
- 测试早期一次路径层级多算一层，把自建fixture写到兄弟test-data；发现后已把**自建测试目录**整体移回本extractor内`test-data-initial/`，其后所有fixture均位于EXE旁selftest-data。未留下该兄弟目录。
- `release/` 是已验证自包含目录；随包必须带Core、Worker、Tests和运行库/语言资源，而不只是GUI/Service两个EXE。
- 面向复制的zip排除运行config.json、自测数据、自测日志、pdb；保留安全占位example、README及架构/验证文档。
- 最终独立AI审查与上层检查器组合包由父代理负责；本记录不替代该审查。
