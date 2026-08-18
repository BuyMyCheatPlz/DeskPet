# DeskPet for Windows v1.0.2

DeskPet（桌面宠物）Windows 原生实现（C# / WPF / .NET 8）。

## 🆕 本版更新（v1.0.1 → v1.0.2）
- **新的应用图标**：应用、安装包、开始菜单与桌面快捷方式统一使用内置 deepseek-girl 的形象帧图标（`assets/DeskPet.ico`，含 256/64/48/32/16 多尺寸）。

## 本版（1.0.1）以来包含
- AI 对话直接嵌入桌宠：点模型 → 底部输入框，不再新开窗口；Enter 发送；AI 回复以头顶云朵气泡显示。
- 对话时隐藏整块弹出面板，只保留输入框；状态条贴合宠物。

## 包含功能
- 桌面宠物序列帧动画 + 行为 AI（idle/walk/sleep/yawn/happy/hurt）
- 圆形可拖动悬浮窗（替代灵动岛），深色菜单
- 戳一戳 vs 抚摸、心情/清洁度属性
- 鼠标穿透时模型高速远离光标
- 音乐联动跳舞、AI 对话（DeepSeek/OpenAI/自定义）、开机自启动
- walk 起步/循环/移动可配（deepseek-girl 已内置配置）
- 皮肤：猫/狗/兔/熊猫 + deepseek-girl；导入自定义皮肤
- 全中文界面，设置项带说明

## 下载
- **DeskPet-setup-1.0.2.exe** — 在线引导安装包（自动检测 .NET 8 Desktop Runtime）
  - 若缺运行时：`winget install Microsoft.DotNet.DesktopRuntime.8`

## 运行要求
- Windows 10/11 x64
- 安装包模式会自动检测并引导安装 .NET 8 Desktop Runtime
（源码快照由 GitHub 自动提供：Source code zip / tar.gz）
