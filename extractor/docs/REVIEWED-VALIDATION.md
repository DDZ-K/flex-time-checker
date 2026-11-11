# 独立审查修订版：实际验证与限制

本轮仅修改新工程 `extractor/`，运行目录为 `release-reviewed/`。原 `release/` 的程序文件没有覆盖；未改 checker、旧生产工程或配置，未安装服务、未注册自启、未提交/push。

## 已修复并真实复现

| 审查项 | 复现与修复 | 证据（artifacts/reviewed/） |
|---|---|---|
| P1a 本机源junction指向保存目录，purge先删源 | RED真实删除当前别名源；本机源及祖先在配置验证、purge开始和每次删除前检查，重解析点/无法确认/文本重叠关闭失败；真正UNC离线purge不探网络 | P1a-RED.log / P1a-GREEN.log |
| P1b 未选ACL拒读目录阻断整个站 | RED连续失败；进入子目录前判选择相关性，逐目录隔离打开/惰性枚举异常；父选含后代、空选全部，健康分支继续，不靠文件游标伪装进展 | P1b-RED.log / P1b-GREEN.log |
| P1c 首个坏清单阻断后续清理 | RED健康目标永不清理；每目标捕获预期IO/格式异常并带目标路径报告，坏目标不改，其余继续。另测null记录字段及重解析目标 | P1c-RED.log / P1c-GREEN.log、P1c-null-* |
| P2 所有权清单{} | RED被当空；已有清单必须显式有Files，缺字段关闭失败；不存在清单和Files:[]合法 | P2-manifest-* |
| P2 旧Items=null迁移 | RED NullReferenceException；仅显式旧版导入时归一化两组null为[]，保留旧回退顺序与另一组显式items | P2-legacy-* |
| P2 最近文件/站位 | RED缺UI控件；真实Worker→Engine→UI显示最近成功复制源和站位，空轮次不清空，传输长度有界 | P2-recent-* |
| 目标手删策略 | 不改变事务语义：purge移除已缺失记录，scan再补拷仍在保留期/OnlyNew/子目录范围的源；README明确，不支持靠删目标永久排除源 | TestReviewManualTargetDeletionRepairedAfterPurge |

## Soak故障注入修订

历史900秒请求在约340秒因测试Harness直接MoveDirectory遇到占用退出，**不是已经确认的产品崩溃**，也没有完成900秒验收。旧Harness反复只写6种内容，复制数停在35本身不能证明恢复。

- 原失败历史完整保留于 `prior-soak-900-aborted-at-340s.log`；独立审查原始结果保留于 `independent-review-baseline.jsonl`。
- 新Harness只对Windows错误码5/32/33的临时IO异常最多重试5秒；超时仍失败，不吞永久权限/共享故障。
- 另把同一真实共享占用保持24秒，超过重试预算时reviewed进程确实exit=1、未输出成功RESULT，`soak-forced-sharing-reviewed-permanent.log`，验证并未吞掉永久故障。
- 通过真正GENERIC_READ且无FILE_SHARE_DELETE的目录句柄持锁17.5秒，原release在首次断源MoveDirectory必败；reviewed版等待释放后完成断源、恢复，并验证恢复后新marker准确落地。证据 `soak-forced-sharing-baseline.log` / `soak-forced-sharing-reviewed.log`。对应自动回归为 `TestReviewSoakSharingViolationInjectionRecovers`。
- 新版本每轮写唯一内容；每次断源必须看到引擎失败计数增加，恢复后10秒内必须校验一个全新的、独立命名marker及其目标完整字节；记录每轮OFFLINE / RESTORED / RECOVERED，不允许靠空转采样通过。

## 已完成的修订版实测

- 源码完整测试：64/64通过，`full-source-tests.log`。
- 自包含发布四程序全部exit=0，`publish-summary.json`、`publish-*.log`。
- reviewed发布版：64/64通过，`release-full-tests.log`；GUI入口和Service入口各64/64通过、exit=0，`ui-selftest.log` / `service-selftest.log`。
- 真实630文件压力：630复制、60次注入故障、修复后630全部恢复，630自有过期目标删除，外来文件和630源文件保留；44.910秒、exit=0，`release-stress.log`。
- Job超时、取消后无晚写、杀父杀worker、lease释放/互斥、UNC不存在共享恢复均在64项实际测试中执行。
- 真正reviewed GUI按自有PID捕获浅色/深色窗口：`ui-main-light.png` / `ui-main-dark.png`。新增最近站位/文件两行完整可读，计数、开始/配置按钮、日志标题无遮挡。

## 长Soak及源码包验收

- **reviewed版900秒soak已完成，exit=0**：实际900.274秒、887次采样；479份新版本成功发布；118次可见失败计数（坏XML/断源等注入及结束取消；最后采样117，停止后118）；22次断源、22次恢复均逐次校验新marker字节，非空转。52份源文件保留（30初始+22恢复marker）。UI日志最多50行；磁盘1个日志共38,430字节。父进程private基线13,340,672字节、峰值25,239,552字节，漂移11,898,880字节；句柄基线290、峰值360（+70），末次采样282。证据 `soak-900.log`、`soak-900-summary.json` 和原测试目录samples.json。
- 源码压缩包已真实解包构建：0警告/0错误、exit=0，解包后64/64自测通过，`source-archive-build.log` / `source-archive-tests.log`；最终交付源码包亦重新解包构建/测试，末次结果存放 `final-source-archive-build.log` / `final-source-archive-tests.log`（包外验收日志，避免打包自引用）。
- 运行包ZIP完整性检查通过，498个EXE/DLL与reviewed目录逐一SHA-256相同，不带运行config.json和selftest-data，`runtime-package-validation.json`。
- 原release的498个EXE/DLL与原运行包SHA-256逐一相同，`original-release-unchanged.json`。为复现旧Harness故障仅运行原Tests并产生它自己的selftest-data，没有覆盖原程序。
- 长soak对应Core SHA-256：`c40c4fcfbcdb9ba2a1dc5828f17064e0408ff132be1f7057d365ec846901567a`；测试程序集SHA-256：`825ff606689562c5bc2d0c0069b11e4fe0ea6c70198730b888a3b59cfafcb4f7`。soak后未修改这些程序，后续仅更新交付文档/打包工具。

交付路径：
- EXE：`release-reviewed/FlexRequestExtractor.exe`，整个目录一起复制。
- 后台宿主：`release-reviewed/FlexRequestExtractor.Service.exe`（未安装）。
- 源码：`packages/FlexRequestExtractor-v2.0.0-reviewed-source.zip`。
- 运行包：`packages/FlexRequestExtractor-v2.0.0-reviewed-win-x64.zip`。
- 最终包SHA-256与字节数：`artifacts/reviewed/package-hashes.json`。

## 剩余边界

1. 未实跑7天/7×24，900秒也不能外推为长期稳定认证。
2. 未访问真实产线SMB共享，未实测拔网线、域凭据过期、磁盘写满或Windows 10现场。
3. 未安装/启动SCM服务，服务账户ACL/现场共享授权与SCM恢复策略需管理员验收。
4. 不探测UNC以保障离线清理，因此UNC别名、DFS、映射盘/回环共享与本机保存目录物理重叠仍须现场排除；源与保存树必须物理分离。
5. 静态路径检查不保证抵抗可信管理员被替代后的恶意实时路径交换；源和保存树必须由可信管理员维护。
6. 容量上限仍为每目标20,000条/8MiB所有权清单等README列出的边界。源文件始终不主动删除，旧版/新版互斥域不同，部署先停旧版。
