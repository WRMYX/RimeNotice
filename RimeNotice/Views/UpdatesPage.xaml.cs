using System;
using System.Collections.Generic;
using Windows.Services.Store;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RimeNotice.Core;
using RimeNotice.Services;
using RimeNotice.Windows;
namespace RimeNotice.Views;
public sealed partial class UpdatesPage : SettingsFormPage {
 private readonly GitHubUpdateService _service=new();private CancellationTokenSource? _job;
 private StoreContext? _store;private IReadOnlyList<StorePackageUpdate>? _storeUpdates;
 private GitHubRelease? _release;private string? _payload;
 private readonly TextBlock _status=new(){Text="",Visibility=Visibility.Collapsed,TextWrapping=TextWrapping.Wrap};
 private readonly TextBlock _notes=new(){TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
 private readonly ProgressBar _progress=new(){Minimum=0,Maximum=100};
 private readonly Button _check=new(){Content="检查更新"},_download=new(){Content="下载更新",IsEnabled=false},_install=new(){Content="安装更新",IsEnabled=false};
 private readonly Button _cancel=new(){Content="取消",Visibility=Visibility.Collapsed};
 private readonly InfoBar _info=new(){IsClosable=false};
 private readonly TextBlock _lastChecked=new(){Text="尚未检查更新",TextWrapping=TextWrapping.Wrap};
 private bool _unloaded;
 public UpdatesPage(){InitializeComponent();Rows.Spacing=16;Rows.MaxWidth=960;
  var top=new Grid{ColumnSpacing=24,Padding=new Thickness(0,20,0,24)};
  top.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(96)});top.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  var icon=(FontIcon)Microsoft.UI.Xaml.Markup.XamlReader.Load("<FontIcon xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Glyph='&#xE895;' FontSize='72' VerticalAlignment='Top' Foreground='{ThemeResource AccentTextFillColorPrimaryBrush}'/>");top.Children.Add(icon);
  var details=new StackPanel{Spacing=10};Grid.SetColumn(details,1);top.Children.Add(details);
  details.Children.Add(new TextBlock{Text="检查 RimeNotice 更新",Style=(Style)Application.Current.Resources["SubtitleTextBlockStyle"],TextWrapping=TextWrapping.Wrap});
  details.Children.Add(new TextBlock{Text="当前版本："+GitHubUpdateService.InstalledVersion});
  details.Children.Add(new TextBlock{Text=GitHubUpdateService.Channel switch{GitHubUpdateChannel.Portable=>"便携版 · GitHub 正式版本",GitHubUpdateChannel.Store=>"Microsoft Store",_=>"MSIX 安装版 · GitHub 正式版本"},TextWrapping=TextWrapping.Wrap});
  _check.Style=(Style)Application.Current.Resources["AccentButtonStyle"];
  var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};foreach(var button in new[]{_check,_download,_install,_cancel})actions.Children.Add(button);details.Children.Add(actions);
  actions.SizeChanged+=(_,_)=>actions.Orientation=details.ActualWidth<380?Orientation.Vertical:Orientation.Horizontal;
  details.Children.Add(_lastChecked);details.Children.Add(_status);details.Children.Add(_progress);details.Children.Add(_info);Rows.Children.Add(top);
  top.SizeChanged+=(_,_)=>{bool narrow=top.ActualWidth<440;icon.Visibility=narrow?Visibility.Collapsed:Visibility.Visible;top.ColumnDefinitions[0].Width=new GridLength(narrow?0:96);top.ColumnSpacing=narrow?0:24;};
  _check.Click+=async(_,_)=>await RunAsync(async token=>{_payload=null;_release=null;_storeUpdates=null;_notes.Text="检查更新后显示版本说明。";
   if(GitHubUpdateService.Channel==GitHubUpdateChannel.Store){_store=StoreContext.GetDefault();WinRT.Interop.InitializeWithWindow.Initialize(_store,WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.Main!));_storeUpdates=await _store.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(token);_status.Text=_storeUpdates.Count>0?"发现商店更新":"已是最新商店版本";}
   else{_release=await _service.CheckAsync(token);_notes.Text=_release?.Notes??"项目尚未发布正式版本。";_status.Text=_release is null?"尚无正式发布版本":_release.Version>GitHubUpdateService.InstalledVersion?"发现新版本 "+_release.Tag:"已是最新版本";}
   _lastChecked.Text="上次检查："+DateTime.Now.ToString("yyyy-MM-dd HH:mm");});
  _download.Click+=async(_,_)=>await RunAsync(async token=>{if(_release?.Asset is not{} asset)return;_payload=null;var settings=Current.Updates;
   var path=await _service.DownloadAsync(asset,new Progress<double>(v=>{if(_unloaded)return;_progress.IsIndeterminate=false;_progress.Value=v;}),token,settings.SourceId,settings.CustomPrefix);
   _payload=await Task.Run(()=>GitHubUpdateService.Extract(path,GitHubUpdateService.Channel,token),token);_status.Text="下载及 SHA-256 校验完成，等待安装";
   UpdateHistory.Write(_release.Tag,GitHubUpdateService.Channel.ToString(),"下载完成","文件大小与 SHA-256 校验通过，尚未安装。");});
  _install.Click+=async(_,_)=>await RunAsync(_=>InstallAsync());_cancel.Click+=(_,_)=>_job?.Cancel();
  Section("更多选项");
  Toggle("启动时检查更新",Current.Updates.CheckOnStartup,v=>Save(x=>x with{Updates=x.Updates with{CheckOnStartup=v}}),"检查 GitHub 正式版本，下载和安装仍由你确认。商店版由 Microsoft Store 管理。");
  if(GitHubUpdateService.Channel!=GitHubUpdateChannel.Store){
   var source=new ComboBox{Header="选择下载线路",HorizontalAlignment=HorizontalAlignment.Stretch};
   string[] ids=["direct","ghproxy","ghfast","ghproxynet","custom"];foreach(string label in new[]{"GitHub 官方","gh-proxy.org","ghfast.top","ghproxy.net","自定义 HTTPS 地址"})source.Items.Add(label);
   source.SelectedIndex=Array.IndexOf(ids,Current.Updates.SourceId);source.SelectionChanged+=(_,_)=>{if(source.SelectedIndex>=0)Save(x=>x with{Updates=x.Updates with{SourceId=ids[source.SelectedIndex]}});};
   var address=new TextBox{Header="自定义 HTTPS 加速前缀",Text=Current.Updates.CustomPrefix,PlaceholderText="https://example.com/"};
   var save=new Button{Content="保存地址"};save.Click+=(_,_)=>{try{string prefix=GitHubDownloadSource.NormalizePrefix(address.Text);Save(x=>x with{Updates=x.Updates with{CustomPrefix=prefix,SourceId="custom"}});source.SelectedIndex=4;}catch(Exception ex){Error(ex);}};
   var remove=new Button{Content="删除所选地址"};remove.Click+=(_,_)=>{if(source.SelectedIndex!=4)return;Save(x=>x with{Updates=x.Updates with{CustomPrefix="",SourceId="direct"}});address.Text="";source.SelectedIndex=0;};
   var sourceActions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};sourceActions.Children.Add(save);sourceActions.Children.Add(remove);
   var body=new StackPanel{Spacing=12};body.Children.Add(source);body.Children.Add(address);body.Children.Add(sourceActions);body.Children.Add(new TextBlock{Text="线路只用于下载安装包；版本信息与文件校验信息来自 GitHub 官方。",TextWrapping=TextWrapping.Wrap});
   Rows.Children.Add(new Expander{Style=(Style)Application.Current.Resources["SettingExpanderStyle"],Header="下载线路",Content=body,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  }
  _notes.Text="检查更新后显示版本说明。";Rows.Children.Add(new Expander{Style=(Style)Application.Current.Resources["SettingExpanderStyle"],Header="更新说明",Content=_notes,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  var history=new TextBlock{Text="暂无更新记录。",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};var historyPanel=new Expander{Style=(Style)Application.Current.Resources["SettingExpanderStyle"],Header="更新历史记录",Content=history,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};historyPanel.Expanding+=(_,_)=>{try{history.Text=UpdateHistory.Read();}catch(Exception ex){Error(ex);}};Rows.Children.Add(historyPanel);
  if(GitHubUpdateService.Channel==GitHubUpdateChannel.Store){Section("Microsoft Store");Note("当前版本："+GitHubUpdateService.InstalledVersion);Button("在 Microsoft Store 中查看",async()=>{if(!await global::Windows.System.Launcher.LaunchUriAsync(new Uri(RimeNotice.Core.StoreIdentity.ProductUri)))throw new InvalidOperationException("无法打开 Microsoft Store，请稍后重试。");});}
  var releasePanel=new StackPanel{Spacing=4};releasePanel.Children.Add(new TextBlock{Text="项目发布页",Style=(Style)Application.Current.Resources["BodyStrongTextBlockStyle"]});releasePanel.Children.Add(new HyperlinkButton{Content="GitHub · WRMYX/RimeNotice",NavigateUri=new Uri("https://github.com/WRMYX/RimeNotice/releases"),HorizontalAlignment=HorizontalAlignment.Left});Rows.Children.Add(new Border{Style=(Style)Application.Current.Resources["SettingCardStyle"],Padding=new Thickness(20),Child=releasePanel});
  RefreshActions();Unloaded+=(_,_)=>{_unloaded=true;_job?.Cancel();};Loaded+=(_,_)=>_unloaded=false;
 }
 private void RefreshActions(){bool busy=_job is not null;_check.IsEnabled=!busy;_cancel.Visibility=busy?Visibility.Visible:Visibility.Collapsed;_progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;
  _download.IsEnabled=!busy&&_release?.Asset is not null&&_release.Version>GitHubUpdateService.InstalledVersion&&_payload is null;_download.Visibility=_download.IsEnabled?Visibility.Visible:Visibility.Collapsed;
  _install.Content=GitHubUpdateService.Channel==GitHubUpdateChannel.Store?"下载并安装":"安装更新";_install.IsEnabled=!busy&&(_payload is not null||_storeUpdates?.Count>0);_install.Visibility=_install.IsEnabled?Visibility.Visible:Visibility.Collapsed;
 }
 private async Task RunAsync(Func<CancellationToken,Task> action){if(_job is not null)return;using var job=new CancellationTokenSource();_job=job;_info.IsOpen=false;_status.Visibility=Visibility.Visible;_status.Text="正在处理…";_progress.Value=0;_progress.IsIndeterminate=true;RefreshActions();
  try{await action(job.Token);}catch(OperationCanceledException){_status.Text="操作已取消";}catch(Exception ex){_status.Text="操作未完成";_info.Severity=InfoBarSeverity.Error;_info.Message=ex.Message;_info.IsOpen=true;ServiceDiagnostics.Error("更新",ex);UpdateHistory.Write(_release?.Tag??"",GitHubUpdateService.Channel.ToString(),"失败",ex.Message);}finally{_job=null;_progress.IsIndeterminate=false;RefreshActions();}}
 private async Task InstallAsync(){if((_payload is null||_release is null)&&!(_storeUpdates?.Count>0))return;
  var confirm=new ContentDialog{XamlRoot=XamlRoot,Title="安装 "+(_release?.Tag??"商店更新")+"？",Content=GitHubUpdateService.Channel==GitHubUpdateChannel.Portable?"RN 将退出并替换程序文件。旧文件保留备份，更新失败会回滚。":"Windows 将检查包签名、身份和版本并请求安装确认。",PrimaryButtonText="继续安装",CloseButtonText="取消",DefaultButton=ContentDialogButton.Close};
  if(await confirm.ShowAsync()!=ContentDialogResult.Primary)return;
  try{if(GitHubUpdateService.Channel==GitHubUpdateChannel.Store&&_store is not null&&_storeUpdates is not null){var storeWindow=new UpdateProgressWindow();storeWindow.Activate();var operation=_store.RequestDownloadAndInstallStorePackageUpdatesAsync(_storeUpdates);operation.Progress=(_,p)=>DispatcherQueue.TryEnqueue(()=>storeWindow.ShowStatus("商店安装更新","下载及安装由 Microsoft Store 管理。",p.PackageDownloadProgress*100));var result=await operation.AsTask(_job?.Token??CancellationToken.None);storeWindow.ShowStatus("商店更新结果",result.OverallState==StorePackageUpdateState.Completed?"更新完成":"更新未完成，请查看 Microsoft Store",finished:true,succeeded:result.OverallState==StorePackageUpdateState.Completed);return;}
   if(_payload is null||_release is null)return;
   if(GitHubUpdateService.Channel==GitHubUpdateChannel.Portable){await PortableUpdateInstaller.StartAsync(_payload,_release.Version);App.Instance.ExitApplication();return;}
   var files=Directory.GetFiles(_payload,"*.msix",SearchOption.AllDirectories);if(files.Length!=1)throw new InvalidDataException("更新包应包含一个 MSIX。");
   using(var zip=ZipFile.OpenRead(files[0])){if(zip.GetEntry("AppxSignature.p7x") is null)throw new InvalidDataException("安装包未签名。");using var xml=zip.GetEntry("AppxManifest.xml")!.Open();var id=XDocument.Load(xml).Root!.Elements().First(x=>x.Name.LocalName=="Identity");var current=global::Windows.ApplicationModel.Package.Current.Id;
    if((string?)id.Attribute("Name")!=current.Name||(string?)id.Attribute("Publisher")!=current.Publisher||Version.Parse((string)id.Attribute("Version")!)!=_release.Version)throw new InvalidDataException("安装包身份或版本不匹配。");}
   var window=new UpdateProgressWindow();window.Activate();window.ShowStatus("交由 Windows 安装","请在 Windows 应用安装程序中确认。打开安装程序并不表示安装成功。",allowClose:true);
   if(!await global::Windows.System.Launcher.LaunchFileAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(files[0])))throw new InvalidOperationException("Windows 应用安装程序不可用。");
  }catch(Exception ex){_status.Text=ex.Message;ServiceDiagnostics.Error("安装更新",ex);}
 }
}
