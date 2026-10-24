using System;
using System.Linq;
using RimeNotice.Services;
using RimeNotice.Models;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace RimeNotice.Views;
public sealed partial class ReceptionPage : SettingsFormPage {
 public ReceptionPage(){InitializeComponent();var o=Current.Reception;Section("消息来源");
  Choice("系统通知接收方式",["事件驱动（推荐）","定时扫描"],o.EventDriven?0:1,i=>Save(s=>s with{Reception=s.Reception with{EventDriven=i==0}}));
  int compatibilityStart=Rows.Children.Count;
  Number("扫描间隔（秒）",o.ScanSeconds,.1,5,v=>Save(s=>s with{Reception=s.Reception with{ScanSeconds=v}}));
  Note("RN 仅接收 QQ 消息，其他软件的通知不会进入提醒队列。");
  Toggle("使用 UI Automation 兼容接收",o.UseUIAutomation,v=>Save(s=>s with{Reception=s.Reception with{UseUIAutomation=v}}),"安装版默认关闭；便携版自动使用此兼容来源。依赖 QQ 可见通知横幅，不能保证所有系统版本兼容。");
  Note("切换来源后跳过当时已有的通知，只接收之后出现的新通知。系统通知模式需要安装版及通知访问权限。");
  Fold("扫描与 UIA 高级选项",compatibilityStart);
  Section("仅显示 RN 通知");
  Rows.Children.Add(new InfoBar{IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Informational,Title="在 Windows 中关闭 QQ 横幅",Message="保留 QQ 通知总开关及通知中心，只关闭横幅和声音。此方法使用系统通知来源；UIA 兼容来源依赖可见横幅。RN 无法提前拦截横幅，不要关闭 QQ 通知总开关。"});
  Button("打开 Windows 通知设置",()=>WindowsSettingsLauncher.OpenAsync("notifications"));
  Section("图片与附件");Toggle("启用图片与附件预览",o.PreviewImages,v=>Save(s=>s with{Reception=s.Reception with{PreviewImages=v}}));
  Group("图片显示与滚动",()=>{
   var images=o.Images;
   Choice("图片显示位置",["通知弹窗内","通知旁边（放大）"],(int)images.Placement,i=>Save(s=>s with{Reception=s.Reception with{Images=s.Reception.Images with{Placement=(ImagePlacement)i}}}));
   Choice("外部图片在通知哪一侧",["左侧","右侧"],(int)images.Side,i=>Save(s=>s with{Reception=s.Reception with{Images=s.Reception.Images with{Side=(ImageSide)i}}}));
   Toggle("长正文与多图自动滚动",images.AutoScroll,v=>Save(s=>s with{Reception=s.Reception with{Images=s.Reception.Images with{AutoScroll=v}}}));
   Number("滚动速度（DIP/秒）",images.ScrollSpeed,8,80,v=>Save(s=>s with{Reception=s.Reception with{Images=s.Reception.Images with{ScrollSpeed=v}}}));
   Number("手动滚动后续滚等待（秒）",images.ResumeSeconds,1,30,v=>Save(s=>s with{Reception=s.Reception with{Images=s.Reception.Images with{ResumeSeconds=v}}}));
   Number("外部图片区宽度（DIP）",images.PanelWidth,240,800,v=>Save(s=>s with{Reception=s.Reception with{Images=s.Reception.Images with{PanelWidth=v}}}));
   Note("多图等比排列，不裁剪；可滚轮、拖动滚动条或触摸。所选一侧空间不足时回退通知内显示。设置从下一条通知生效。");
  });
  int imageSettingsStart=Rows.Children.Count;
  Toggle("按时间推测图片",o.InferRecentImages,v=>Save(s=>s with{Reception=s.Reception with{InferRecentImages=v}}),"没有明确文件名时，匹配消息接收时间附近的新图片。可能匹配错误；同批多个候选会显示为推测相册。仅只读访问目录。");
  var account=new TextBox{Text=o.QQAccount,Width=260,MinHeight=36};
  var directory=new TextBox{Text=o.CacheDirectory,Width=260,MinHeight=36};
  Row("QQ 账号",account,"只读取已知目录结构；无法确认时请填写账号。");
  Row("缩略图或附件目录",directory,"用于唯一匹配通知中的文件名，不代表能够还原聊天图片。");
  var detection=new TextBlock{TextWrapping=TextWrapping.Wrap};Rows.Children.Add(detection);
  var candidates=new ComboBox{MinWidth=240,MaxWidth=420,Visibility=Visibility.Collapsed};Rows.Children.Add(candidates);
  bool searching=false;int generation=0;
  async Task DiscoverAsync(){
   int request=++generation;searching=true;
   try{
    string number=new string(account.Text.Where(char.IsAsciiDigit).ToArray());string root=directory.Text;
    var found=await Task.Run(()=>QQDirectoryDiscovery.Discover(number,root));
    if(request!=generation)return;
    candidates.Items.Clear();candidates.Visibility=Visibility.Collapsed;
    if(found.Count==1){account.Text=found[0].Account;directory.Text=found[0].Directory;Save(s=>s with{Reception=s.Reception with{QQAccount=account.Text,CacheDirectory=directory.Text}});detection.Text="已找到一个账号目录；请确认它对应当前使用的 QQ。";}
    else if(found.Count>1){foreach(var item in found)candidates.Items.Add(new ComboBoxItem{Content=item.Account+" · "+item.Directory,Tag=item});candidates.Visibility=Visibility.Visible;detection.Text="找到多个账号目录，请选择。";}
    else detection.Text="未找到可确认的目录。可填写 QQ 账号后重试，或手动选择实际附件目录。";
   }catch(Exception ex){Error(ex);}finally{if(request==generation)searching=false;}
  }
  candidates.SelectionChanged+=(_,_)=>{if(candidates.SelectedItem is ComboBoxItem{Tag:QQDirectoryCandidate item}){account.Text=item.Account;directory.Text=item.Directory;Save(s=>s with{Reception=s.Reception with{QQAccount=item.Account,CacheDirectory=item.Directory}});detection.Text="已使用选中的账号目录。";}};
  account.LostFocus+=async(_,_)=>{if(searching)return;Save(s=>s with{Reception=s.Reception with{QQAccount=account.Text}});await DiscoverAsync();};
  directory.LostFocus+=(_,_)=>{if(!searching)Save(s=>s with{Reception=s.Reception with{CacheDirectory=directory.Text}});};
  Button("重新查找 QQ 目录",DiscoverAsync);
  Loaded+=async(_,_)=>{if(string.IsNullOrWhiteSpace(account.Text)||string.IsNullOrWhiteSpace(directory.Text))await DiscoverAsync();};
  Button("手动选择附件匹配目录",async()=>{var picker=new global::Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.Main!));var folder=await picker.PickSingleFolderAsync();if(folder is not null){++generation;searching=false;directory.Text=folder.Path;Save(s=>s with{Reception=s.Reception with{CacheDirectory=folder.Path}});detection.Text="已使用所选目录。只有通知中的明确文件名或路径可用于匹配预览。";}});
  Button("检测图片目录",async()=>{
   try{
    detection.Text="正在检测图片目录…";var options=Current.Reception;
    var files=await QQImageIndex.Shared.SnapshotAsync(options,System.Threading.CancellationToken.None);
    int checkedImages=0,decodable=0;int unsupported=files.Count(f=>!NotificationAttachmentResolver.Allowed(f.Path));foreach(var file in files.Where(f=>NotificationAttachmentResolver.Allowed(f.Path)).OrderByDescending(f=>f.WrittenUtc).Take(3)){
     checkedImages++;if(await NativeImageDecoder.CanDecodeAsync(file.Path,System.Threading.CancellationToken.None))decodable++;
    }
    detection.Text=QQImageIndex.Shared.Describe()+$"；最近 {checkedImages} 个候选中 {decodable} 个可解码；{unsupported} 个文件不属于支持的附件扩展名。请发送一张新图片后再次检测；没有文件变化时请核对 QQ 账号和实际保存目录。";
    ServiceDiagnostics.Set("图片目录检测",detection.Text);
   }catch(Exception ex){Error(ex);}
  });
  Number("图片收集上限（秒）",o.AttachmentWaitSeconds,0,30,v=>Save(s=>s with{Reception=s.Reception with{AttachmentWaitSeconds=(int)v}}));
  Number("相邻图片归批间隔（秒）",o.Images.BatchGapSeconds,.5,5,v=>Save(s=>s with{Reception=s.Reception with{Images=s.Reception.Images with{BatchGapSeconds=v}}}));
  Note("一条图片通知也会持续收集附近的新图并补入相册。收集上限 0 表示只检查一次；非零时按实际设定秒数观察，最多 30 秒。相邻图片在归批间隔内可组成一批，Thumb/Ori 只计一张。只读所选目录，时间推测可能匹配错误。");
  Fold("图片目录与匹配参数",imageSettingsStart);
 }
}
