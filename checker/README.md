# Flex 点胶时间检查 — C# 稳定性重写版

仅重写检查器；原工程只读，未修改提取器、未设置开机启动、未推送 Git。运行工具不依赖 Python、SDK 或预装 .NET。

## 运行

双击 **`publish-reviewed/FlexTimeChecker.exe`**（独立审查修复版，自包含 win-x64，.NET 8 WinForms + AntdUI 2.4.4）。原 `publish/` 保留未覆盖。程序和状态目录必须放本机磁盘。

- 首次运行为空配置；左上角 **配置** 新增点胶/组装站，或 **导入 v1.3.6 配置…** 显式选择旧 JSON、预览并保存。不会搜索、自动迁移、修改原工程配置。
- `Dispense` = 点胶站；`Bind` = 组装站。可增删、修改名称、目录及各站拆除/清胶分钟。建议由提取器落到本机后再查。
- 默认配置/返工状态保存在 `%LOCALAPPDATA%/FlexTimeCheckerStable/`，与 EXE 分离。可用 `--data-dir <本机目录>` 指定；测试只指定 `checker/test-logs/` 下的隔离目录。
- 主界面不显示目录；浅色 AntdUI、深色 BOSCH、配置/时钟、等高扫码与查询、两大判定卡、196 高日志、蓝色良品/橙色返工圆环。圆环保持控件实例，不整排 Clear 重建。
- 查询/统计只运行一个隔离进程；忙碌时不排队扫码，请等待可查询后重扫。可以取消。统计每 60 秒刷新，查询成功也更新。
- 出现未知/坏配置时明确警告并保留原文件；在配置界面明确确认替换后先备份，再原子保存。备份已存在时停止覆盖，请人工保管备份。
- 返工状态坏文件不会静默清空或覆盖；需要人工保留文件并排查。内部程序错误停止接收任务，保留有上限的 `last-fault.txt`，不伪装“正常未找到”。
- 缺少 `config.json` 但存在 `rework.json` 时进入配置恢复，暂停查询/统计，不当作首次空配置启动。删除或暂不配置某站不会抹掉它的返工；重新配置该站后继续使用原记录。
- 同一 `--data-dir` 由 `.instance.lock` 的 Windows 内核独占文件句柄保护，覆盖其他 Windows 会话；正常退出或进程终止自动释放句柄，锁文件本身保留，不要删除。不同数据目录可独立运行。关闭窗口会等待正在进行的配置备份/原子保存结束，不在后台写入未完成时退出。

## 兼容规则与明确安全差异

1. 仅递归 `Request*.xml`（不区分文件名大小写）。Housing 精确、不区分大小写。先直接查 Housing；只有未命中再查组装 `item` 属性，取最新绑定 Housing 后取最新点胶时间。不是模糊匹配。
2. 物料沿用所有非 schema 属性，不限 identifier/matID/value。排除原有属性表：`name,dataType,typeNo,typeVar,batch,class,vendor,qty`；XML namespace 声明不是物料。
3. 扫 Housing 使用实际命中点胶站阈值；扫物料使用组装绑定站阈值。返工始终归实际命中的点胶站，与阈值站分开。
4. 所有站位取最新时间；同刻按标准化站位目录、文件路径的 OrdinalIgnoreCase/Ordinal 顺序确定，配置顺序不影响结果。
5. `elapsed < 拆除分钟` 才可拆除；`elapsed > 清胶分钟` 才可清胶；等于均不可。未来时间两项均禁止，并显示核对时钟的原因。
6. 产量按每站当前可读 Housing 去重；返工按实际点胶站 Housing 去重、持久化、不按午夜清空。仅对本次实际扫描且 Complete=true 的站裁剪消失 Housing。未扫描/已移除配置的站、目录不完整时保留原返工，不把不可达或空配置误判为删除。含历史保留站仍受 32 站 / 20 万条上限约束，超限会拒绝写入并保留原文件，需人工备份后核查历史记录。
7. **与旧版的安全差异：** 任一点胶目录未完整扫描时不能证明“最新”，查询 fail-closed。组装查询同理。坏 XML、删除/变化竞争、拒绝访问、junction/重解析点、限额均显示诊断而不放行；不是“忽略坏文件后仍放行”。这可能比旧版更保守，需要清理异常报文再重扫。
8. XML 禁用 DTD/外部解析；每文件 2 MiB / 2M 字符；扫码/Housing 512 字符；最多 32 站、20 万扫描条目、64 层、4096 个待枚举目录。配置 256 KiB、等待分钟 0–525600、超时 1–120 秒；返工 JSON 16 MiB，worker 结果 32 MiB。超过限额显式失败，不静默截断后放行。
9. 旧版返工 JSON 不自动迁移；原文件不变。新版本自身的返工状态在重启后保留。
10. XML 元素按旧 `XmlReader.Name` 精确匹配：只识别无前缀的 `header`、`partProcessed`、`item`；默认 namespace 下无前缀元素仍识别，`m:header` 等不识别，不能覆盖真实时间/Housing。非 schema 的带 namespace 属性仍可作为物料码。**明确安全差异：** 同一报文重复出现合法 `header` 或 `partProcessed` 一律拒绝，站位标不完整，不沿用旧版最后一个覆盖前一个的歧义行为。
11. 仅显式旧配置导入允许 JSON 注释、尾逗号；新版 schema 配置始终严格。非空 `Folders` 优先；旧 `Folders:null` / `[]` 使用明确提供的顶层 `MessageFolder`，不会猜旧工程目录。`Role` 先 Trim 再映射 Bind/Assembly/Dispense。错误类型/溢出分钟、数字路径、未知/空角色等作为导入校验失败，不冒泡为 UI 未处理异常；原文件不改。不沿用旧版坏配置静默回退和未知角色默认点胶的做法。

