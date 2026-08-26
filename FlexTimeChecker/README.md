# FlexTimeChecker - 工业双击 exe 版
# 深色科技数字孪生风格 • Housing Code 点胶时间检查工具
# 支持扫码枪输入 • 拆除/清胶双卡片提示 • 无控制台

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 发布后 exe 位于：FlexTimeChecker/bin/Release/net8.0-windows/publish/FlexTimeChecker.exe
# 直接双击即可打开（无 cmd 窗口）
