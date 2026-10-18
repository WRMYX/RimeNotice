<div align="center">
  <img src="RimeNotice/Assets/RimeNotice-Logo.svg" width="88" height="88" alt="RimeNotice Logo" />
  <h1>RimeNotice</h1>
  <p><strong>让课堂里的重要消息，及时被看见。</strong></p>
  <p>面向 QQ 消息与教学触摸屏的 Windows 通知工具 · 原生 WinUI 3</p>
  <p><a href="https://github.com/WRMYX/RimeNotice/releases">下载发布版</a> · <a href="https://github.com/WRMYX/RimeNotice/issues">反馈问题</a></p>
  <p><strong>WinUI 3</strong> / <strong>.NET 10</strong> / <strong>Windows App SDK</strong></p>
</div>

老师们在群里发送消息，课堂电脑右下角的系统通知却很容易被忽略。RimeNotice（RN）接收 QQ 的 Windows 通知，将需要注意的内容显示为独立通知卡片，并提供重要消息、呼叫提醒、提示音和正文朗读。

[功能介绍](#功能介绍) · [安装与上手](#安装与上手) · [软件更新](#软件更新) · [源码构建](#源码构建) · [自动打包与发布](#自动打包与发布) · [反馈与贡献](#反馈与贡献)

## 功能介绍

### 接收与提醒

- 仅接收 QQ：系统通知默认采用事件唤醒，支持定时扫描及独立 UI Automation 兼容模式。
- 按联系人、关键词及 @ 提醒识别重要消息，支持黑名单、白名单和重复消息冷却。
- 呼叫提醒可持续到确认，支持循环铃声和静音。
- 关闭设置窗口后继续在托盘运行；托盘菜单提供打开设置和退出。
- 支持开机启动；通知默认置顶，可单独关闭。

### 通知显示

- 独立的原生 WinUI 通知窗口，按内容收紧，达到设定上限后纵向滚动。
- 发送人与正文分别设置字体，支持导入 TTF、OTF；短名字不会因为类型栏挤压而强制滚动。
- 支持浅色、深色、Mica、Mica Alt、亚克力与纯色；效果不可用时回退纯色。
- 消息类型与倒计时、QQ 来源标识、MYXMJY 标识，以及可选背景遮罩。
- 鼠标、触摸与键盘均可操作；系统关闭动画时不强制播放动效。

### 声音与语音

- 提示音可自定义，支持 Windows 本机语音与可选 Edge 在线语音。
- 仅朗读正文，不朗读发送人。长正文拆分请求，不静默丢弃尾部。
- 在线语音默认关闭，启用后才向选定服务发送待朗读文本；可设置超时和失败回退。
- Edge 使用可选兼容协议，没有长期可用性保证；CosyVoice 和 Kokoro 未接入。

## 安装与上手

建议使用 x64 Windows 11，或 Windows 10 2004 / 19041 及以上版本。材质显示取决于 Windows 与系统效果设置。

发布文件位于 [GitHub Releases](https://github.com/WRMYX/RimeNotice/releases)。若页面尚无正式版本，请等待发布；仓库公开不代表已有安装包。Microsoft Store 版本尚未公开发行。

- **MSIX＋公开证书**：信任随包 `.cer` 后通过 Windows 安装 MSIX。测试签名包不是 Microsoft Store 版本，程序不会自动导入证书。
- **便携版**：完整解压 Portable ZIP 后运行 `RimeNotice.exe`，保留全部依赖和资源。系统通知访问需要包身份；未安装的便携版使用 UIA 兼容来源时存在识别限制。

### 第一次使用

1. 登录 QQ，允许 QQ 产生 Windows 通知。
2. 安装并启动 RN，授予通知访问权限；获得权限后自动开始接收。
3. 如只想显示 RN：在 Windows 的 QQ 通知设置中保留通知中心，只关闭横幅及声音，不要关闭 QQ 的通知总开关。
4. 在“提醒规则”配置联系人、关键词和呼叫词。
5. 在“通知外观”“声音与语音”中调整显示和声音。关闭设置后仍会继续接收；完全退出请使用托盘菜单。

## 数据与限制

- RN 不修改 QQ 文件、不注入 QQ、不读取聊天数据库，也不保存消息历史。需回看消息时请打开 QQ。
- Windows 通知接口只能提供 QQ 实际投递的通知文本。若 QQ 只提供摘要，RN 无法从该接口还原聊天全文。
- 图片与附件只有在通知中的文件名或路径能够唯一匹配时展示，不把最近修改的图片当作当前会话图片。无法确认时提示去 QQ 查看。
- UIA 依赖 Windows 暴露的可访问通知元素，不保证每个 QQ/Windows 组合都可用。
- 诊断记录用于权限、错误及布局状态，不记录完整聊天正文；在线朗读则会把待朗读文本发送至服务商。

## 软件更新

- GitHub 渠道检查正式 Release，草稿和预发布不作为更新来源。
- 支持官方直连、预设加速线路和自定义 HTTPS 前缀；版本与校验信息仍来自 GitHub 官方 API。
- 下载校验架构、版本、大小和 SHA-256，确认安装前不会自动退出 RN。
- 便携版使用独立 WinUI 更新器备份、替换和启动，失败时尝试恢复旧文件。
- MSIX 交由 Windows 验证身份、签名及版本；使用新测试证书时仍需用户信任相应证书。
- Store 渠道使用 Windows.Services.Store。当前测试包不是 Store 包，不调用其他应用的商店身份。

用户设置位于 `%LOCALAPPDATA%/RimeNotice/settings.json`。更新记录、备份和诊断位于该目录的对应子目录。字体文件在 MSIX 下缓存在应用本地数据目录，便携版保存在程序目录的 UserFonts 文件夹；该目录需可写。

## 源码构建

准备 Windows、PowerShell 7.2+、.NET SDK 10.0.402、Windows SDK，以及带 Windows SDK .NET 工具的 Visual Studio / Build Tools。原生构建使用 MSBuild 18；旧版 Visual Studio 环境改用该 .NET SDK 自带的 MSBuild。

在仓库根目录执行：

```powershell
./tools/Initialize-Build.ps1
dotnet run --project tools/RuleChecks -c Release
./tests/RimeNotice.Update.Tests/Test-PortableUpdate.ps1
./tools/Build-Payload.ps1 -OutputDirectory artifacts/Payload
```

生成便携版和 MSIX（可复用同一份自包含程序）：

```powershell
$portable = ./tools/Build-Portable.ps1 -OutputDirectory artifacts/Portable
./tools/Build-MSIX.ps1 -PublishedDirectory $portable.Stage -OutputDirectory artifacts/MSIX
```

自包含包包含 .NET 与 Windows App SDK 运行依赖。CI 保留自动检查代码，不上传参考项目、内部任务、模型试验、构建目录及人工测试文档。

## 自动打包与发布

`.github/workflows/ci.yml` 编译主程序与更新器，并运行离线规则和隔离更新回滚检查。`package.yml` 可手动生成构建产物，或使用 `vMAJOR.MINOR.PATCH` 标签生成草稿 Release。三段标签映射为四段包版本，例如 `v1.0.1` 对应 `1.0.1.0`。

工作流不会自动公开草稿，也不会自动提交到 Microsoft Store。生成的 Release 附件名称与 RN 更新器约定一致。

<details>
<summary>固定签名与 Microsoft Store 配置</summary>

本地可通过 `Build-MSIX.ps1 -CertificateThumbprint` 选择当前用户证书库内的固定签名证书。证书发布者必须与安装包身份相符；不提供固定签名时生成本地测试证书，不修改系统信任。

CI 可配置 `RN_SIGNING_PFX_BASE64`、`RN_SIGNING_PASSWORD` Secrets。私钥仅在构建环境中使用，不能提交到 Git 或发布为附件。

Store 打包需要 Partner Center 的真实应用身份。参照 `packaging/StoreIdentity.example.json` 创建本地身份文件后使用：

```powershell
./tools/Build-MSIX.ps1 -Store -StoreIdentityFile packaging/StoreIdentity.json
```

CI 对应 Secret 为 `RN_STORE_IDENTITY_JSON`。未配置时不生成商店就绪产物。商店关联、认证、限定群体分发和真实版本升级仍需在 Partner Center 完成。

</details>

## 常见问题

**收不到消息？** 检查 QQ 是否产生 Windows 通知、RN 是否具有通知访问权限，以及是否选择了适合当前安装形式的接收模式。调试页可查看权限和实际接收方式。

**长文本不完整？** 先用调试页的长正文预览判断显示是否正常，再核对系统通知提供的正文长度。仅有通知摘要时请回到 QQ 查看完整消息。

**关闭窗口后怎样退出？** 关闭的是设置窗口；托盘中选择“退出 RimeNotice”才停止后台接收。

**Edge 无法播放？** 检查网络、在线文本发送开关和回退选项；服务可能变化，可改用 Windows 本机语音。

## 反馈与贡献

请在 [Issues](https://github.com/WRMYX/RimeNotice/issues) 提供版本、Windows 版本、安装渠道、接收模式、复现步骤和经过脱敏的截图。不要上传聊天记录、令牌、私钥或包含个人消息的日志。

项目仍在完善，构建及自动检查通过不代表所有设备上的动画、UIA、在线语音或商店升级已经验收。界面使用原生 WinUI 组件，不引入 WPF、Qt 或网页界面。

打包脚本及更新回滚测试参照并移植自 [RimePPT](https://github.com/WRMYX/RimePPT)，相关脚本许可见 [tools/LICENSE-RimePPT](tools/LICENSE-RimePPT)。设计参考 [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery)，第三方说明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。项目整体许可证尚待作者确定，此处不代为选择。
