<div align="center">
  <img src="RimeNotice/Assets/RimeNotice-Logo.svg" width="88" height="88" alt="RimeNotice Logo" />
  <h1>RimeNotice</h1>
  <p><strong>让课堂里的重要消息，及时被看见。</strong></p>
  <p>面向 QQ 消息与教学触摸屏的 Windows 通知工具 · 原生 WinUI 3</p>
  <p><a href="https://github.com/WRMYX/RimeNotice/releases">下载发布版</a> · <a href="https://RimeNotice.MYXMJY.Top">项目官网</a> · <a href="https://github.com/WRMYX/RimeNotice/issues">反馈问题</a></p>
  <p><strong>WinUI 3</strong> / <strong>.NET 10</strong> / <strong>Windows App SDK</strong></p>
</div>

老师们在群里发送消息，课堂电脑右下角的系统通知却很容易被忽略。RimeNotice（RN）接收 QQ 的 Windows 通知，将需要注意的内容显示为独立通知卡片，并提供重要消息、呼叫提醒、提示音和正文朗读。

[功能介绍](#功能介绍) · [安装与上手](#安装与上手) · [软件更新](#软件更新) · [源码构建](#源码构建) · [自动打包与发布](#自动打包与发布) · [反馈与贡献](#反馈与贡献)

## 功能介绍

**v1.1.0** 发布说明见 [版本介绍](docs/releases/v1.1.0.md)，对应包版本为 `1.1.0.0`。发布包与公开状态以 [Releases](https://github.com/WRMYX/RimeNotice/releases) 为准；草稿不会被官网或应用的正式更新检查选中。

### 接收与提醒

- 仅接收 QQ：系统通知默认采用事件唤醒，支持定时扫描及独立 UI Automation 兼容模式。
- 按联系人、关键词及 @ 提醒识别重要消息，支持黑名单、白名单和重复消息冷却。
- 呼叫提醒可持续到确认，支持循环铃声、静音和可选边框呼吸；原生与材质弹窗均支持呼吸，默认关闭，BPM 可调。
- 启动直接进入托盘，关闭设置窗口后继续运行；托盘菜单提供打开设置和退出。
- 新用户首次运行尝试启用开机启动，尊重 Windows 已禁用状态与系统策略；可在设置中关闭。通知默认置顶，可单独关闭。

### 通知显示

- 两种通知样式可切换：**原生弹窗**使用 ContentDialog 默认卡片及其打开、关闭动画；**材质弹窗**使用 WinUI 自定义布局与窗口背景材质。
- 按内容收紧高度，达到设定上限后纵向滚动。长正文支持自动滚动，滚轮、拖动和触摸可手动接管，静置后从当前位置续滚；阅读期间暂缓关闭倒计时。
- 多条消息按接收顺序排队，等待卡片不倒计时或播放声音；最多等待 512 条，超出时明确报告。
- 发送人与正文分别设置字体，支持导入 TTF、OTF；短名字不会因为类型栏挤压而强制滚动。
- 支持浅色、深色主题；材质弹窗支持 Mica、Mica Alt、亚克力与纯色，效果不可用时回退纯色。原生弹窗保留默认卡片背景。
- 材质弹窗可分别设置入场与退场：无、渐显／渐隐、上下左右滑动，并调整时长和缓动系数。已移除 Q 弹弹出和放大淡出；原生弹窗不提供这组自定义动画。
- 消息类型与倒计时、可选 QQ／MYXMJY 标识、等级颜色和背景遮罩；原生模式可选择紧凑父窗口或全屏同窗口遮罩。通知显示后会主动获取键盘焦点。
- 鼠标、触摸与键盘均可操作；系统关闭动画时不强制播放动效。

### 图片与附件

- 一条 QQ 图片通知也可收集多张图片：先显示稳定可读的图片，再把收集期内陆续落盘的同批图片补入相册，不依赖 QQ 再发送多条通知。
- 按相邻图片时间间隔归批，默认 2 秒；采集上限默认 5 秒，可设 0–30 秒。0 只检查初始候选并验证稳定性；不是持续后台全盘扫描。
- 同一图片的 Thumb/Ori 按文件名前缀归组，后到 Ori 提升已有图片，不新增一张重复图。已发布图片防止重复认领，关闭通知后停止该条收集。
- 等比多图网格、不裁剪内容、可见区域按需解码，支持自动滚动与手动浏览；补图不重播通知动画或重置阅读位置。
- 图片可放在通知内，或放大显示在通知左／右侧；工作区不足时回退通知内。点击缩略图或“打开附件”链接，可用系统默认程序打开。
- **时间关联属于推测**，无法证明聊天归属；多个聊天同时落盘时可能误关联。明确文件名或路径优先匹配，推测结果会标注。

### 声音与语音

- 提示音可自定义，试听播放完整一遍；支持 Windows 本机语音与可选 Edge 在线语音。
- 支持朗读优先、提示音优先和同时播放，音量衰减比例可调。
- 默认仅朗读正文，可单独选择是否朗读发送人。长正文拆分请求，不静默丢弃尾部。
- 在线语音默认关闭，启用后才向选定服务发送待朗读文本；可设置超时和失败回退。
- Edge 使用可选兼容协议，没有长期可用性保证；RN 不提供 CosyVoice 或 Kokoro 集成。
- 中文语音包页仅下载 MSIX 到系统“下载”文件夹，支持点击路径定位。NaturalVoiceSAPIAdapter 由用户自行下载；语音包解压后在该工具中配置，RN 不安装或卸载语音包。

## 安装与上手

建议使用 x64 Windows 11，或 Windows 10 2004 / 19041 及以上版本。材质显示取决于 Windows 与系统效果设置。

发布文件位于 [GitHub Releases](https://github.com/WRMYX/RimeNotice/releases)。源码版本不代表已公开发布同版本安装包；Microsoft Store 渠道的可用性以实际商店页面为准。

- **MSIX＋公开证书**：信任随包 `.cer` 后通过 Windows 安装 MSIX。测试签名包不是 Microsoft Store 版本，程序不会自动导入证书。
- **便携版**：完整解压 Portable ZIP 后运行 `RimeNotice.exe`，保留全部依赖和资源。系统通知访问需要包身份；未安装的便携版使用 UIA 兼容来源时存在识别限制。

### 第一次使用

1. 登录 QQ，允许 QQ 产生 Windows 通知。
2. 启动 RN 后默认只显示托盘图标。通过托盘打开设置；MSIX 首次使用时点击“授予通知访问权限”，获得权限后自动接收。便携版自动使用 UIA 兼容来源，需要 QQ 可见通知横幅。
3. 如只想显示 RN：在 Windows 的 QQ 通知设置中保留通知中心，只关闭横幅及声音，不要关闭 QQ 的通知总开关。
4. 在“提醒规则”配置联系人、关键词和呼叫词。新用户预置联系人“老师”，关键词“作业、通知、课代表”，均可修改。
5. 需要图片预览时，在“接收兼容”检测 QQ 图片目录，确认账号和实际路径。推荐选择当前 QQ 的 `Pic` 根目录，常见结构为系统文档文件夹下的 `Tencent Files/<QQ号>/nt_qq/nt_data/Pic/<年月>/Thumb` 或 `Ori`；用户迁移过的文档路径也需要核对。
6. 在“通知外观”“声音与语音”中调整显示和声音。新用户默认启用遮罩、触摸友好菜单、图片预览与时间推测，混音为同时播放；旧用户设置保留，不由新默认覆盖。关闭设置后仍会继续接收；完全退出请使用托盘菜单。

## 数据与限制

- RN 不修改 QQ 文件、不注入 QQ、不读取聊天数据库，也不保存消息历史。需回看消息时请打开 QQ。
- Windows 通知接口只能提供 QQ 实际投递的通知文本。若 QQ 只提供摘要，RN 无法从该接口还原聊天全文。
- 图片只读访问用户选定的 QQ 目录，优先明确路径匹配；可选时间推测使用目录事件和有界索引核对。收集上限外的图片不加入旧通知，可按实际保存延迟调整上限。“检测图片目录”可检查索引和解码；QQ 没有落盘或文件无法解码时不能预览。
- UIA 依赖 Windows 暴露的可访问通知元素，不保证每个 QQ/Windows 组合都可用。
- 常规诊断用于权限、错误及布局状态。调试页的**手动诊断采样**可能包含消息原文、文件名和窗口边界，仅保存在本机，最多 5 分钟／16 MiB；分享前请自行检查并脱敏。在线朗读启用后会把待朗读文本发送至选定服务。

## 软件更新

- GitHub 渠道检查正式 Release，草稿和预发布不作为更新来源。
- 支持官方直连、预设加速线路和自定义 HTTPS 前缀；版本与校验信息仍来自 GitHub 官方 API。
- 下载校验架构、版本、大小和 SHA-256，确认安装前不会自动退出 RN。
- 便携版使用独立 WinUI 更新器备份、替换和启动，失败时尝试恢复旧文件。
- MSIX 交由 Windows 验证身份、签名及版本；使用新测试证书时仍需用户信任相应证书。
- Store 渠道使用 Windows.Services.Store。当前测试包不是 Store 包，不调用其他应用的商店身份。

用户设置位于 `%LOCALAPPDATA%/RimeNotice/settings.json`。更新记录、备份和诊断位于该目录的对应子目录。字体文件在 MSIX 下缓存在应用本地数据目录，便携版保存在程序目录的 UserFonts 文件夹；该目录需可写。

## 源码构建

<details>
<summary>开发环境、构建命令与检查</summary>

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


</details>

## 自动打包与发布

<details>
<summary>GitHub Actions、签名与商店打包</summary>

`.github/workflows/ci.yml` 编译主程序与更新器，并运行离线规则和隔离更新回滚检查。`package.yml` 可手动生成构建产物，或使用 `vMAJOR.MINOR.PATCH` 标签生成草稿 Release。三段标签映射为四段包版本，例如 `v1.1.0` 对应 `1.1.0.0`。手动运行时留空版本只生成产物，版本来自源码 manifest，不自动沿用已有草稿标签。

Actions 的 Artifacts 分别显示：

- `RimeNotice-Portable-x64`：便携 ZIP 与 `SHA256.txt`。
- `RimeNotice-MSIX-with-certificate-x64`：MSIX＋公开证书 ZIP 与 `SHA256.txt`。
- `RimeNotice-Store-x64`：商店提交 MSIX，只在生成商店包后上传。

上传前检查两个发布 ZIP 是否齐全、非空并与校验文件一致。Release 页面仍单独附带两个 ZIP 与 `SHA256.txt`，不把商店提交包作为侧载安装包发布。旧的 Actions 运行保留原有 `RimeNotice-Release-x64` 合集，分组调整只影响新运行。

工作流不会自动公开草稿，也不会自动提交到 Microsoft Store。生成的 Release 附件名称与 RN 更新器约定一致。

<details>
<summary>固定签名与 Microsoft Store 配置</summary>

本地可通过 `Build-MSIX.ps1 -CertificateThumbprint` 选择当前用户证书库内的固定签名证书。证书发布者必须与安装包身份相符；不提供固定签名时生成本地测试证书，不修改系统信任。

CI 可配置 `RN_SIGNING_PFX_BASE64`、`RN_SIGNING_PASSWORD` Secrets。私钥仅在构建环境中使用，不能提交到 Git 或发布为附件。

Store 打包已关联 Partner Center 的公开身份，Store ID 为 `9P8J1LBNZV2D`。默认使用 `packaging/StoreIdentity.public.json`；也可显式传入独立身份文件：

```powershell
./tools/Build-MSIX.ps1 -Store
```

CI 默认使用公开身份配置，Secret `RN_STORE_IDENTITY_JSON` 仅作为可选覆盖。商店包不使用本地测试签名。商店关联、认证、限定群体分发和真实版本升级仍需在 Partner Center 完成。

</details>


</details>

## 常见问题

<details>
<summary>接收、长文本与语音问题排查</summary>

**收不到消息？** 检查 QQ 是否产生 Windows 通知、RN 是否具有通知访问权限，以及是否选择了适合当前安装形式的接收模式。调试页可查看权限和实际接收方式。

**长文本不完整？** 先用调试页的长正文预览判断显示是否正常，再核对系统通知提供的正文长度。仅有通知摘要时请回到 QQ 查看完整消息。

**只显示一部分图片？** 确认选择了正确 QQ 账号和 Pic 目录，启用“按时间推测图片”，核对归批间隔与收集上限。较晚落盘的图片可适当增加收集时间；Thumb/Ori 是同一张图的不同版本，不应重复计数。

**安装语音包后 RN 没显示自然语音？** Windows 讲述人可用的声音不一定能直接被 RN 的朗读接口调用。RN 只下载语音包，不安装或集成适配器；使用 NaturalVoiceSAPIAdapter 时由用户自行解压、配置，然后刷新 RN 本机语音列表。

**关闭窗口后怎样退出？** 关闭的是设置窗口；托盘中选择“退出 RimeNotice”才停止后台接收。

**Edge 无法播放？** 检查网络、在线文本发送开关和回退选项；服务可能变化，可改用 Windows 本机语音。


</details>

## 反馈与贡献

请在 [Issues](https://github.com/WRMYX/RimeNotice/issues) 提供版本、Windows 版本、安装渠道、接收模式、复现步骤和经过脱敏的截图。不要上传聊天记录、令牌、私钥或包含个人消息的日志。

项目仍在完善，构建及自动检查通过不代表所有设备上的动画、UIA、在线语音或商店升级已经验收。界面使用原生 WinUI 组件，不引入 WPF、Qt 或网页界面。

打包脚本及更新回滚测试参照并移植自 [RimePPT](https://github.com/WRMYX/RimePPT)，相关脚本许可见 [tools/LICENSE-RimePPT](tools/LICENSE-RimePPT)。设计参考 [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery)，第三方说明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 许可证

Copyright © 2026 **MYXMJY**。本项目采用 **GNU GPL v3（GPL-3.0-only）**，完整条款见 [LICENSE](LICENSE)。请保留适用的原作者版权与许可证声明；使用和分发以许可证全文为准。

第三方依赖与素材来源见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

---

<div align="center">
  <p>由 <strong>MYXMJY</strong> 开发与维护</p>
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="RimeNotice/Assets/MYXMJY-white.png" />
    <source media="(prefers-color-scheme: light)" srcset="RimeNotice/Assets/MYXMJY-black.png" />
    <img src="RimeNotice/Assets/MYXMJY-black.png" width="180" height="40" alt="开发者 MYXMJY Logo" />
  </picture>
  <p><a href="https://RimeNotice.MYXMJY.Top">了解 RimeNotice</a> · <a href="https://github.com/WRMYX/RimeNotice/releases">查看最新版本</a></p>
</div>
