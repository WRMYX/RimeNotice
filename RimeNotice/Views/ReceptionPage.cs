using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace RimeNotice.Views;
public sealed partial class ReceptionPage : SettingsFormPage {
 public ReceptionPage(){InitializeComponent();var o=Current.Reception;Section("消息来源");
  Choice("系统通知接收方式",["事件驱动（推荐）","定时扫描"],o.EventDriven?0:1,i=>Save(s=>s with{Reception=s.Reception with{EventDriven=i==0}}));
  Number("扫描间隔（秒）",o.ScanSeconds,.1,5,v=>Save(s=>s with{Reception=s.Reception with{ScanSeconds=v}}));
  Note("RN 仅接收 QQ 消息，其他软件的通知不会进入提醒队列。");
  Toggle("使用 UI Automation 兼容接收",o.UseUIAutomation,v=>Save(s=>s with{Reception=s.Reception with{UseUIAutomation=v}}),"默认关闭。识别 Windows 可访问的通知窗口，不能保证所有系统版本兼容。");
  Note("切换来源后跳过当时已有的通知，只接收之后出现的新通知。系统通知模式需要安装版及通知访问权限。");
  Section("仅显示 RN 通知");
  Rows.Children.Add(new InfoBar{IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Informational,Title="在 Windows 中关闭 QQ 横幅",Message="保留 QQ 通知总开关及通知中心，只关闭横幅和声音。此方法使用系统通知来源；UIA 兼容来源依赖可见横幅。RN 无法提前拦截横幅，不要关闭 QQ 通知总开关。"});
  Button("打开 Windows 通知设置",async()=>{await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:notifications"));});
  Section("图片与附件");Toggle("启用可验证的附件预览",o.PreviewImages,v=>Save(s=>s with{Reception=s.Reception with{PreviewImages=v}}));
  Text("QQ 账号（目录辅助标记）",o.QQAccount,v=>Save(s=>s with{Reception=s.Reception with{QQAccount=v}}));
  Text("缩略图或附件目录",o.CacheDirectory,v=>Save(s=>s with{Reception=s.Reception with{CacheDirectory=v}}));
  Button("选择预览目录",async()=>{var picker=new global::Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.Main!));var folder=await picker.PickSingleFolderAsync();if(folder is not null)Save(s=>s with{Reception=s.Reception with{CacheDirectory=folder.Path}});});
  Number("等待明确附件的时间（秒）",o.AttachmentWaitSeconds,0,10,v=>Save(s=>s with{Reception=s.Reception with{AttachmentWaitSeconds=(int)v}}));
  Note("请选择实际缩略图目录。通知必须包含明确的文件名或 file:// 路径且匹配唯一文件，才显示预览。只有“[图片]”时不猜测缓存，不读取 QQ 数据库。");
 }
}
