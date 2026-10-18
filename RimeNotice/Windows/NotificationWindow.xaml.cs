using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using RimeNotice.Models;
using RimeNotice.Controls;
using RimeNotice.Services;
using Windows.Graphics;
namespace RimeNotice.Windows;
public sealed partial class NotificationWindow : Window {
 private IncomingNotice _notice;private readonly RuleSettings _settings;private readonly AppearanceOptions _appearance;
 private readonly ContentDialog _dialog;private readonly Grid _content=new(){RowSpacing=8,VerticalAlignment=VerticalAlignment.Top};
 private readonly TextBlock _countdown=new(){Width=84,TextAlignment=TextAlignment.Right,FontSize=12,VerticalAlignment=VerticalAlignment.Center};
 private readonly InfoBar _typeBar;private readonly StackPanel _message=new(){Spacing=12};
 private readonly ScrollViewer _messageScroll;private readonly MarqueeText _bodyMarquee;private readonly MarqueeText _senderMarquee;
 private readonly NotificationAnimation _animation;private readonly NotificationAnimation _pulse;
 private NotificationAudioService _audio=new();private NotificationMaskWindow? _mask;
 private readonly NotificationLifetime _life=new(ex=>ServiceDiagnostics.Error("会话释放",ex));private readonly Stopwatch _clock=new();
 private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromMilliseconds(200)};
 private readonly TaskCompletionSource _loaded=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private readonly TaskCompletionSource _requested=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private readonly TaskCompletionSource _closed=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private bool _allowClose,_disposed,_fittingContent,_layoutPending,_visible,_layoutAfterShow;private int _duration;private CardSize _limit;private RectInt32 _workArea;private double _scale;private readonly bool _hold;
 public Task Completion=>_closed.Task;
 public NotificationWindow(IncomingNotice notice,ElementTheme theme){
  InitializeComponent();_notice=notice;_settings=SettingsStore.Shared.Current;_appearance=_settings.Appearance;
  _duration=_settings.Duration(notice.Kind);_hold=notice.Kind==NoticePreviewKind.Calling&&_settings.Calling.UntilAcknowledged;
  Root.RequestedTheme=_appearance.NotificationTheme switch{1=>ElementTheme.Light,2=>ElementTheme.Dark,_=>theme};
  AppWindow.IsShownInSwitchers=false;
  if(AppWindow.Presenter is OverlappedPresenter p){p.SetBorderAndTitleBar(false,false);p.IsAlwaysOnTop=_appearance.AlwaysOnTop;p.IsResizable=false;p.IsMinimizable=false;p.IsMaximizable=false;}
  WindowPlumbing.RemoveResizableFrame(this);WindowPlumbing.RemoveWindowBorder(this);WindowPlumbing.SetNotificationShadow(this,_appearance.Shadow);
  for(int i=0;i<3;i++)_content.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var header=new Grid{ColumnSpacing=12,RowSpacing=8};header.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});header.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var sender=new MarqueeText(notice.Sender,_appearance.SenderSize,string.IsNullOrEmpty(_appearance.SenderFontFamily)?_appearance.FontFamily:_appearance.SenderFontFamily){MaxHeight=_appearance.SenderSize*1.6};_senderMarquee=sender;header.Children.Add(sender);_life.Register(sender);

  var status=new Grid{ColumnSpacing=12,Padding=new Thickness(0,4,8,4)};status.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});status.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  status.Children.Add(new TextBlock{Text=notice.Kind switch{NoticePreviewKind.Calling=>"呼叫提醒",NoticePreviewKind.Important=>"重要消息",_=>"普通消息"},FontSize=14,TextTrimming=TextTrimming.CharacterEllipsis,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
  Typography.SetNumeralAlignment(_countdown,FontNumeralAlignment.Tabular);Grid.SetColumn(_countdown,1);status.Children.Add(_countdown);_countdown.Text=_hold?"等待确认":$"{_settings.Duration(notice.Kind)} 秒后关闭";
  _typeBar=new InfoBar{IsOpen=true,IsClosable=false,Content=status,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Padding=new Thickness(8),
   Severity=notice.Kind==NoticePreviewKind.Important?InfoBarSeverity.Warning:InfoBarSeverity.Informational,
   IconSource=notice.Kind==NoticePreviewKind.Calling?new FontIconSource{Glyph="\uE717"}:null};
  Grid.SetColumn(_typeBar,1);header.Children.Add(_typeBar);_content.Children.Add(header);
  _content.SizeChanged+=(_,_)=>{
   bool narrow=NotificationLayout.StackHeader(_content.ActualWidth)||sender.NaturalWidth+Math.Max(220,_typeBar.ActualWidth)+12>_content.ActualWidth;
   Grid.SetColumn(_typeBar,narrow?0:1);Grid.SetColumnSpan(_typeBar,narrow?2:1);
   Grid.SetRow(_typeBar,narrow?1:0);Grid.SetRow(sender,0);Grid.SetColumnSpan(sender,narrow?2:1);
   _typeBar.HorizontalAlignment=HorizontalAlignment.Right;
   _typeBar.MaxWidth=Math.Max(0,_content.ActualWidth);
  };
  _bodyMarquee=new MarqueeText(notice.Message,_appearance.MessageSize,string.IsNullOrEmpty(_appearance.MessageFontFamily)?_appearance.FontFamily:_appearance.MessageFontFamily,true);_message.Children.Add(_bodyMarquee);_life.Register(_bodyMarquee);
  _messageScroll=new ScrollViewer{Content=_message,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollMode=ScrollMode.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(_messageScroll,1);_content.Children.Add(_messageScroll);
  var footer=new Grid{ColumnSpacing=16};footer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  footer.Children.Add(new TextBlock{Text=notice.IsPreview&&string.IsNullOrWhiteSpace(_appearance.Footer)?"模拟消息 · 仅供调试":_appearance.Footer,FontSize=12,TextWrapping=TextWrapping.NoWrap,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});
  var source=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,VerticalAlignment=VerticalAlignment.Center};
  var icon=new Image{Width=20,Height=20,Source=new BitmapImage(new Uri(notice.SourceName is "QQ" or "腾讯QQ"?"ms-appx:///Assets/QQ.png":"ms-appx:///Assets/icon44.png"))};
  source.Children.Add(icon);source.Children.Add(new TextBlock{Text="QQ",FontSize=14,VerticalAlignment=VerticalAlignment.Center});
  var developer=new Image{Width=64,Height=16,Stretch=Stretch.Uniform};source.Children.Add(developer);
  void UpdateDeveloperLogo(){developer.Source=new BitmapImage(new Uri(Root.ActualTheme==ElementTheme.Dark?"ms-appx:///Assets/MYXMJY-white.png":"ms-appx:///Assets/MYXMJY-black.png"));}
  Root.ActualThemeChanged+=(_,_)=>UpdateDeveloperLogo();UpdateDeveloperLogo();Grid.SetColumn(source,1);footer.Children.Add(source);Grid.SetRow(footer,2);_content.Children.Add(footer);
  _dialog=new ContentDialog{Content=_content,PrimaryButtonText=_appearance.ConfirmText,CloseButtonText=_hold?"静音":_appearance.CloseText,DefaultButton=ContentDialogButton.None,
   HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,RequestedTheme=Root.RequestedTheme,MinWidth=0,MinHeight=0,MaxWidth=double.PositiveInfinity,MaxHeight=double.PositiveInfinity,FontSize=_appearance.ButtonSize};
  _dialog.Resources["ContentDialogMaxWidth"]=2000d;
  _dialog.Transitions=new TransitionCollection();
  _dialog.Resources["ContentDialogPadding"]=new Thickness(16);
  WindowMaterialService.Apply(this,Root,_appearance.NotificationMaterial,_appearance.SolidColor);
  ApplySurface(_appearance);
  _dialog.Resources["ContentDialogSmokeFill"]=new SolidColorBrush(Microsoft.UI.Colors.Transparent);
  var primaryStyle=CreateButtonStyle("AccentButtonStyle",_appearance.ConfirmIcon);
  _dialog.PrimaryButtonStyle=primaryStyle;
  _dialog.CloseButtonStyle=CreateButtonStyle("DefaultButtonStyle",_appearance.CloseIcon);
  _dialog.SetValue(Microsoft.UI.Xaml.Automation.AutomationProperties.NameProperty,$"来自 {notice.Sender} 的 {notice.SourceName} 消息");
  _animation=new NotificationAnimation(Root,_appearance.NotificationMaterial==WindowMaterial.Solid);_pulse=new NotificationAnimation(_typeBar);
  _life.Register(_audio);_life.Register(_pulse);_life.Register(_animation);
  _dialog.PrimaryButtonClick+=(_,e)=>{e.Cancel=true;RequestDismiss();};
  _dialog.CloseButtonClick+=(_,e)=>{e.Cancel=true;if(_hold){_audio.Dispose();_pulse.Dispose();ServiceDiagnostics.Set("呼叫","已静音，等待确认");}else RequestDismiss();};
  Root.Children.Add(_dialog);Root.Loaded+=(_,_)=>_loaded.TrySetResult();
  _timer.Tick+=(_,_)=>{if(_hold)return;int remain=NotificationLayout.Remaining(_duration,_clock.Elapsed);_countdown.Text=$"{remain} 秒后关闭";if(remain==0)RequestDismiss();};
  AppWindow.Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;RequestDismiss();}};
  Closed+=(_,_)=>_closed.TrySetResult();
  SettingsStore.Shared.Changed+=ApplyLiveMaterial;
  _=LoadIconAsync(icon);if(_settings.Reception.PreviewImages)_=LoadAttachmentAsync();
 }
 private void ApplyLiveMaterial(){if(_disposed)return;var s=SettingsStore.Shared.Current;var a=s.Appearance;if(AppWindow.Presenter is OverlappedPresenter p)p.IsAlwaysOnTop=a.AlwaysOnTop;_mask?.SetTopmost(a.AlwaysOnTop);int theme=a.NotificationTheme==0?s.Theme:a.NotificationTheme;Root.RequestedTheme=theme switch{1=>ElementTheme.Light,2=>ElementTheme.Dark,_=>ElementTheme.Default};_dialog.RequestedTheme=Root.RequestedTheme;WindowMaterialService.Apply(this,Root,a.NotificationMaterial,a.SolidColor);}
 // The window itself stays fully transparent; the native ContentDialog surface carries the colour,
 // so fading the content fades the whole card and no empty window is ever visible.
 private void ApplySurface(AppearanceOptions a){
  // Let the selected native Window.SystemBackdrop show through the ContentDialog.
  // WindowMaterialService paints the host root when effects are unavailable or Solid is selected.
  _dialog.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);
  _dialog.Resources["ContentDialogBackground"]=_dialog.Background;
 }
 private async Task LoadIconAsync(Image image){try{var source=await AppIconResolver.ResolveAsync(_notice.SourceName,_notice.AppId,_notice.SourceLogo);if(!_disposed)image.Source=source;}catch(Exception ex){ServiceDiagnostics.Error("图标显示",ex);}}
 private async Task LoadAttachmentAsync(){string text=_notice.Message;var attachment=await NotificationAttachmentResolver.ResolveAsync(text,_settings.Reception,_life.Token);if(_disposed||text!=_notice.Message)return;if(attachment is null){if(text.Contains("[图片]",StringComparison.Ordinal)){_message.Children.Add(new TextBlock{Text="QQ 通知未提供可确认的图片路径，请在 QQ 中查看图片。",FontSize=12,TextWrapping=TextWrapping.Wrap});RequestLayout();}return;}
  if(attachment.IsImage){var image=new Image{MaxHeight=160,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};try{using var stream=await (await global::Windows.Storage.StorageFile.GetFileFromPathAsync(attachment.Path)).OpenReadAsync();var bitmap=new BitmapImage();await bitmap.SetSourceAsync(stream);if(_disposed)return;image.Source=bitmap;_message.Children.Add(image);RequestLayout();}catch(Exception ex){ServiceDiagnostics.Error("图片显示",ex);}}
  var open=new HyperlinkButton{Content="打开附件："+Path.GetFileName(attachment.Path),MinHeight=48};open.Click+=async(_,_)=>{try{await global::Windows.System.Launcher.LaunchFileAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(attachment.Path));}catch(Exception ex){ServiceDiagnostics.Error("打开附件",ex);}};if(!_disposed){_message.Children.Add(open);RequestLayout();}
 }
 public async void ShowNotice(){try{
  GetCursorPos(out var cursor);var area=DisplayArea.GetFromPoint(cursor,DisplayAreaFallback.Nearest);AppWindow.Move(new PointInt32(-32000,-32000));
  if(_appearance.Mask){var mask=new NotificationMaskWindow(_appearance.AlwaysOnTop);if(await mask.ShowMaskAsync(area.OuterBounds)){_mask=mask;_life.Register(()=>mask.Close());WindowPlumbing.OwnNotification(this,mask);}}
  Activate();await _loaded.Task.WaitAsync(TimeSpan.FromSeconds(5));double scale=Root.XamlRoot.RasterizationScale;_scale=scale;_workArea=area.WorkArea;
  var senderProbe=new TextBlock{Text=_notice.Sender,FontSize=_appearance.SenderSize};senderProbe.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity,double.PositiveInfinity));
  var bodyProbe=new TextBlock{Text=_notice.Message,FontSize=_appearance.MessageSize};bodyProbe.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity,double.PositiveInfinity));
  double preferred=Math.Clamp(Math.Max(senderProbe.DesiredSize.Width+48,bodyProbe.DesiredSize.Width+48),360,Math.Min(560,Math.Max(360,_appearance.Width)));
  var size=NotificationLayout.Fit(_appearance with{Width=preferred,Height=_appearance.Height},area.WorkArea.Width/scale,area.WorkArea.Height/scale);
  _limit=size;
  _dialog.Width=size.Width;_dialog.Height=size.Height;
  int w=(int)Math.Round(size.Width*scale),h=(int)Math.Round(size.Height*scale);AppWindow.Resize(new SizeInt32(w,h));
  _dialog.XamlRoot=Root.XamlRoot;_animation.Prepare(_appearance.Animation);
  var opened=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);_dialog.Opened+=(_,_)=>opened.TrySetResult();
  var operation=_dialog.ShowAsync(ContentDialogPlacement.InPlace);await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
  VisualStateManager.GoToState(_dialog,"DialogShowingWithoutSmokeLayer",false);
  if(FindTemplateChild(_dialog,"BackgroundElement") is FrameworkElement surface){surface.Width=size.Width;surface.Height=size.Height;}
  if(FindTemplateChild(_dialog,"BackgroundElement") is FrameworkElement animatedSurface)animatedSurface.Transitions=new TransitionCollection();
  _dialog.UpdateLayout();
  double compactHeight=FitNativeContent(size.Height);
  _dialog.Height=compactHeight;
  if(FindTemplateChild(_dialog,"BackgroundElement") is FrameworkElement compactSurface)compactSurface.Height=compactHeight;
  h=(int)Math.Round(compactHeight*scale);
  try{await TransparentBackdrop.WaitForFrameAsync(Root);}catch(TimeoutException){}
  AppWindow.MoveAndResize(new RectInt32(area.WorkArea.X+(area.WorkArea.Width-w)/2,area.WorkArea.Y+(area.WorkArea.Height-h)/2,w,h));
  _dialog.UpdateLayout();
  await Task.WhenAll(_animation.ShowAsync(_appearance.Animation,_life.Token),_mask?.FadeInAsync(_appearance.Animation,_life.Token)??Task.CompletedTask);_visible=true;if(_layoutAfterShow)RequestLayout();_senderMarquee.Restart();_bodyMarquee.Restart();
  if(_requested.Task.IsCompleted){_dialog.Hide();await operation;return;}
  _clock.Restart();if(!_hold)_timer.Start();if(!_notice.IsPreview)_=_audio.StartAsync(_notice.Kind,_settings.Audio,_notice.Message);
  if(_notice.Kind==NoticePreviewKind.Calling)_pulse.BeginBreathing(_settings.Calling.Breathing,_settings.Calling.BeatsPerMinute);
  await _requested.Task;_timer.Stop();_pulse.Dispose();_audio.Dispose();await Task.WhenAll(_animation.HideAsync(_appearance.Animation,_life.Token),_mask?.FadeOutAsync(_appearance.Animation,_life.Token)??Task.CompletedTask);AppWindow.Hide();_dialog.Hide();await operation;
 }catch(Exception ex){ServiceDiagnostics.Error("通知窗口",ex);}
 finally{_disposed=true;_visible=false;_timer.Stop();SettingsStore.Shared.Changed-=ApplyLiveMaterial;try{_dialog.Hide();}catch(Exception){} _allowClose=true;try{base.Close();}finally{_life.Dispose();_closed.TrySetResult();}}}
 public bool TryUpdate(IncomingNotice notice){
  if(_disposed||_requested.Task.IsCompleted||notice.SourceKey.Length==0||notice.SourceKey!=_notice.SourceKey||notice.Sender!=_notice.Sender||notice.Kind!=_notice.Kind)return false;
  if(notice.Message==_notice.Message)return true;_notice=notice;_audio.Dispose();_audio=new NotificationAudioService();_life.Register(_audio);
  _message.Children.Clear();_message.Children.Add(_bodyMarquee);_bodyMarquee.SetText(notice.Message);_clock.Restart();RequestLayout();if(_settings.Reception.PreviewImages)_=LoadAttachmentAsync();if(_visible&&!notice.IsPreview)_=_audio.StartAsync(notice.Kind,_settings.Audio,notice.Message);return true;
 }
 private void RequestLayout(){
  if(_disposed)return;if(!_visible){_layoutAfterShow=true;return;}if(_layoutPending)return;_layoutAfterShow=false;_layoutPending=true;
  DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,()=>{_layoutPending=false;if(_disposed)return;try{
   _dialog.Height=_limit.Height; if(FindTemplateChild(_dialog,"BackgroundElement") is FrameworkElement surface)surface.Height=_limit.Height;
   _dialog.UpdateLayout();double height=FitNativeContent(_limit.Height);_dialog.Height=height;
   if(FindTemplateChild(_dialog,"BackgroundElement") is FrameworkElement fitted)fitted.Height=height;
   int w=(int)Math.Round(_limit.Width*_scale),h=(int)Math.Round(height*_scale);AppWindow.MoveAndResize(new RectInt32(_workArea.X+(_workArea.Width-w)/2,_workArea.Y+(_workArea.Height-h)/2,w,h));
   _bodyMarquee.Restart();
  }catch(Exception ex){ServiceDiagnostics.Error("通知重排",ex);}});
 }
 private static FrameworkElement? FindTemplateChild(DependencyObject node,string name){
  if(node is FrameworkElement element&&element.Name==name)return element;
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var found=FindTemplateChild(VisualTreeHelper.GetChild(node,i),name);if(found is not null)return found;}
  return null;
 }
 private double FitNativeContent(double limit){
  if(_fittingContent||_disposed)return limit;
  if(FindTemplateChild(_dialog,"ContentScrollViewer") is not ScrollViewer viewport||viewport.ActualHeight<=0||viewport.Content is not Grid container)return limit;
  _fittingContent=true;
  try{
   double titleHeight=0;
   if(FindTemplateChild(_dialog,"Title") is FrameworkElement title&&title.Visibility==Visibility.Visible)
    titleHeight=title.ActualHeight+title.Margin.Top+title.Margin.Bottom;
   double insets=container.Padding.Top+container.Padding.Bottom+container.BorderThickness.Top+container.BorderThickness.Bottom;
   double available=NotificationLayout.ContentHeight(viewport.ActualHeight,insets,titleHeight);
   double width=Math.Max(0,viewport.ActualWidth-container.Padding.Left-container.Padding.Right-container.BorderThickness.Left-container.BorderThickness.Right);
   var constraint=new global::Windows.Foundation.Size(width,double.PositiveInfinity);
   _content.Children[0].Measure(constraint);_content.Children[2].Measure(constraint);
   _messageScroll.MaxHeight=Math.Max(0,available-_content.Children[0].DesiredSize.Height-_content.Children[2].DesiredSize.Height-2*_content.RowSpacing);
   _bodyMarquee.SetViewport(_bodyMarquee.MeasureWrapped(Math.Max(1,width)));
   _message.Measure(constraint);
   _messageScroll.Height=Math.Min(_messageScroll.MaxHeight,_message.DesiredSize.Height);
   _bodyMarquee.SetViewport(Math.Min(_messageScroll.MaxHeight,_bodyMarquee.DesiredSize.Height));
   _duration=Math.Max(_settings.Duration(_notice.Kind),Math.Min(120,(int)Math.Ceiling(_bodyMarquee.FirstPassSeconds)+2));
   ServiceDiagnostics.Set("正文显示",$"接收 {_notice.Message.Length} 字符，内容高度 {_bodyMarquee.DesiredSize.Height:F0}，视口 {_messageScroll.MaxHeight:F0}");
   _content.Height=double.NaN;_content.Measure(constraint);
   return NotificationLayout.MeasureCard(_appearance with{Width=_limit.Width,Height=limit},_workArea.Width/_scale,_workArea.Height/_scale,limit-available,_content.DesiredSize.Height+8).Height;
  }finally{_fittingContent=false;}
 }
 private Style CreateButtonStyle(string nativeStyle,string iconPath){
  if(string.IsNullOrWhiteSpace(iconPath)){var native=new Style(typeof(Button)){BasedOn=(Style)Application.Current.Resources[nativeStyle]};native.Setters.Add(new Setter(Control.FontSizeProperty,_appearance.ButtonSize));return native;}
  string image="";
  if(!string.IsNullOrWhiteSpace(iconPath)&&File.Exists(iconPath)){
   string uri=System.Security.SecurityElement.Escape(new Uri(Path.GetFullPath(iconPath)).AbsoluteUri)!;
   image=$"<Image Source=\"{uri}\" Width=\"20\" Height=\"20\"/>";
  }
  string font=_appearance.ButtonSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
  var template=(DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
   $"<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><StackPanel Orientation=\"Horizontal\" Spacing=\"8\" VerticalAlignment=\"Center\">{image}<TextBlock Text=\"{{Binding}}\" FontSize=\"{font}\" VerticalAlignment=\"Center\"/></StackPanel></DataTemplate>");
  var style=new Style(typeof(Button)){BasedOn=(Style)Application.Current.Resources[nativeStyle]};
  style.Setters.Add(new Setter(Control.FontSizeProperty,_appearance.ButtonSize));
  style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,4,12,4)));
  style.Setters.Add(new Setter(ContentControl.ContentTemplateProperty,template));
  return style;
 }
 private void RequestDismiss(){_audio.Dispose();_requested.TrySetResult();}
 public new void Close()=>RequestDismiss();
 [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool GetCursorPos(out PointInt32 point);
}
