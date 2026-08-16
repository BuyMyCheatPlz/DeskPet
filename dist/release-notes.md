# DeskPet for Windows v1.0.0

将 macOS 版 DeskPet（桌面宠物 + 动态岛）移植为 Windows 原生应用（C# / WPF / .NET 8）。

## 包含
- 桌面宠物序列帧动画 + 行为 AI（idle/walk/sleep/yawn/happy/hurt）
- 圆形可拖动悬浮窗（替代灵动岛），深色风格二/三级菜单
- 戳一戳 vs 抚摸（动画 + 心情不同）、宠物属性（心情/清洁度）
- 开启「鼠标穿透」时模型自动远离光标（GetCursorPos 精确检测 + 高速躲避）
- 音乐联动跳舞、AI 对话（DeepSeek/OpenAI/自定义）、开机自启动
- 皮肤：内置 猫/狗/兔/熊猫 + 打包的 deepseek-girl
- 托盘/悬浮窗/设置界面全部中文化，设置项带说明

## 下载
- **DeskPet-setup-1.0.0.exe** — 在线引导安装包（.NET 8 Desktop Runtime 检测）
- **DeskPet-1.0.0-win-x64.zip** — 便携版（解压即用，需自行安装 .NET 8 Desktop Runtime）

## 运行要求
- Windows 10/11 x64
- 安装包模式：若机器缺 .NET 8 Desktop Runtime，引导会自动提示下载
  winget install Microsoft.DotNet.DesktopRuntime.8
