# Speed Tournament

Unity 制作的横屏单机小球竞速游戏。六条车道、六个角色、两张固定赛道，支持高低差、分岔路线、AI 技能对抗与本地最好成绩。

## 运行项目

1. 用 Unity Hub 安装 **Unity 6000.6.0f1**。
2. 手机打包需要 Android Build Support，以及对应的 SDK、NDK、OpenJDK 模块。
3. 打开项目，等待 Packages 依赖和资源导入完成。
4. 打开 `Assets/Scenes/SampleScene.unity`，点击 Play。

编辑器 MCP 包固定在 `v10.0.0`，仅用于开发；正常游玩不需要启动 MCP 服务。

## 操作与玩法

- 手机：左侧按钮相邻切轨（可长按连续切换）；右侧按钮释放角色技能、氮气。
- 电脑：A / D 或方向键切轨，空格氮气，左 Ctrl 角色技能，Esc 暂停。
- 起步喷射：GO 前 0.35 秒至后 0.20 秒点击氮气，免费提速 35%、持续 1.2 秒，每局一次。
- 技能围绕能量设计，没有冷却。六角色：疾风、重盾、脉冲、引力、幻影、蓄能。
- 选角页的“玩法说明”包含操作、技能、赛道道具及参考数据。

## Android 安装包

目标配置：ARM64 / IL2CPP，最低 Android 8.0（API 26），横屏，包名 `com.fuqing.speedtournament`，版本 `0.1.0`。

本地 APK 输出位置：`Builds/Android/SpeedTournament-0.1.0-arm64.apk`。

重复打包可用菜单 **Speed Tournament → Build Android APK**，或 Unity 命令行方法 `SpeedTournament.Editor.AndroidBuild.BuildApk`。构建脚本会输出 `Builds/Android/build-summary.json`。

当前交付为侧载测试包，使用本机默认测试签名，不是商店正式上架包。正式发布需要另外设置持久的发布签名与审核配置。签名密钥、安装包及符号文件不进入源码历史。

## 目录

- `Assets/SpeedTournament/Runtime`：赛道、角色、AI、道具、UI、音频及起步逻辑。
- `Assets/SpeedTournament/Resources`：运行时 UI、材质、驾驶手册和音频配置。
- `Assets/Audio/Prepared`：项目实际使用的背景音乐。
- `Assets/SpeedTournament/Editor`：入口、起步、音频与完整比赛回归工具（Play 模式中运行）。
- `Docs`：设计、变更和验证说明。

`Library`、`Temp`、`Builds`、`LocalTools`、本机 IDE 状态和 `SourceAudio` 原始视频备份不纳入仓库；构建所需的音频与 `.meta` 均在 `Assets` 中。

## 验证边界

项目已执行编辑器里的入口、起步、音频与完整比赛模拟测试。编辑器测试和 APK 签名检查不能替代手机上的实际启动、触控、声音及性能测试。

本项目不是官方游戏；仓库中的第三方音乐及其他素材不因源码上传而自动获得再发行许可。未擅自为用户代码添加开源许可证。
