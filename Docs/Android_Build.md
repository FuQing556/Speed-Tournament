# Android 构建与安装

## 本次构建结果（2026-09-07）

- 最终构建成功：0 错误、3 警告，约 126 秒。
- APK 大小：92,676,610 字节（约 88.4 MiB）。调试符号另存，不计入 APK 大小。
- Android APK Signature Scheme v2 校验通过；清单确认包名、ARM64、最低 API 26、目标 API 36。
- 发布裁剪结果确认保留 SphereCollider、CapsuleCollider 和 BoxCollider。
- SHA-256：`2243185C946751BAC5074EF7D78A7698162F54871E032C70D4B8F68656057550`。
- 当前没有连接手机，尚未进行 APK 真机安装和运行测试。
- 完整摘要保存在本地 `Builds/Android/build-summary.json`。

## 环境与配置

- Unity 6000.6.0f1 + 对应版本 Android Build Support、SDK、NDK、OpenJDK。
- 架构 ARM64，IL2CPP，非 Development 构建。
- 包名 `com.fuqing.speedtournament`，版本 `0.1.0`，versionCode `1`。
- 最低 Android API 26（Android 8.0），本次安装的 SDK 编译/目标版本为 API 36。
- 横屏；使用本机默认 Android Debug 证书侧载测试，不用于商店正式发布。

## 重新打包

在 Unity 中使用菜单 `Speed Tournament → Build Android APK`。脚本位置：
`Assets/SpeedTournament/Editor/AndroidBuild.cs`。

也可在未打开同一项目的 Unity 编辑器时，通过 Unity 命令行执行：

```powershell
& '<Unity.exe 的完整路径>' -batchmode -quit -projectPath '<项目完整路径>' -executeMethod SpeedTournament.Editor.AndroidBuild.BuildApk -logFile '<构建日志完整路径>'
```

输出到 `Builds/Android/`，安装包、调试符号与构建报告均被 Git 忽略。

## 本机工具链路径兼容

本机第一次原生编译出现 NDK 头文件 `size_t` / `NULL` 错误。使用项目内的 ASCII 路径联接指向原 Android 工具链后，原生编译通过；没有移动、删除或修改 Unity 安装目录。

本机已建立 `LocalTools/Android` 目录联接，目标是此 Unity 版本的 `Editor/Data/PlaybackEngines/AndroidPlayer`。它不是被复制到项目里的 SDK，不能提交到 Git。构建菜单仅在联接存在时临时使用它，并在结束后恢复原 SDK/NDK/JDK 偏好路径。

其他电脑若 Unity 安装在普通英文路径，一般不需要该联接。若同样遇到工具链路径问题，可以在项目根目录创建 `LocalTools`，用 PowerShell `New-Item -ItemType Junction` 将其 `Android` 子路径指向本机对应版本的 AndroidPlayer 目录；不要指向其他版本或删除原安装目录。

## 发布裁剪

场景只保存启动组件，游戏小球和大部分环境在运行时创建。因此 `Assets/SpeedTournament/link.xml` 显式保留 CreatePrimitive 所需的 MeshFilter、MeshRenderer、BoxCollider、SphereCollider、CapsuleCollider，防止 IL2CPP/引擎裁剪导致模型创建失败。并未为装饰额外添加活动碰撞体。

## 安装与验证

将 APK 复制到 ARM64 Android 8.0 及以上手机，允许当前文件管理器安装未知来源应用后安装；或在授权 USB 调试的设备上运行：

```powershell
adb install -r 'Builds/Android/SpeedTournament-0.1.0-arm64.apk'
```

没有连接设备时，构建机器只能检查 APK、签名和清单，不能证明真机帧率与触控体验。手机上建议检查：启动选角、两张图、GO 起步喷射、技能音效与 BGM、暂停恢复、完赛成绩和再次启动后的记录。

测试签名和正式发布签名不同。更换签名不能直接覆盖旧包；不要随意卸载旧版本，以免丢失本机记录。正式发布前另外设置并安全备份长期签名密钥，不要提交到仓库。
