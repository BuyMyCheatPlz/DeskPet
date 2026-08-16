# DeskPet for Windows

将 macOS 版 **DeskPet**（桌面宠物 + boring.notch 动态岛）移植到 Windows 的原生应用，使用 **C# / WPF（.NET 8）** 实现。已移除原版顶部的"灵动岛"，改为一个**圆形悬浮窗**：宠物回巢时自动走向悬浮窗，到达后淡出消失；单击悬浮窗展开**深色风格的功能菜单**。

> **本项目自包含于 `DeskPet.Windows/` 一个文件夹内**，没有引用 macOS 工程的任何外部文件（无 `PET.md`、无 `deskpet/`、无 `BoringNotchXPCHelper` 依赖；宠物皮肤在运行时从 `图片/BoringPet/` 动态生成/导入）。因此**发布时只需分发 `DeskPet.Windows` 这个文件夹**即可。

## 功能对照

| macOS 原版功能 | Windows 实现 | 说明 |
|---|---|---|
| 桌面宠物序列帧动画（24fps） | `Services/PetSkin.cs` + `PetManager.cs` | 相同素材目录规范，降采样解码 + 循环衔接点 |
| 行为 AI（idle/walk/sleep/yawn/happy/hurt） | `PetManager.DecideNextBehavior()` | 心情差时更容易睡觉/委屈 |
| 单击戳、双击回巢/出巢、拖拽拎起 | `Windows/PetWindow.xaml.cs` | 单击=互动并弹面板，双击=回巢/出巢，拖拽=拎起 |
| 回巢（保留原版逻辑） | `PetManager.GoHome()` | 宠物走向悬浮窗当前位置 → 到达后淡出消失 |
| 出巢（保留原版逻辑） | `PetManager.LeaveHome()` | 从悬浮窗位置以掉落动画（fall）出场，落地后自由活动 |
| 回巢节能（卸载活动帧） | `PetSkin.TrimToHomeOnly()` | 回巢只保留 home/fall |
| 心情/清洁度属性 + 衰减 | `PetManager.StatTick()` | 60 秒衰减 |
| 音乐联动跳舞 | `PetManager` 音乐分支 | 需提供 music 素材 |
| 动作音效 | `Services/AudioService.cs` | `<动作>/sound.mp3|wav|m4a` |
| 切换宠物形象/模型 | `PetManager.SwitchModel()` | 内置猫/狗/兔/熊猫 + deepseek 娘，托盘或设置一键切换 |
| 调节宠物大小 | 设置 → 宠物 → 宠物大小（缩放） | 拖动实时预览（0.3×~2.0×），保存后持久化 |
| 动画连贯性 | `PetSkin` 缓存策略 + 并行解码 | 循环动作缓存常驻，切换动作不再重新解码→画面平滑 |
| 设置界面语言 | `SettingsWindow.xaml` | 简体中文 |
| AI 对话 + 切换大模型 | `AIChatService.cs` + `ChatWindow` | DeepSeek/OpenAI/自定义（OpenAI 兼容） |
| 导入自定义皮肤/模型 | `Services/SkinImporter.cs` | 从文件夹或 zip 导入，加入模型列表 |
| 菜单栏图标 | 系统托盘 `NotifyIcon` | 中文菜单：打开设置 / 和宠物对话 / 放宠物 / 回巢 / 宠物形象 / 导入 / 重启 / 退出 |
| 悬浮窗 | `Windows/FloatWindow.xaml` | 圆形可拖动，点击展开深色风格二级菜单集中所有功能 |
| 开机自启动 | `Services/AutoStart.cs` | 悬浮窗菜单或设置可开关 |
| 设置面板 | `Windows/SettingsWindow.xaml` | 宠物 / AI 对话 / 行为 / 动作 四个标签页，每个设置项均带中文说明 |
| 戳一戳 vs 抚摸 | `PetManager.Poke()` / `Pet()` | 戳一戳播 `happy` 变体 0 且心情 -1（心情差则委屈），抚摸播 `happy` 变体 1 且心情 +12 |

## 与原版的差异（Windows 平台限制）

- **无灵动岛**：原版顶部刘海动态岛已整体移除，改为**圆形悬浮窗**（`Windows/FloatWindow`），可拖动、点击展开二级菜单。
- **回巢地点**：宠物回巢走向**悬浮窗当前所在位置**（悬浮窗可拖动，宠物跟着走到新位置），到达后淡出；出巢从悬浮窗位置掉落出场。动画逻辑（走回 + 淡出 / 掉落出场）沿用原版。
- **媒体控制**：移除了岛内音乐控制 UI，但"宠物随音乐跳舞"仍保留（读系统 SMTC 播放状态）。
- **首次引导**：原版多步引导简化为欢迎音 + 直接使用。

