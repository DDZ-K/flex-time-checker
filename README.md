# Flex 点胶查询与报文提取 — C# sol 重写试用版

以检查器 v1.3.6、提取器 v1.3.2 为功能基线独立重写，版本标识 **v2.0.0-sol-rc1**。由 gpt-6.1-sol 辅助开发和验证；原工程和旧发布版不覆盖。C# / .NET 8 WinForms + AntdUI 2.4.4，Windows x64。

## 下载包用途

- `FlexTools-v2.0.0-sol-rc1-Source.zip`：给其他AI/开发者审查的完整C#源码、项目文件、构建脚本、功能契约和测试证据。没有现场配置或真实报文。
- `FlexTools-v2.0.0-sol-rc1-win-x64.zip`：自包含运行包，分为 `Checker/` 和 `Extractor/`。

不要覆盖旧版。解压到新的可靠本机目录。查询器双击 `Checker/FlexTimeChecker.exe`；提取器双击 `Extractor/FlexRequestExtractor.exe`。**Extractor目录必须整体复制**，随附Worker、Service、Tests与运行库不是可随意删除的缓存。程序不需要安装Python或.NET；编译源码另需SDK和依赖。

## 首次部署

1. 停止旧查询器、旧提取器及旧提取服务，保留整个旧目录与配置作为回退。
2. 新版界面分别点配置→显式导入旧版JSON，检查站位、目录、分钟与保留天，再保存；不自动改原文件。
3. 查询器状态默认 `%LOCALAPPDATA%/FlexTimeCheckerStable/`；提取器配置在自身目录，独占锁状态使用专用本机ProgramData目录。详见各自README。
4. 先在测试报文目录核对Housing/物料查询、阈值、返工和提取清理；确认后再由现场授权迁移。不自动安装服务或设置开机自启。
5. 回退先停止新版并确认工作进程结束，再恢复旧版，禁止新旧同时写同一保存目录。

## 源码构建与测试

Windows 10/11 x64；本机构建工具链 .NET SDK **9.0.308**，目标 `net8.0-windows`。依赖AntdUI 2.4.4；服务引用版本见 `extractor/Service/Service.csproj`。首次恢复需NuGet网络或相应包缓存。运行包已自包含运行库8.0.22，**不声称最新安全补丁**；正式投产前应在受控环境升级运行库安全补丁并重新验证。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-all.ps1
.\run\Checker\FlexTimeChecker.exe --self-test --report .\checker-tests.txt
.\run\Checker\FlexTimeChecker.exe --ui-test --report .\checker-ui-tests.txt
.\run\Extractor\FlexRequestExtractor.exe --self-test
.\run\Extractor\FlexRequestExtractor.Service.exe --self-test
.\run\Extractor\FlexExtractor.Tests.exe --stress
.\run\Extractor\FlexExtractor.Tests.exe --soak 900
```

自测只使用合成XML/临时测试目录，不读取运行配置、不安装服务。Service自测是服务入口/核心验证，**不是SCM现场安装验证**。

源码布局：`checker/FlexTimeChecker.Core`、`checker/FlexTimeChecker`、`checker/FlexTimeChecker.Tests`；`extractor/Core`、`UI`、`Service`、`Worker`、`Tests`。`docs/legacy-contract.md`记录旧功能，`docs/REVIEW-HANDOFF.md`解释审查重点和兼容差异。各子项目报告保留真实失败、修复及压力结果。

## 兼容与容量边界

- 拆除严格“小于分钟”、清胶严格“大于分钟”，等于不放行；未来时间/扫描不完整更保守，错误可见而不当成已通过。
- Housing优先直接查；物料从组装反查Housing，使用组装站阈值，但返工归实际点胶站。
- 旧配置仅显式导入；**旧返工记录不自动迁移**。新返工按站/Housing持久化，不跨午夜清零；未扫描站、缺配置或不可达不当成删除。
- 提取目标文件增加来源/内容哈希以防同名覆盖，仍满足Request*.xml；只自动清理新版拥有且指纹未改的文件。旧版遗留文件须人工备份管理。
- 每目标所有权清单上限20,000条/8MiB；高产量站先评估，不能不测试就无限扩大。其余XML/目录/任务/日志上限见子项目README。
- 源与保存树须物理分离。UNC/DFS别名的物理重叠须现场排除；不支持把本机状态目录经junction重定向到网络。

## 稳定性声明

设计目标为故障隔离、可恢复和资源有界，**不承诺7×24永不崩溃**。已做本机真实文件/进程/GUI自测及故障注入，但未做7天现场连续运行、真实产线SMB拔线/域账号过期、物理磁盘写满、扫码枪现场和SCM账户验收。短soak不能外推为长期可靠性认证；投产前先旁路试用。