## 隔离与资源边界

- 报文目录的所有递归枚举、属性查询、文件读取、XML 解析都在可终止的 EXE worker 进程。UNC 卡住时杀进程并确认退出，不用 Task.WhenAny 留下卡死 I/O 线程。
- 每任务独立目录、JobId、配置 SHA-256、generation；只接收同一任务/配置结果。取消/退出不应用旧结果。查询和统计共用单一 gate，无后台任务无界排队。
- 父进程退出监控与 worker 自身限时 watchdog；若无法确认进程死亡，锁住任务入口，不再不断创建孤儿。尚未实际复现 Windows 内核拒绝结束进程的极端情况。
- 最多容忍 4 个未清理任务目录项，达到即停止新任务并要求退出后检查占用、清理；不会无限累计失败缓存。正常/超时/取消会删除任务目录。
- 无长期 XML 缓存，无强制 GC。UI 日志最多 200 条，每条截断 300 字符；诊断最多 50 条；故障文件固定覆盖、有长度上限。所有字体、绘图对象、枚举器、文件、进程、timer 都有明确 dispose。
- **边界：** 配置/返工/任务存储只支持可靠本机磁盘。不要把 LocalAppData 或 `--data-dir` 通过 junction/symlink 重定向到网络。普通 UNC/网络盘路径被检查拒绝用于状态存储，但系统级重解析重定向未完整防护；这类持久化 I/O 仍可能阻塞，不应宣称已覆盖。报文目录中的 junction 则明确跳过并标为不完整。

## 构建与自测

在 `checker/` 执行（SDK 实测 9.0.308，目标 net8.0-windows）：

```text
dotnet run --project FlexTimeChecker.Tests
dotnet build FlexTimeChecker -c Release
dotnet publish FlexTimeChecker -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:RuntimeFrameworkVersion=8.0.22 --ignore-failed-sources -o publish-reviewed
publish-reviewed/FlexTimeChecker.exe --self-test --report test-logs/reviewed/self-test.txt
publish-reviewed/FlexTimeChecker.exe --ui-test --report test-logs/reviewed/ui-test.txt
publish-reviewed/FlexTimeChecker.exe --soak-seconds 120 --report test-logs/reviewed/soak-seconds.txt
python scripts/verify_reviewed.py
python scripts/make_report.py
```

发布使用本机已缓存 .NET 8.0.22 runtime pack；没有声称这是当前最新安全补丁。上线前应在受控网络更新 .NET 8 安全补丁并重跑测试。AntdUI 包使用缓存的 2.4.4。

- `FlexTimeChecker.Core/`：配置、XML、安全枚举、查询、返工、隔离协议；不引用 WinForms。
- `FlexTimeChecker/`：WinForms UI、配置编辑、CLI/worker 入口、UI 验收与短 soak。
- `FlexTimeChecker.Tests/`：无第三方测试框架的控制台断言；同套测试编入 EXE，发布 EXE 实际运行。
- `test-logs/01…24-*RED/GREEN.txt`：原逐行为证据；`25…35-*RED/GREEN.txt`：本次审查修复逐行为证据。19、22 和后续增加的前缀-only/查询判定断言为补充回归，不伪称单独先失败。
- `scripts/verify_startup.py`：仅开发辅助，验证真实启动、第二进程拒绝、定时正常关闭；运行产品不需要它。
- `scripts/verify_missing_config.py`：真实普通启动 + 3 秒退出，验证缺配置恢复无 worker、返工逐字节保留。
- `scripts/verify_instance_lock.py`：不持有 Local mutex 的独立内核文件锁持有者，验证启动拒绝、同目录路径别名竞争、退出后重新获取；未实际切换两个人工登录会话。
- `test-logs/reviewed/ui-screenshot.png`：reviewed EXE 实际 WinForms DrawToBitmap 截图，不是设计稿。配置保存关闭测试以可控等待点延迟持久化，随后执行真实原子保存并读回，断言关闭等待且无临时文件。

所有样本与故障均为隔离生成的模拟报文/真实本机文件操作、进程操作，不是产线实扫。短时资源曲线不能证明 7 天运行稳定；未做 7 天 soak、未接现场扫码枪、未复现真实 UNC 断链卡死。具体最终测试次数、时长、曲线见 `TEST-REPORT.md` 和原始日志。