## 悬浮窗（替代灵动岛）

一个**圆形、可拖动**的悬浮窗，始终置顶显示宠物当前形象的头像。点击它弹出**二级菜单**，集中所有功能：

- **回巢 / 出巢**（按当前状态自动显示）
- **音乐控制**（播放/暂停、上一首、下一首、当前曲目）
- **音量**（＋/－/静音切换）
- **电池状态**（电量% / 充电中）
- **宠物互动**（戳一戳 / 抚摸，见下方说明）
- **AI 对话**、**设置**
- **切换宠物形象**（子菜单列出全部模型）
- **宠物大小**（小/中/大）
- **开机自启动**（开关）
- **宠物鼠标穿透**（开关）
- **重启 DeskPet / 退出**

菜单整体采用**深色风格**（深色圆角面板 + 白色半透明描边 + 投影），与悬浮窗外观一致；二级/三级子菜单自动套用同一套样式，勾选项显示蓝色「✓」标记。

### 戳一戳 vs 抚摸

- **戳一戳**（单击宠物，或菜单「戳一戳宠物」）：心情好时播放 `happy` 变体 0（开心地蹦一下）并使心情 **-1**；心情差（<30）时播放 `hurt`（委屈）并使心情 -1。
- **抚摸**（面板「抚摸」按钮，或菜单「抚摸宠物」）：播放 `happy` 变体 1（温柔享受）并使心情 **+12**。

两者现在动画和心情效果都不同；对只有单个 happy 变体的皮肤会自动回退到唯一变体，但心情增减仍区分。

原来灵动岛里的各项功能（回巢、出巢、宠物面板、设置、模型切换、导入皮肤、AI 对话等）以及音乐/音量/电池系统信息，都集中在这个悬浮窗菜单里。悬浮窗可拖动到屏幕任意位置。

## 宠物属性（心情 / 清洁度）

宠物有两个隐藏属性，范围 **0~100**，初始均为 **100**：

- **心情（Happiness）**：影响表情和行为——心情低于 25 进入"悲伤"状态（更容易睡觉/委屈）；低于 30 时被戳会表现委屈。
- **清洁度（Cleanliness）**：卫生值，目前仅随时间衰减展示，暂不参与行为判断。

### 随时间变化（每 60 秒结算一次）

- **清洁度**：每 60 秒下降 `0.8 × 属性衰减速度`（默认速度 0.5 → 每次 -0.4），只降不升，最低 0。
- **心情**：每 60 秒向 50 收敛：`心情 += (50 - 心情) × 0.04`。高于 50 时缓慢回落、低于 50 时缓慢回升，因此不会无限掉到 0。

`属性衰减速度` 可在 **设置 → 宠物 → 属性衰减速度**（0.1~2.0）调节。

### 互动对心情的影响

- **戳一戳**：心情 **-1**（封底 0）。
- **抚摸**：心情 **+12**（封顶 100）。

### 心情对行为的影响

- **心情 < 25（悲伤）**：每次决定动作走固定分支——约 30% 睡觉、20% 委屈（hurt）、20% 走路、其余待机。
- **心情 ≥ 25**：按设置「动作」页的走路/睡觉/小动作/待机四档**加权概率**随机决定。
- **心情 < 30 且被戳**：播放委屈（hurt）表情而非开心。

## 设置界面说明

设置窗口（`Windows/SettingsWindow.xaml`）分四个标签页，**每个设置项下方都有灰色小字说明**其含义：

- **宠物**：启用桌面宠物、宠物形象（模型）、导入皮肤、宠物大小（0.3×~2.0×）、活动频率（多少秒决定一次下一步动作，越小越活跃）、属性衰减速度（心情/清洁度下降速度）、播放音乐时跳舞、动作音效音量、皮肤目录。
- **AI 对话**：服务商（DeepSeek / OpenAI / 自定义）、API 密钥、模型、接口地址（Base URL）。
- **行为**：触觉反馈、开机自启动、宠物鼠标穿透、悬浮窗大小（0.5×~2.5×）、悬浮窗透明度（0.2~1.0）。
- **动作**：走路/睡觉/小动作/待机的自动触发概率（**加权随机，按比例分配，无需加起来等于 100%，某项设为 0 则永不触发**），以及各动作的播放速度倍率（1.0 = 原速）。

## 开机自启动

