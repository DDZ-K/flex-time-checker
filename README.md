# Flex 点胶时间检查

扫 Housing（或 PCB/Cover 物料码），在本地报文里查点胶时间，判断**能否拆除 / 能否清胶**。

当前检查器 **v1.2.0**（2026-08-25）：AntdUI 浅色工位壳，最大化后两张大卡占主视觉。工位电脑不需要安装 .NET。

**可运行程序在 [Releases](https://github.com/DDZ-K/flex-time-checker/releases) 下载，不要只 clone 源码。**

## 两个工具

| 工具 | 版本 | 干什么 |
|------|------|--------|
| `FlexTimeChecker.exe` | 1.2.0 | 工位扫码，显示能否拆除 / 能否清胶 |
| `FlexRequestExtractor.exe` | 1.3.1 | 从产线共享盘按站位把 `Request*.xml` 拷到本地 |
| `FlexRequestExtractor.Service.exe` | 1.3.1 | 提取器的无界面 Windows 服务 |

检查器只查本地目录。共享盘按日期刷新，直接搜会慢，所以先用提取器拷到本地。

## 检查器（清胶 / 拆除）

1. 从 Release 下载 `FlexTimeChecker-v1.2.0.zip`，解压到固定目录（不要放桌面）。
2. 双击 `FlexTimeChecker.exe`。打开会最大化。
3. 左上角 **配置**：分别加 **点胶站** 和 **组装站** 的本地报文目录，以及拆除/清胶等待分钟。
4. 扫 Housing，或扫 PCB/Cover 物料码。中间两张大卡只显示「可以 / 不可以」。

查询逻辑：

- 点胶站：`partProcessed/@identifier` = Housing，`header/@timeStamp` = 点胶时间
- 组装站：`item/@identifier` = 物料码，对应同一份报文里的 Housing
- 顺序：先当 Housing 查点胶站；找不到再当物料查组装站，拿到 Housing 后再回点胶站取时间
- 拆除/清胶分钟只跟点胶站目录走
- 只递归枚举 `Request*.xml`

界面约定：标题下没有小字，主界面不显示报文路径，BOSCH 是深色标。

## 报文提取

共享盘结构类似：

```text
\\服务器\Line_xxxx\
  2026\08\24\
    0021_1_1_0_0\
    0031_1_1_1_1\
```

报文目录填到 `Line_xxxx` 这一层。服务请用 UNC，不要用映射盘。

配置要点：

- 点胶站 / 组装站分开，**每条站位自己选保存路径**
- 本地不再按年/月/日或站位自动分子目录
- 只拷 `Request*.xml`，不改源文件
- **保留天**（默认 1）：超时自动删本地过期报文
- 界面和服务不能同时监控
- 检查器把对应保存路径分别加进点胶站 / 组装站

服务安装见 `SERVICE-README.md`。本机开发环境默认不安装服务，也不注册开机自启。

## 自测

```text
FlexTimeChecker.exe --self-test
FlexRequestExtractor.exe --self-test
FlexRequestExtractor.Service.exe --self-test
```

## 源码

- `FlexTimeChecker/` — 检查器（.NET 8 WinForms + AntdUI 2.4.4）
- `FlexRequestExtractor/` — 提取器界面
- `FlexRequestExtractor.Core/` — 提取逻辑
- `FlexRequestExtractor.Service/` — Windows 服务

本地配置 `config.json` 不入库。请复制 `config.example.json` 再改路径。
