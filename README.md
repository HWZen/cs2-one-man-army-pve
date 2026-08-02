# CS2-One-Man-Army-PVE / CS2-一人成军PVE

English and Chinese bilingual README for the standalone CounterStrikeSharp plugin project.

独立 CounterStrikeSharp 插件项目的中英双语说明文档。

## Overview / 项目简介

**English**

`CS2-One-Man-Army-PVE` is a standalone CounterStrikeSharp plugin for a competitive PvE mode in CS2:

- 1 human player vs 10 bots
- Human: 1000 HP, 400 armor, helmet enabled
- Bots: 100 HP
- Competitive rules with halftime side switch, first to 13, overtime enabled
- `oma` also enables `sv_infinite_ammo 2`

**中文**

`CS2-一人成军PVE` 是一个独立的 CounterStrikeSharp 插件，用于在 CS2 中实现竞技规则下的 PVE 模式：

- 1 名真人对战 10 名人机
- 真人：1000 血、400 护甲、自动有头甲
- 人机：100 血
- 竞技模式规则：12 局后换边、先到 13 胜、允许加时
- 执行 `oma` 时会同时开启 `sv_infinite_ammo 2`

## Quick Start for Players / 普通用户使用教程

**English**

1. Install [CS2](https://store.steampowered.com/app/730/CounterStrike_2/) via Steam.
2. Download the deploy tool executable from the [Releases](https://github.com/anomalyco/cs2-one-man-army-pve/releases) page.
3. Run the deploy tool.
4. Click `安装` (Install) to deploy the plugin.
5. Click `启动 CS2` (Launch CS2) to start the game.
6. In the game, start a casual/competitive match against bots. During the team selection phase, open the console and type `oma t` or `oma ct` to enable the mode.

**中文**

1. 在 Steam 上下载好 [CS2](https://store.steampowered.com/app/730/CounterStrike_2/)。
2. 从本项目的 [Releases 发布页](https://github.com/anomalyco/cs2-one-man-army-pve/releases) 下载本项目可执行程序。
3. 启动部署工具。
4. 点击 `安装` 部署插件。
5. 点击 `启动 CS2` 启动游戏。
6. 进入游戏后，开始一局人机竞技，并在选边环节，打开控制台，输入 `oma t` 或 `oma ct` 开启模式。

## Enhanced Bots (CS2-Bot-Improver) / 增强机器人（CS2-Bot-Improver）

**English**

This project integrates the [CS2-Bot-Improver](https://github.com/ed0ard/CS2-Bot-Improver) enhancement project:

- Uses CS2-Bot-Improver's **enhanced bots** (improved `botprofile.vpk` overrides) so the 10 bots play smarter and more realistically.
- Uses CS2-Bot-Improver's **`gameinfo.gi`** config file in the CS2 `csgo` folder. It registers the `csgo/overrides/botprofile.vpk` and `csgo/addons/metamod` search paths, which is required for the enhanced bots to load and for CounterStrikeSharp plugins (including this one) to be usable.

**中文**

本项目集成了 [CS2-Bot-Improver](https://github.com/ed0ard/CS2-Bot-Improver) 增强项目：

- 使用 CS2-Bot-Improver 的**增强机器人**（改进的 `botprofile.vpk` 覆盖文件），让 10 个人机玩得更聪明、更真实。
- 使用 CS2-Bot-Improver 提供的 **`gameinfo.gi`** 配置文件，放置于 CS2 的 `csgo` 目录。该文件注册了 `csgo/overrides/botprofile.vpk` 和 `csgo/addons/metamod` 搜索路径，这是增强机器人能够加载、以及 CounterStrikeSharp 插件（包括本插件）能够使用的前提。

## Project Structure / 项目结构

- `OneManArmyPve/OneManArmyPve.cs` - plugin source / 插件源码
- `OneManArmyPve/OneManArmyPve.csproj` - .NET 10 plugin project / .NET 10 插件工程
- `DeployTool/Program.cs` - deploy helper (auto find CS2, copy DLL, optional launch) / 部署工具（自动找 CS2、复制 DLL、可选启动）
- `DeployTool/DeployTool.csproj` - deploy tool project / 部署工具工程

## Build / 编译

```powershell
dotnet build "OneManArmyPve/OneManArmyPve.csproj"
```

## Deploy / 部署

**English**

Copy `OneManArmyPve.dll` to your CS2 CounterStrikeSharp plugins directory, then restart the server or reload plugins.

**中文**

将 `OneManArmyPve.dll` 复制到你的 CS2 CounterStrikeSharp 插件目录，然后重启服务器或重载插件。

## Deploy Tool / 部署工具

**English**

Build deploy tool:

```powershell
dotnet build "DeployTool/DeployTool.csproj"
```

Then put the prebuilt `OneManArmyPve.dll` in the same folder as `DeployTool.exe` and run the GUI.

The GUI has only two buttons:

- `安装` (Install): detect CS2 path and copy DLL
- `启动 CS2（-insecure）` (Launch): start CS2 with `-insecure`

Output exe (Debug):

```powershell
DeployTool/bin/Debug/net10.0-windows/DeployTool.exe
```

**中文**

先编译部署工具：

```powershell
dotnet build "DeployTool/DeployTool.csproj"
```

然后把你预编译好的 `OneManArmyPve.dll` 放到 `DeployTool.exe` 同目录，双击启动图形界面。

界面只有两个按钮：

- `安装`：自动定位 CS2 并复制 DLL
- `启动 CS2（-insecure）`：用 `-insecure` 参数启动 CS2

Debug 版 exe 路径：

```powershell
DeployTool/bin/Debug/net10.0-windows/DeployTool.exe
```

## Commands / 命令

- `oma [t|ct]` - enable mode and optionally set initial side / 开启模式并可选设置初始阵营
- `oma_disable` - disable mode / 关闭模式
- `oma_status` - show current mode status / 查看当前模式状态