- 悬浮窗菜单 → **开机自启动**，或 设置 → **行为** → **开机自启动**。
- 通过注册表 `HKCU\...\CurrentVersion\Run` 实现（写入当前 exe 路径），无需管理员权限。

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

### 方式二：发布整批文件（框架依赖，约 210MB，含内置 deepseek 皮肤）★ 发布用
目标机需装有 **.NET 8 Desktop Runtime**：
```powershell
cd DeskPet.Windows
dotnet publish -c Release -r win-x64 --self-contained false -o publish
# 产物：publish\ 文件夹（DeskPet.exe + WinRT 依赖 + Skins\，首次启动自动同步皮肤）
```
> `Skins/` 里的皮肤素材会随发布复制到 `publish/Skins/`，应用首次启动时自动把它们同步到用户 `图片/BoringPet/imported/`。

> 自包含单文件 exe 属于另一套发布方案（不建议，素材体积大），这里不作展开。

## 内置 / 打包皮肤模型

| 模型名 | 来源 | 说明 |
|---|---|---|
| cat / dog / rabbit / panda | 内置程序绘制 | 首次启动自动生成到 `图片/BoringPet/builtin/` |
| **deepseek-girl** | 原 GitHub 仓库 `deskpet/BoringPet` | 作者提供的"deepseek 娘"序列帧皮肤（14 动作 / 1778 帧 / 24fps） |

- **deepseek-girl** 素材随发布打包在 `Skins/deepseek-girl/`，应用首次启动自动同步到 `图片/BoringPet/imported/deepseek-girl/`，随后即可在托盘菜单「宠物形象」或设置「宠物 → 宠物形象」中切换。
- 如果你想省去发布体积，可删除工程里的 `Skins/` 目录（应用其它内置模型仍可用）。

## AI 对话配置

设置 → **AI 对话** 标签页：

- **服务商**：DeepSeek / OpenAI / 自定义（均为 OpenAI 兼容接口，切换时自动填入默认模型和地址）
- **API 密钥**：填入对应服务的密钥（DeepSeek 在 platform.deepseek.com，OpenAI 在 platform.openai.com）
- **模型 / 接口地址**：默认 `deepseek-chat`、`gpt-4o-mini`，自定义服务可自行填写

对话入口：托盘菜单 **和宠物对话**，或单击宠物 → 操作面板 → **对话**。

## 切换宠物形象

- 托盘菜单 → **宠物形象** → 选择 cat / dog / rabbit / panda / deepseek-girl
- 或 设置 → **宠物** → **宠物形象（模型）**

内置模型生成在 `图片/BoringPet/builtin/<模型名>/`，可继续用 `PET.md` 规范自定义任意序列帧皮肤。

## 导入自定义皮肤/模型

- 托盘菜单 → **宠物形象** → **从文件夹导入皮肤… / 从压缩包导入皮肤…**
- 或 设置 → **宠物** → **从文件夹导入皮肤… / 从压缩包导入皮肤…**

选择含动作子文件夹（`idle_0/`、`walk_0/`…）的文件夹或 zip 包即可，导入后自动加入模型列表并立即应用。导入的模型保存在 `图片/BoringPet/imported/<模型名>/`。

> 说明：原 GitHub 仓库在 `deskpet/BoringPet/` 下提供了作者自带的 **deepseek 娘** 序列帧皮肤，本工程已把它打包为内置模型（见上方"内置/打包皮肤模型"）。用户也可自行用 `tools/frames_from_video.sh`（源仓库）把 AI 生成的视频抽帧后做成皮肤导入。

## 宠物素材

素材规范与 macOS 版一致（见源仓库 `PET.md`）：默认目录为 `图片/BoringPet/`，每个动作一个子文件夹（编号 PNG 序列帧），可选 `config.json`。首次启动自动生成四套内置模型（猫/狗/兔/熊猫）到 `builtin/`，并把随包携带的 `Skins/deepseek-girl/` 同步到 `imported/`。

## 项目结构

```
DeskPet.Windows/
├── DeskPet.csproj        项目文件（.NET 8 WPF）
├── app.manifest          DPI / 兼容清单
├── App.xaml(.cs)         应用入口、系统托盘、悬浮窗
├── Models/               Enums、AppSettings（设置持久化）
├── Services/             宠物/媒体/音效/AI/皮肤生成与导入/开机自启
├── Windows/              PetWindow、SettingsWindow、ChatWindow、FloatWindow
├── Skins/                内置皮肤包（deepseek-girl，随发布打包并自动同步）
└── publish/              框架依赖发布输出（可选）
```
