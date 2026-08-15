# DeskPet for Windows

将 macOS 版 **DeskPet**（桌面宠物 + boring.notch 动态岛）移植到 Windows 的原生应用，使用 **C# / WPF（.NET 8）** 实现。已移除原版顶部的"灵动岛"，改为：宠物回巢时自动走向**主屏右下角的系统托盘溢出菜单**，到达后淡出消失。

> **本项目自包含于 `DeskPet.Windows/` 一个文件夹内**，没有引用 macOS 工程的任何外部文件（无 `PET.md`、无 `deskpet/`、无 `BoringNotchXPCHelper` 依赖；宠物皮肤在运行时从 `图片/BoringPet/` 动态生成/导入）。因此**发布时只需分发 `DeskPet.Windows` 这个文件夹**即可。

## 功能对照

| macOS 原版功能 | Windows 实现 | 说明 |
|---|---|---|
| 桌面宠物序列帧动画（24fps） | `Services/PetSkin.cs` + `PetManager.cs` | 相同素材目录规范，降采样解码 + 循环衔接点 |
| 行为 AI（idle/walk/sleep/yawn/happy/hurt） | `PetManager.DecideNextBehavior()` | 心情差时更容易睡觉/委屈 |
| 单击戳、双击回巢/出巢、拖拽拎起 | `Windows/PetWindow.xaml.cs` | 时间戳单击/双击判定，与拖拽统一手势 |
| 回巢（保留原版逻辑） | `PetManager.GoHome()` | 宠物走向主屏右下角托盘溢出菜单 → 到达后淡出消失 |
| 出巢（保留原版逻辑） | `PetManager.LeaveHome()` | 从托盘角落以掉落动画（fall）出场，落地后自由活动 |
| 回巢节能（卸载活动帧） | `PetSkin.TrimToHomeOnly()` | 回巢只保留 home/fall |
| 心情/清洁度属性 + 衰减 | `PetManager.StatTick()` | 60 秒衰减 |
| 音乐联动跳舞 | `PetManager` 音乐分支 | 需提供 music 素材 |
| 动作音效 | `Services/AudioService.cs` | `<动作>/sound.mp3|wav|m4a` |
| 切换宠物形象/模型 | `PetManager.SwitchModel()` | 内置猫/狗/兔/熊猫，托盘或设置一键切换 |
| AI 对话 + 切换大模型 | `AIChatService.cs` + `ChatWindow` | DeepSeek/OpenAI/自定义（OpenAI 兼容） |
| 导入自定义皮肤/模型 | `Services/SkinImporter.cs` | 从文件夹或 zip 导入，加入模型列表 |
| 菜单栏图标 | 系统托盘 `NotifyIcon` | 设置/对话/放宠物/回巢/换模型/导入/重启/退出 |
| 设置面板 | `Windows/SettingsWindow.xaml` | Pet / AI / Behavior |

## 与原版的差异（Windows 平台限制）

- **无灵动岛**：原版顶部刘海动态岛已整体移除（连同其中的音乐面板、日历、文件暂存架、电池、镜子等嵌入 UI）。
- **回巢地点**：宠物不再回顶部刘海，而是走向**主屏右下角的系统托盘溢出菜单**区域（Windows 的"托盘"即原版"菜单栏图标"的生活位置）。回巢/出巢动画逻辑（走回 + 淡出 / 掉落出场）沿用原版。
- **媒体控制**：移除了岛内音乐控制 UI，但"宠物随音乐跳舞"仍保留（读系统 SMTC 播放状态）。
- **首次引导**：原版多步引导简化为欢迎音 + 直接使用。

## 新机子上怎么用（不含单文件方案）

> 本方案用**框架依赖发布**（体积小，约 25MB），新机器无需安装 .NET SDK，只需要 **.NET 8 Desktop Runtime**。

### 角色 A：只是"拿来用"的最终用户（不需要装 SDK）

**第 1 步：准备 .NET 运行时（只需一次）**

打开 PowerShell，粘贴执行：
```powershell
winget install Microsoft.DotNet.DesktopRuntime.8
```
> 如果没有 winget，可到 <https://dotnet.microsoft.com/download/dotnet/8.0> 下载 **Windows x64 Desktop Runtime** 安装包手动安装。

**第 2 步：拿到应用包并运行**

发布者会把 `DeskPet.Windows/publish/` 目录压缩成一个 zip 发给你。你只需：
1. 解压 zip；
2. 双击里面的 **`DeskPet.exe`**。

至此宠物就出现在桌面上了。**无需任何额外配置**——首次启动会自动生成 4 套内置皮肤（猫/狗/兔/熊猫）到你的 `图片/BoringPet/`。

