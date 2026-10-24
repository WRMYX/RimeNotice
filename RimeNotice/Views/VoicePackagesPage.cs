using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RimeNotice.Services;
namespace RimeNotice.Views;
public sealed partial class VoicePackagesPage : SettingsFormPage {
 private readonly TextBlock _status=new(){TextWrapping=TextWrapping.Wrap};
 private readonly InfoBar _failure=new(){IsOpen=false,IsClosable=true,Severity=InfoBarSeverity.Error,Title="语音包下载未完成"};
 private readonly ComboBox _packages=new(){MinWidth=200,MinHeight=36};
 private readonly HyperlinkButton _location=new(){HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Visibility=Visibility.Collapsed};
 private readonly Button _openFolder=new(){Content="打开所在文件夹",Visibility=Visibility.Collapsed};
 private readonly Button _retryButton=new(){Content="重试下载",Visibility=Visibility.Collapsed};
 private ChineseVoicePackage[] _catalog=[];private CancellationTokenSource? _operation;private ChineseVoicePackage? _retry;private string? _file;private bool _unloaded;
 public VoicePackagesPage(){InitializeComponent();
  Rows.Children.Add(_failure);Rows.Children.Add(_status);
  Section("中文自然语音 MSIX");Note("下载原始语音包文件。使用 NaturalVoiceSAPIAdapter 时，将 MSIX 解压到固定目录，再在该工具中设置“本地语音路径”。无需安装语音包。当前收录中文历史版本，兼容情况以该工具说明为准。");
  try{_catalog=VoicePackageDownloader.Catalog();foreach(var voice in _catalog)_packages.Items.Add(voice.DisplayName+" · "+voice.Version);if(_catalog.Length>0)_packages.SelectedIndex=0;}catch(Exception ex){ShowFailure(ex);}
  Row("选择声音",_packages,"来源：NaturalVoiceSAPIAdapter Wiki / NVDA 中文站。");
  try{Note("默认保存到："+VoicePackageDownloader.DownloadDirectory());}catch(Exception ex){ShowFailure(ex);}
  _packages.SelectionChanged+=(_,_)=>RefreshLocation();
  Button("下载所选语音包",async()=>{if(_packages.SelectedIndex<0)throw new InvalidOperationException("请先选择声音。");await DownloadAsync(_catalog[_packages.SelectedIndex]);});
  Button("取消下载",()=>{_operation?.Cancel();return Task.CompletedTask;});
  _retryButton.Click+=async(_,_)=>{if(_retry is{} selected)await DownloadAsync(selected);};Rows.Children.Add(_retryButton);
  _location.Click+=(_,_)=>OpenLocation();_openFolder.Click+=(_,_)=>OpenLocation();Rows.Children.Add(_location);Rows.Children.Add(_openFolder);
  Group("如何使用语音包",()=>{
   Note("1. 自行下载并配置 NaturalVoiceSAPIAdapter。\n2. 将下载的 MSIX 当作压缩包解压到固定文件夹。\n3. 在适配器中选择该目录作为本地语音路径；配置后保留解压文件。\n4. 返回 RN 的“声音与语音”，刷新本机声音并选择对应的 SAPI 声音。");
   var link=new HyperlinkButton{Content="下载 NaturalVoiceSAPIAdapter",NavigateUri=new Uri("https://github.com/gexgd0419/NaturalVoiceSAPIAdapter/releases")};Rows.Children.Add(link);
   Note("RN 只下载 MSIX，不安装、卸载、解压语音包，也不安装适配器。文件不会在下载完成后自动运行。");
  });
  Button("返回声音与语音",()=>{App.Instance.Main?.NavigateTo("audio");return Task.CompletedTask;});
  Loaded+=(_,_)=>{_unloaded=false;RefreshLocation();};Unloaded+=(_,_)=>{_unloaded=true;_operation?.Cancel();};RefreshLocation();
 }
 private void RefreshLocation(){if(_packages.SelectedIndex<0)return;var path=VoicePackageDownloader.LastDownload(_catalog[_packages.SelectedIndex]);if(_operation is null)_status.Text=path is null?"尚未下载所选语音包。":"点击文件路径打开所在目录。";SetLocation(path);}
 private void SetLocation(string? path){_file=path;_location.Visibility=_openFolder.Visibility=path is null?Visibility.Collapsed:Visibility.Visible;if(path is null)return;_location.Content=new TextBlock{Text=path,TextWrapping=TextWrapping.Wrap};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_location,"打开下载文件所在目录："+path);if(!System.IO.File.Exists(path))_status.Text="上次下载的文件已移动或删除，可重新下载。";}
 private void OpenLocation(){try{if(_file is{} path)VoicePackageDownloader.Reveal(path);}catch(Exception ex){ShowFailure(ex);}}
 private async Task DownloadAsync(ChineseVoicePackage selected){
  if(_operation is not null){_status.Text="已有下载正在进行，请等待或取消。";return;}
  using var stop=new CancellationTokenSource();_operation=stop;_packages.SelectedIndex=Array.FindIndex(_catalog,v=>v==selected);_retry=selected;_failure.IsOpen=false;_retryButton.Visibility=Visibility.Collapsed;_packages.IsEnabled=false;
  try{
   _status.Text="正在连接下载源…";string file=await VoicePackageDownloader.DownloadAsync(selected,new Progress<string>(text=>{if(!_unloaded&&ReferenceEquals(_operation,stop))_status.Text=text;}),stop.Token);
   string warning="";try{VoicePackageDownloader.Remember(selected,file);}catch(Exception ex){warning="\n下载记录未能保存；请记下下面的文件位置。";ServiceDiagnostics.Error("语音下载记录保存",ex);}
   if(!_unloaded){SetLocation(file);_status.Text="下载完成，点击文件路径或按钮打开所在目录。"+warning;}
  }catch(OperationCanceledException){if(!_unloaded)_status.Text="下载已取消，未完成的临时文件已清理。";}
  catch(Exception ex){if(!_unloaded){ShowFailure(ex);_status.Text="下载未完成，可以重试。";_retryButton.Visibility=Visibility.Visible;}}
  finally{_operation=null;_packages.IsEnabled=true;}
 }
 private void ShowFailure(Exception ex){_failure.Message=ex is System.Net.Http.HttpRequestException?"无法连接下载源，请检查网络或系统代理后重试。":ex.Message;_failure.IsOpen=true;ServiceDiagnostics.Error("语音包下载页面",ex);}
}
