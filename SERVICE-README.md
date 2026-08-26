# Flex 报文提取 - Windows 服务

两个 exe 必须放在同一目录，共用 `config.json`：

```
FlexRequestExtractor.exe          配置界面
FlexRequestExtractor.Service.exe  无界面服务（INSTALL 注册的是这个）
config.json
INSTALL-SERVICE.bat
UNINSTALL-SERVICE.bat
```

## 公司电脑安装（开机自启）

1. 把整个 `extractor-publish` 拷到公司电脑固定目录（不要桌面）。
2. 先双击 `FlexRequestExtractor.exe`，在「配置」里填：
   - 报文目录：`\\WUJMESVMA198\Line_2753`（不要用映射盘给服务）
   - 点胶站 / 组装站：每条自己选保存路径
   - 保留天：默认 1（只留 24 小时内的本地报文，超时自动删）
   - 不再按年月日分，文件直接进该条保存路径
3. 保存后关掉界面，**不要点「开始提取」**。
4. 右键 `INSTALL-SERVICE.bat` → 以管理员身份运行。
5. `sc start FlexRequestExtractor`
6. 服务「登录」账户必须能访问共享盘。LocalSystem 看不到你映射的 X:，请改成有权限的域账户，或继续用 UNC。

卸载：管理员运行 `UNINSTALL-SERVICE.bat`。

改配置：

```
sc stop FlexRequestExtractor
打开同目录 FlexRequestExtractor.exe → 配置 → 保存 → 关掉（不要开始提取）
sc start FlexRequestExtractor
```

界面和服务不能同时监控。

本机开发环境默认不安装服务。
