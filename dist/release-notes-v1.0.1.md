# DeskPet for Windows v1.0.1

DeskPet（桌面宠物 + 动态岛）Windows 原生实现（C# / WPF / .NET 8）。

## 🆕 本版更新（v1.0.0 → v1.0.1）
- **AI 回复头顶气泡**：AI 对话发送后，桌宠头顶弹出**云朵状气泡**显示 AI 回复文字，约 6 秒后自动消失；气泡在模型上方，不遮挡宠物，文字自适应居中（`PetWindow.ShowSpeechBubble`）。
- **walk 动作起步/循环/移动解耦**：`config.json` 支持 `loopStart`（循环起始帧）与 `moveStart`（移动起始帧）分开配置——起步帧只播一次、不入循环，移动可从更早的帧开始。内置 deepseek-girl：
  - `walk_0`：第 50 帧开始移动，第 102 帧起循环
  - `walk_1`：第 25 帧开始移动，第 103 帧起循环
- 内置皮肤 `deepseek-girl` 现已纳入源码仓库。

## 包含功能
- 桌面宠物序列帧动画 + 行为 AI（idle/walk/sleep/yawn/happy/hurt）
- 圆形可拖动悬浮窗（替代灵动岛），深色风格菜单
- 戳一戳 vs 抚摸（动画 + 心情不同）、心情/清洁度属性
- 开启「鼠标穿透」时模型高速远离光标（GetCursorPos 精确检测）
- 音乐联动跳舞、AI 对话（DeepSeek/OpenAI/自定义）、开机自启动
- 皮肤：猫/狗/兔/熊猫 + deepseek-girl；导入自定义皮肤
- 托盘/悬浮窗/设置界面全中文，设置项带说明

## 下载
- **DeskPet-setup-1.0.1.exe** — 在线引导安装包（自动检测 .NET 8 Desktop Runtime）
  - 若缺运行时：`winget install Microsoft.DotNet.DesktopRuntime.8`

## 运行要求
- Windows 10/11 x64
- 安装包模式会自动检测并引导安装 .NET 8 Desktop Runtime
（源码快照由 GitHub 自动提供：Source code zip / tar.gz）