### 角色 B：在新电脑上从源码构建发布（发布者）

**第 1 步：装 .NET 8 SDK**

```powershell
winget install Microsoft.DotNet.SDK.8
```

**第 2 步：拿到源码文件夹**

把 `DeskPet.Windows/` 整个文件夹拷贝到新电脑任意位置（该文件夹自包含，无外部依赖）。

**第 3 步：构建发布**

```powershell
cd DeskPet.Windows
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

**第 4 步：分发 `publish/` 目录**

把生成的 `publish/` 文件夹压缩发给用户，用户按"角色 A"操作即可（下载用户需装 .NET 8 Desktop Runtime）。

### 常见问题（新机子提示缺运行时）

启动 `DeskPet.exe` 时若提示"缺少 .NET / framework"，说明机子上没有对应运行时，按下面任一方式解决：
- `winget install Microsoft.DotNet.DesktopRuntime.8`
- 或访问 <https://dotnet.microsoft.com/download/dotnet/8.0> 手动装 **x64 Desktop Runtime（8.x）**

## 构建 / 发布（开发者参考）

前置：**.NET 8 SDK**（含 Windows Desktop 运行时）。

### 方式一：直接运行源码目录（推荐，体积最小）
打开该文件夹用 IDE（如 VS/Rider）打开 `DeskPet.Windows/DeskPet.csproj`，F5 运行即可，或命令行：
```powershell
cd DeskPet.Windows
dotnet build -c Release
```

### 方式二：发布整批文件（框架依赖，约 25MB）★ 发布用
目标机需装有 **.NET 8 Desktop Runtime**：
```powershell
cd DeskPet.Windows
dotnet publish -c Release -r win-x64 --self-contained false -o publish
# 产物：publish\ 文件夹（DeskPet.exe + WinRT 依赖，约 25MB）
```

> 自包含单文件 exe（约 160MB、无需装 .NET）属于另一套发布方案，这里不作展开；有需要可自行用 `--self-contained true` + `PublishSingleFile=true` 生成。

## AI 对话配置

设置 → **AI** 标签页：

- **Provider**：DeepSeek / OpenAI / Custom（均为 OpenAI 兼容接口，切换时自动填入默认模型和地址）
- **API Key**：填入对应服务的密钥（DeepSeek 在 platform.deepseek.com，OpenAI 在 platform.openai.com）
- **Model / Base URL**：默认 `deepseek-chat`、`gpt-4o-mini`，自定义服务可自行填写

对话入口：托盘菜单 **Chat with pet**，或单击宠物 → 操作面板 → **Chat**。

## 切换宠物形象

- 托盘菜单 → **Pet model** → 选择 cat / dog / rabbit / panda
- 或 设置 → **Pet** → **Pet model**

内置模型生成在 `图片/BoringPet/builtin/<模型名>/`，可继续用 `PET.md` 规范自定义任意序列帧皮肤。

## 导入自定义皮肤/模型

- 托盘菜单 → **Pet model** → **Import skin from folder… / from zip…**
- 或 设置 → **Pet** → **Import skin (folder) / (zip)**

选择含动作子文件夹（`idle_0/`、`walk_0/`…）的文件夹或 zip 包即可，导入后自动加入模型列表并立即应用。导入的模型保存在 `图片/BoringPet/imported/<模型名>/`。

> 说明：原仓库（源码与 Release dmg）**没有**内置宠物皮肤序列帧素材，只有图标、欢迎音和 `tools/frames_from_video.sh` 抽帧工具。皮肤需自行准备（如 seeDance 等 AI 生成视频后抽帧，或其它序列帧素材），再通过导入功能加入。

## 宠物素材

素材规范与 macOS 版一致（见源仓库根目录 `PET.md`）：默认目录为 `图片/BoringPet/`，每个动作一个子文件夹（编号 PNG 序列帧），可选 `config.json`。首次启动会自动生成四套内置宠物模型（猫/狗/兔/熊猫）到 `图片/BoringPet/builtin/`，也可继续替换为任意序列帧皮肤。

## 项目结构

```
DeskPet.Windows/
├── DeskPet.csproj        项目文件（.NET 8 WPF）
├── app.manifest          DPI / 兼容清单
├── App.xaml(.cs)         应用入口、系统托盘
├── Models/               Enums、AppSettings（设置持久化）
├── Services/             宠物/媒体/音效/AI/皮肤生成与导入
├── Windows/              PetWindow、SettingsWindow、ChatWindow
└── publish/              框架依赖发布输出（可选）
```
