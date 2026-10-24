# 第三方与参考说明

RimeNotice 自有源码采用 GPL-3.0-only，见 [LICENSE](LICENSE)。第三方代码、依赖、标识和素材保留各自版权与许可。

- Windows App SDK / WinUI：Microsoft，依赖通过 NuGet 获取，遵循其附带许可。
- .NET：Microsoft 与贡献者，运行依赖按其分发条款打包。
- RimePPT：MYXMJY，tools/Build-Portable.ps1、tools/Build-MSIX.ps1 及隔离更新回滚测试由该项目移植后适配。原许可全文保留于 tools/LICENSE-RimePPT。
- WinUI Gallery：Microsoft，设计与动画参考，不在仓库内复制整个参考项目。
- Edge 兼容协议参考 rany2/edge-tts；RN 未安装或打包其 Python 库。
- QQ 图标用于标识通知来源，相关标识属于其权利人；RN 为独立工具。

参考项目与离线模型试验不包含在本仓库及发布包中。

- 默认提示音：普通提醒与呼叫音来自 Windows Media（Windows Unlock.wav、Alarm09.wav）；重要提醒 startup.mp3 由项目作者提供。相关音频不声明为 RN 原创或开源许可资产。

中文语音下载目录参考 NaturalVoiceSAPIAdapter Wiki，MSIX 原始文件托管于 NVDA 中文站。RN 不打包或安装 NaturalVoiceSAPIAdapter。
