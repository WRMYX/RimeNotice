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
using Microsoft.UI.Xaml.Media.Imaging;
using RimeNotice.Models;
using RimeNotice.Controls;
using RimeNotice.Services;
using Windows.Graphics;
namespace RimeNotice.Windows;
public sealed partial class NotificationWindow : Window,INotificationWindow
{
 private readonly bool _native;
 private readonly bool _fullscreenHost;
 private readonly NotificationInlineMask? _inlineMask;
 private readonly ContentDialog? _dialog;
 private readonly TransparentBackdrop? _nativeBackdrop;
 private TaskCompletionSource _nativeOpened=new(TaskCreationOptions.RunContinuationsAsynchronously),_nativeClosed=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private Task<ContentDialogResult>? _nativeOperation;
 private bool _nativeDialogVisible,_nativeDismissRequested;
 private readonly NotificationGeometryTrace _trace=new();
 private void Trace(string phase)=>_trace.Record(this,Root,_dialog is null?Root:_dialog,_notice.SourceKey,phase);
 private IncomingNotice _notice;private RuleSettings _settings;private AppearanceOptions _appearance;
 private readonly Button _confirm=new(),_closeButton=new();private readonly Grid _commands=new(){ColumnSpacing=8,Padding=new Thickness(16)};private readonly Grid _content=new(){Name="NoticeContent",RowSpacing=8,VerticalAlignment=VerticalAlignment.Top};
 private readonly TextBlock _countdown=new(){Width=92,TextAlignment=TextAlignment.Right,FontSize=12,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,16,0),IsHitTestVisible=false};
 private readonly InfoBar _typeBar;private readonly StackPanel _message=new(){Spacing=12};
 private readonly ScrollViewer _messageScroll;private readonly MarqueeText _bodyMarquee,_senderMarquee;
 private readonly TextBlock _footerText;
 private readonly AutoScrollController _contentScroll;
 private readonly Border _callingAccent=new(){BorderThickness=new Thickness(3),CornerRadius=new CornerRadius(8),IsHitTestVisible=false,Visibility=Visibility.Collapsed,Opacity=0};
 private CallingBreathingEffect? _callingEffect;
 private NotificationImageGallery? _gallery;private NotificationImageWindow? _imageWindow;
 private NoticeImageSession? _imageSession;private Action<NoticeAttachmentBatch>? _imageUpdate;
 private readonly TextBlock _collectionStatus=new(){FontSize=12,TextWrapping=TextWrapping.Wrap};
 private NoticeAttachmentBatch? _batch;private StackPanel? _attachmentPanel;private bool _imagesReady,_outsideFallback;
 private readonly StackPanel _qqLogo=new(){Orientation=Orientation.Horizontal,Spacing=8};
 private readonly Image _developerLogo=new(){Width=64,Height=16,Stretch=Stretch.Uniform};
 private NotificationAudioService _audio=new();private NotificationMaskWindow? _mask;private PendingNoticeWindow? _pendingWindow;
 private IncomingNotice[] _pending=[];private int _pendingCount;
 private readonly NotificationLifetime _life=new(ex=>ServiceDiagnostics.Error("会话释放",ex));
 private CancellationTokenSource _attachmentStop=new();private int _generation;
 private readonly Stopwatch _clock=new();private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromMilliseconds(200)};
 private readonly TaskCompletionSource _loaded=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private TaskCompletionSource _requested=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private readonly TaskCompletionSource _closed=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private bool _waitingImage;private bool _allowClose,_disposed,_fittingContent,_layoutPending,_visible,_layoutAfterShow,_closingAll,_hold,_transitioning,_materialPending;
 private int _duration;private CardSize _limit;private RectInt32 _workArea,_screenArea;private double _scale;
 public Task Completion=>_closed.Task;
 public NotificationWindow(IncomingNotice notice,ElementTheme theme):this(notice,theme,false){}
 internal NotificationWindow(IncomingNotice notice,ElementTheme theme,bool native)
 {
  InitializeComponent();_native=native;_notice=notice;_settings=SettingsStore.Shared.Current;_appearance=_settings.Appearance;
  _fullscreenHost=_native&&NotificationPresentationSession.EffectiveHost(_appearance)==NativeNotificationHost.Fullscreen;
  _duration=_settings.Duration(notice.Kind);_hold=notice.Kind==NoticePreviewKind.Calling&&_settings.Calling.UntilAcknowledged;
  Root.RequestedTheme=_appearance.NotificationTheme switch{1=>ElementTheme.Light,2=>ElementTheme.Dark,_=>theme};
  AppWindow.IsShownInSwitchers=false;
  if(AppWindow.Presenter is OverlappedPresenter p){p.SetBorderAndTitleBar(false,false);p.IsAlwaysOnTop=_appearance.AlwaysOnTop;p.IsResizable=false;p.IsMinimizable=false;p.IsMaximizable=false;}
  WindowPlumbing.RemoveResizableFrame(this);WindowPlumbing.RemoveWindowBorder(this);WindowPlumbing.SetNotificationShadow(this,!_native&&_appearance.Shadow);
  for(int i=0;i<3;i++)_content.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var header=new StackPanel{Spacing=8};
  _senderMarquee=new MarqueeText(notice.Sender,_appearance.SenderSize,string.IsNullOrEmpty(_appearance.SenderFontFamily)?_appearance.FontFamily:_appearance.SenderFontFamily){Height=_appearance.SenderSize*1.6};header.Children.Add(_senderMarquee);_life.Register(_senderMarquee);
  // Use native Title/icon layout. InfoBar.Content is a second template row; placing
  // a compact custom grid there was the cause of the previous empty lower area.
  _typeBar=new InfoBar{IsOpen=true,IsClosable=false,HorizontalAlignment=HorizontalAlignment.Stretch};
  var statusRow=new Grid();statusRow.Children.Add(_typeBar);statusRow.Children.Add(_countdown);header.Children.Add(statusRow);_content.Children.Add(header);
  Typography.SetNumeralAlignment(_countdown,FontNumeralAlignment.Tabular);
  _bodyMarquee=new MarqueeText(notice.Message,_appearance.MessageSize,string.IsNullOrEmpty(_appearance.MessageFontFamily)?_appearance.FontFamily:_appearance.MessageFontFamily,true);_message.Children.Add(_bodyMarquee);_life.Register(_bodyMarquee);
  _bodyMarquee.Name="MessageBody";_bodyMarquee.ExternalViewport=true;
  _messageScroll=new ScrollViewer{Content=_message,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollMode=ScrollMode.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalContentAlignment=VerticalAlignment.Top};Grid.SetRow(_messageScroll,1);_content.Children.Add(_messageScroll);
  _messageScroll.Name="MessageViewport";
  _contentScroll=new AutoScrollController(_messageScroll);_life.Register(_contentScroll);
  var footer=new Grid{Name="NoticeFooter",ColumnSpacing=16};footer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  _footerText=new TextBlock{FontSize=12,TextWrapping=TextWrapping.NoWrap,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};footer.Children.Add(_footerText);
  var source=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,VerticalAlignment=VerticalAlignment.Center};
  _qqLogo.Children.Add(new Image{Width=20,Height=20,Source=new BitmapImage(new Uri("ms-appx:///Assets/QQ.png"))});_qqLogo.Children.Add(new TextBlock{Text="QQ",FontSize=14,VerticalAlignment=VerticalAlignment.Center});source.Children.Add(_qqLogo);
  source.Children.Add(_developerLogo);
  void UpdateLogo(){_developerLogo.Source=new BitmapImage(new Uri(Root.ActualTheme==ElementTheme.Dark?"ms-appx:///Assets/MYXMJY-white.png":"ms-appx:///Assets/MYXMJY-black.png"));ApplyNoticeColors();}
  Root.ActualThemeChanged+=(_,_)=>UpdateLogo();UpdateLogo();Grid.SetColumn(source,1);footer.Children.Add(source);Grid.SetRow(footer,2);_content.Children.Add(footer);
  if(_native){
   header.Children.Remove(_senderMarquee);
   if(_fullscreenHost){_inlineMask=new NotificationInlineMask();Root.Children.Add(_inlineMask);}
   _dialog=NativeNotificationWindow.CreateDialog(_content,_senderMarquee);Root.Children.Add(_dialog);
   if(_fullscreenHost){_dialog.HorizontalAlignment=HorizontalAlignment.Stretch;_dialog.VerticalAlignment=VerticalAlignment.Stretch;_dialog.MaxWidth=double.PositiveInfinity;_dialog.MaxHeight=double.PositiveInfinity;}
   _dialog.Resources["ContentDialogMaxWidth"]=_appearance.Width;
   _dialog.Resources["ContentDialogMinWidth"]=0d;
   _dialog.Resources["ContentDialogMaxHeight"]=_appearance.Height;
   _dialog.Resources["ContentDialogMinHeight"]=0d;
   // The optional RN mask is separate. Keep the native card surfaces opaque,
   // but do not paint a second smoke rectangle across its transparent parent.
   _dialog.Resources["ContentDialogSmokeFill"]=new SolidColorBrush(Microsoft.UI.Colors.Transparent);
   _dialog.Opened+=(_,_)=>{Trace("ContentDialog.Opened");_nativeOpened.TrySetResult();};
   _dialog.PrimaryButtonClick+=(_,e)=>{e.Cancel=true;RequestDismiss();};
   _dialog.CloseButtonClick+=(_,e)=>{e.Cancel=true;CloseAction();};
   _dialog.Closing+=(_,e)=>{Trace("ContentDialog.Closing");_callingEffect?.Stop();if(_hold&&!_nativeDismissRequested&&!_closingAll){e.Cancel=true;CloseAction();}else{_transitioning=true;_visible=false;_timer.Stop();RequestDismiss(false);}};
   _dialog.Closed+=(_,_)=>{Trace("ContentDialog.Closed");_nativeDialogVisible=false;RequestDismiss(false);_nativeClosed.TrySetResult();};
   _nativeBackdrop=new TransparentBackdrop();_nativeBackdrop.Attach(this);WindowPlumbing.EnableTransparency(this);
   WindowPlumbing.ConfigureNativeNotificationHost(this);
  }else {BuildWindowCard();
   Grid.SetRowSpan(_callingAccent,2);_callingAccent.BorderBrush=new SolidColorBrush(Microsoft.UI.Colors.Orange);Root.Children.Add(_callingAccent);

  }
  _callingEffect=new CallingBreathingEffect();_life.Register(_callingEffect);
  WindowPlumbing.UseTransparentNativeBackground(this);
  if(!_native)WindowMaterialService.ApplyNotificationNow(this,Root,_appearance.NotificationMaterial,_appearance.SolidColor);
  Root.Loaded+=(_,_)=>_loaded.TrySetResult();
  _timer.Tick+=(_,_)=>{if(_hold)return;if(_waitingImage){_clock.Stop();_countdown.Text="收集图片…";return;}bool reading=_contentScroll.IsUserReading||(_imageWindow?.IsUserReading??false);
   bool scanning=_settings.Reception.Images.AutoScroll&&new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled&&(!_contentScroll.FirstPassCompleted||!(_imageWindow?.FirstPassCompleted??true));
   if(reading||scanning){_clock.Stop();_countdown.Text=reading?"手动阅读中":"阅读中…";return;}
   _clock.Start();int remain=NotificationLayout.Remaining(_duration,_clock.Elapsed);_countdown.Text=$"{remain} 秒后关闭";if(remain==0)RequestDismiss();};
  AppWindow.Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;RequestDismiss();}};Closed+=(_,_)=>_closed.TrySetResult();
  SettingsStore.Shared.Changed+=ApplyLiveMaterial;ApplyNotice(notice);
 }
 private void BuildWindowCard()
 {
  Root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
  Root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  _content.Margin=new Thickness(24);_content.VerticalAlignment=VerticalAlignment.Stretch;_commands.Style=(Style)Application.Current.Resources["NotificationCommandAreaStyle"];
  _content.RowDefinitions[1].Height=new GridLength(1,GridUnitType.Star);Root.Children.Add(_content);
  _commands.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  _commands.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  _confirm.HorizontalAlignment=_closeButton.HorizontalAlignment=HorizontalAlignment.Stretch;
  _confirm.MinHeight=_closeButton.MinHeight=32;
  _confirm.Click+=(_,_)=>RequestDismiss();_closeButton.Click+=(_,_)=>CloseAction();
  _commands.Children.Add(_confirm);Grid.SetColumn(_closeButton,1);_commands.Children.Add(_closeButton);Grid.SetRow(_commands,1);Root.Children.Add(_commands);
  Root.KeyDown+=(_,e)=>{if(e.Key==global::Windows.System.VirtualKey.Escape){e.Handled=true;CloseAction();}};
 }
 private void CloseAction(){if(_hold){_audio.Dispose();ServiceDiagnostics.Set("呼叫","已静音，等待确认");}else RequestDismiss();}
 private void ApplyNotice(IncomingNotice notice)
 {
  _settings=SettingsStore.Shared.Current;_appearance=_settings.Appearance;
  DetachImageSession();_callingEffect?.Stop();_contentScroll.Stop();_gallery?.Dispose();_gallery=null;_imageWindow?.Dispose();_imageWindow=null;_batch=null;_attachmentPanel=null;_imagesReady=false;_outsideFallback=false;
  _notice=notice;_generation++;
  _contentScroll.Speed=_settings.Reception.Images.ScrollSpeed;_contentScroll.ResumeSeconds=_settings.Reception.Images.ResumeSeconds;_contentScroll.Enabled=_settings.Reception.Images.AutoScroll;
  _nativeDismissRequested=false;_nativeOpened=new(TaskCreationOptions.RunContinuationsAsynchronously);_nativeClosed=new(TaskCreationOptions.RunContinuationsAsynchronously);_nativeOperation=null;
  _senderMarquee.SetTypography(_appearance.SenderSize,string.IsNullOrEmpty(_appearance.SenderFontFamily)?_appearance.FontFamily:_appearance.SenderFontFamily);_senderMarquee.Height=_appearance.SenderSize*1.6;
  _bodyMarquee.SetTypography(_appearance.MessageSize,string.IsNullOrEmpty(_appearance.MessageFontFamily)?_appearance.FontFamily:_appearance.MessageFontFamily);
  _confirm.Content=_appearance.ConfirmText;_confirm.Style=CreateButtonStyle("AccentButtonStyle",_appearance.ConfirmIcon);_closeButton.Style=CreateButtonStyle("DefaultButtonStyle",_appearance.CloseIcon);
_attachmentStop.Cancel();_attachmentStop.Dispose();_attachmentStop=new();
  _audio.Dispose();_audio=new();
  _requested=new(TaskCreationOptions.RunContinuationsAsynchronously);_hold=notice.Kind==NoticePreviewKind.Calling&&_settings.Calling.UntilAcknowledged;_duration=_settings.Duration(notice.Kind);
  _senderMarquee.SetText(notice.Sender);_bodyMarquee.SetText(notice.Message);
  _message.Children.Clear();_message.Children.Add(_bodyMarquee);
  _typeBar.Title=notice.Kind switch{NoticePreviewKind.Calling=>"呼叫提醒",NoticePreviewKind.Important=>"重要消息",_=>"普通消息"};
  _typeBar.Severity=notice.Kind==NoticePreviewKind.Important?InfoBarSeverity.Warning:InfoBarSeverity.Informational;
  _typeBar.IconSource=notice.Kind==NoticePreviewKind.Calling?new FontIconSource{Glyph="\uE717"}:null;
  ApplyNoticeColors();
  _countdown.Text=_hold?"等待确认":$"{_duration} 秒后关闭";
  _footerText.Text=notice.IsPreview&&string.IsNullOrWhiteSpace(_appearance.Footer)?"模拟消息 · 仅供调试":_appearance.Footer;
  _closeButton.Content=_hold?"静音":_appearance.CloseText;
  if(_dialog is not null){_dialog.PrimaryButtonText=_appearance.ConfirmText;_dialog.CloseButtonText=_hold?"静音":_appearance.CloseText;_dialog.PrimaryButtonStyle=_confirm.Style;_dialog.CloseButtonStyle=_closeButton.Style;}
  Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(Root,$"来自 {notice.Sender} 的 QQ 消息");
  _waitingImage=_settings.Reception.PreviewImages&&NotificationAttachmentResolver.IsImageMessage(notice.Message);
  if(_waitingImage&&!_hold)_countdown.Text="等待图片…";
  if(_settings.Reception.PreviewImages)_=LoadAttachmentAsync(_generation,_attachmentStop.Token);
 }
 private void ApplyLiveMaterial()
 {
  if(_disposed)return;if(_transitioning){_materialPending=true;return;}
  var s=SettingsStore.Shared.Current;var a=s.Appearance;
  if(AppWindow.Presenter is OverlappedPresenter p)p.IsAlwaysOnTop=a.AlwaysOnTop;_mask?.SetTopmost(a.AlwaysOnTop);
  int theme=a.NotificationTheme==0?s.Theme:a.NotificationTheme;Root.RequestedTheme=theme switch{1=>ElementTheme.Light,2=>ElementTheme.Dark,_=>ElementTheme.Default};
  if(!_native)WindowMaterialService.ApplyNotificationNow(this,Root,a.NotificationMaterial,a.SolidColor);ApplyNoticeColors();if(_visible){RequestLayout();ShowPending();}
 }
 private void ApplyNoticeColors(){
  var a=SettingsStore.Shared.Current.Appearance;
  _qqLogo.Visibility=a.ShowQQLogo?Visibility.Visible:Visibility.Collapsed;_developerLogo.Visibility=a.ShowDeveloperLogo?Visibility.Visible:Visibility.Collapsed;
  bool highContrast=new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
  if(!_native){bool dark=Root.ActualTheme==ElementTheme.Dark;_commands.Background=new SolidColorBrush(highContrast?new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Background):global::Windows.UI.Color.FromArgb((byte)Math.Round(a.CommandTint*255),dark?(byte)24:(byte)250,dark?(byte)24:(byte)250,dark?(byte)24:(byte)250));}
  string text=_notice.Kind switch{NoticePreviewKind.Important=>a.ImportantColor,NoticePreviewKind.Calling=>a.CallingColor,_=>a.NormalColor};
  _typeBar.Resources.Clear();_typeBar.ClearValue(Control.BackgroundProperty);_countdown.ClearValue(TextBlock.ForegroundProperty);
  if(highContrast||!uint.TryParse(text.TrimStart('#'),System.Globalization.NumberStyles.HexNumber,null,out var color))return;
  var c=global::Windows.UI.Color.FromArgb(255,(byte)(color>>16),(byte)(color>>8),(byte)color);
  var background=new SolidColorBrush(c);var foreground=new SolidColorBrush((.2126*c.R+.7152*c.G+.0722*c.B)>145?Microsoft.UI.Colors.Black:Microsoft.UI.Colors.White);
  foreach(string severity in new[]{"Informational","Warning","Error","Success"}){_typeBar.Resources[$"InfoBar{severity}SeverityBackgroundBrush"]=background;_typeBar.Resources[$"InfoBar{severity}SeverityIconBackground"]=foreground;_typeBar.Resources[$"InfoBar{severity}SeverityIconForeground"]=background;}
  _typeBar.Resources["InfoBarTitleForeground"]=foreground;_typeBar.Resources["InfoBarMessageForeground"]=foreground;_typeBar.Background=background;_countdown.Foreground=foreground;
 }
 private async Task LoadAttachmentAsync(int generation,CancellationToken token)
 {
  try{await LoadAttachmentCoreAsync(generation,token);}catch(OperationCanceledException)when(token.IsCancellationRequested){}catch(Exception ex){ServiceDiagnostics.Error("图片加载",ex);}
  finally{if(generation==_generation&&_imageSession is null&&_waitingImage){_waitingImage=false;if(_visible&&!_requested.Task.IsCompleted)_clock.Restart();}}
 }
 private Task LoadAttachmentCoreAsync(int generation,CancellationToken token)
 {
  var notice=_notice;
  var session=notice.ImageSession??new NoticeImageSession(notice.Message,_settings.Reception,notice.CreatedAt??notice.ReceivedAt,[notice.SourceKey],token,NativeImageDecoder.CanDecodeAsync);
  _imageSession=session;
  _imageUpdate=batch=>DispatcherQueue.TryEnqueue(()=>{
   if(_disposed||generation!=_generation||!ReferenceEquals(_imageSession,session)||token.IsCancellationRequested||_requested.Task.IsCompleted)return;
   AcceptImageBatch(batch);
  });
  session.Updated+=_imageUpdate;AcceptImageBatch(session.Snapshot);
  return Task.CompletedTask;
 }
 private void AcceptImageBatch(NoticeAttachmentBatch batch){
  if(_batch is not null&&_batch.BatchId==batch.BatchId&&batch.Revision<=_batch.Revision)return;
  _batch=batch;_imagesReady=batch.Items.Length>0||NotificationAttachmentResolver.IsImageMessage(_notice.Message);_waitingImage=batch.IsWaiting&&NotificationAttachmentResolver.IsImageMessage(_notice.Message);
  if(!_transitioning){RenderAttachments();RequestLayout();}

 }
 private void DetachImageSession(){
  var session=_imageSession;_imageSession=null;if(session is null)return;
  if(_imageUpdate is not null)session.Updated-=_imageUpdate;_imageUpdate=null;session.Dispose();
 }
 private void RenderAttachments()
 {
  if(!_imagesReady||_batch is null)return;
  _collectionStatus.Text=_batch.Items.Length==0?(_batch.IsWaiting?"正在收集图片…":"未能关联图片，请在接收兼容中检测 QQ 图片目录。") : (_batch.Inferred?"推测图片批次 · 按文件时间关联，可能不准确":"明确附件")+(_batch.IsWaiting?" · 正在补充图片…":"");
  if(_attachmentPanel is not null){_gallery?.ApplyBatch(_batch);_imageWindow?.ApplyBatch(_batch);return;}
  _attachmentPanel=new StackPanel{Spacing=8};_attachmentPanel.Children.Add(_collectionStatus);_message.Children.Add(_attachmentPanel);
  _gallery=new NotificationImageGallery(_batch,_messageScroll);_attachmentPanel.Children.Add(_gallery);
  foreach(var attachment in _batch.Items){if(attachment.IsImage)continue;
   var open=new HyperlinkButton{Content="打开附件："+Path.GetFileName(attachment.Path),MinHeight=36};
   open.Click+=async(_,_)=>{try{if(!await global::Windows.System.Launcher.LaunchFileAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(attachment.Path)))throw new IOException("系统未打开附件");}catch(Exception ex){ServiceDiagnostics.Error("打开附件",ex);open.Content="无法打开附件，文件可能已移动或删除";}};_attachmentPanel.Children.Add(open);
  }
 }
 private void PositionImages()
 {
  if(_requested.Task.IsCompleted||!_visible||_transitioning||_batch is null||_batch.Items.Length==0||!System.Linq.Enumerable.Any(_batch.Items,a=>a.IsImage)||_settings.Reception.Images.Placement!=ImagePlacement.Outside)return;
  var card=CardAnchor()??new RectInt32(AppWindow.Position.X,AppWindow.Position.Y,AppWindow.Size.Width,AppWindow.Size.Height);
  var bounds=ImagePreviewLayout.Beside(new(card.X,card.Y,card.Width,card.Height),new(_workArea.X,_workArea.Y,_workArea.Width,_workArea.Height),_scale,_settings.Reception.Images.PanelWidth,_settings.Reception.Images.Side);
  if(bounds is null){ServiceDiagnostics.Set("外部图片","所选一侧空间不足，回退通知内显示");
   if(!_outsideFallback){_outsideFallback=true;_attachmentPanel?.Children.Add(new TextBlock{Text="所选一侧空间不足，图片改在通知内显示。",FontSize=12,TextWrapping=TextWrapping.Wrap});RequestLayout();}return;}
  if(_imageWindow is null){
   _imageWindow=new NotificationImageWindow(_batch,Root.ActualTheme,_settings.Reception.Images,_appearance);
   if(_gallery is not null){_gallery.Dispose();_attachmentPanel?.Children.Remove(_gallery);_gallery=null;}
   _attachmentPanel?.Children.Add(new TextBlock{Text=_settings.Reception.Images.Side==ImageSide.Left?"图片在左侧显示":"图片在右侧显示",FontSize=12});RequestLayout();
  }
  _imageWindow.ShowBeside(this,bounds.Value);
 }
 public async void ShowNotice(Func<IncomingNotice?>? takeNext=null)
 {
  try{
   GetCursorPos(out var cursor);var area=DisplayArea.GetFromPoint(cursor,DisplayAreaFallback.Nearest);
   AppWindow.Move(new PointInt32(area.WorkArea.X,area.WorkArea.Y));
   _scale=Math.Max(96u,GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)))/96d;_workArea=area.WorkArea;_screenArea=area.OuterBounds;
   if(_native)ServiceDiagnostics.Set("原生父窗口方案",_fullscreenHost?"全屏父窗口＋同窗口遮罩（实验）":_appearance.NativeHost==NativeNotificationHost.Fullscreen?"未开启遮罩，暂用紧凑父窗口":"紧凑父窗口＋可选独立遮罩");
   bool first=true;
   while(!_closingAll){
    _visible=false;_transitioning=true;_layoutPending=false;
    var audio=_audio;var audioReady=_notice.IsPreview?Task.CompletedTask:audio.PrepareAsync(_notice.Kind,_settings.Audio);
    bool frameReady;
    if(_fullscreenHost&&!first){MeasureAndPosition();Root.UpdateLayout();await TransparentBackdrop.WaitForFrameAsync(Root,_life.Token);frameReady=true;}
    else frameReady=await PrepareHiddenFrameAsync();
    if(_closingAll||_requested.Task.IsCompleted)break;
    if(first&&_appearance.Mask&&!_fullscreenHost){var mask=new NotificationMaskWindow(_appearance.AlwaysOnTop);if(await mask.ShowMaskAsync(area.OuterBounds)){_mask=mask;WindowPlumbing.OwnNotification(this,mask);}}
    var maskEntrance=first&&_inlineMask is not null?_inlineMask.FadeAsync(true,_life.Token):first&&_mask is not null?_mask.FadeInAsync(true,_life.Token):Task.CompletedTask;
    if(!_notice.IsPreview)_=StartPreparedAudioAsync(audio,audioReady,_notice,_settings.Audio,_generation);
    Trace("before-native-show");
    if(_native)AppWindow.Show(false);
    if(_dialog is not null){
     _dialog.XamlRoot=Root.XamlRoot;_dialog.ApplyTemplate();Trace("before-ContentDialog.ShowAsync");
     _nativeDialogVisible=true;_nativeOperation=_dialog.ShowAsync(ContentDialogPlacement.InPlace).AsTask();
     if(await Task.WhenAny(_nativeOpened.Task,_nativeOperation)==_nativeOperation)await _nativeOperation;
     ServiceDiagnostics.Set("通知入场动画","原生 ContentDialog；画面待验收");
    }else if(!frameReady){
     // A rendering timeout must not discard the user's message or count as a successful animation.
     AppWindow.Show(false);await _loaded.Task.WaitAsync(TimeSpan.FromSeconds(5),_life.Token);
     _scale=Root.XamlRoot.RasterizationScale;MeasureAndPosition();
     ServiceDiagnostics.Set("通知入场动画","首帧准备超时；已普通显示，动画未通过");
    }else{
     try{await MaterialNotificationAnimation.PlayAsync(this,Root,_appearance,_screenArea,true,_attachmentStop.Token);}
     catch(OperationCanceledException)when(_requested.Task.IsCompleted){}
    }
    Trace("after-native-show");
    await maskEntrance;
    first=false;_transitioning=false;_visible=true;
    if(_materialPending){_materialPending=false;ApplyLiveMaterial();}
    if(_layoutAfterShow){_layoutAfterShow=false;MeasureAndPosition();}
    RenderAttachments();MeasureAndPosition();Root.UpdateLayout();PositionImages();
    _contentScroll.Start();
    if(_notice.Kind==NoticePreviewKind.Calling&&_settings.Calling.Breathing){if(_dialog is not null)_callingEffect?.StartNative(_dialog,_settings.Calling.BeatsPerMinute);else _callingEffect?.StartMaterial(_callingAccent,_settings.Calling.BeatsPerMinute);}
    ShowPending();_senderMarquee.Restart();_bodyMarquee.Restart();
    Activate();WindowPlumbing.BringToForeground(this);if(_dialog is not null)_dialog.Focus(FocusState.Programmatic);else _confirm.Focus(FocusState.Programmatic);
    if(!_requested.Task.IsCompleted){_clock.Restart();if(!_hold)_timer.Start();}
    if(_nativeOperation is not null){if(await Task.WhenAny(_requested.Task,_nativeOperation)==_nativeOperation)await _nativeOperation;}else await _requested.Task;
    _callingEffect?.Stop();_contentScroll.Stop();_imageWindow?.Dispose();_imageWindow=null;
    _visible=false;_transitioning=true;_timer.Stop();_audio.Dispose();_pendingWindow?.Hide();
    Trace("before-native-hide");
    if(_nativeOperation is not null){await _nativeOperation;await _nativeClosed.Task;Trace("ContentDialog.ShowAsync-and-Closed-completed");ServiceDiagnostics.Set("通知退场动画","原生操作及 Closed 已返回；画面待验收");}
    else{await MaterialNotificationAnimation.PlayAsync(this,Root,_appearance,_screenArea,false,_life.Token);AppWindow.Hide();}
    Trace("after-native-hide");
    _transitioning=false;
    var next=_closingAll?null:takeNext?.Invoke();
    if(next is null){if(_inlineMask is not null)await _inlineMask.FadeAsync(false,_life.Token);if(_mask is not null)await _mask.FadeOutAsync(true,_life.Token);break;}
    ApplyNotice(next);
   }
  }catch(Exception ex){if(DiagnosticCapture.Active)DiagnosticCapture.Write("window.exception-cleanup",new{key=_notice.SourceKey,error=ex.GetType().Name,ex.HResult});ServiceDiagnostics.Error("通知窗口",ex);QQNotificationListener.Shared.ReportPresentationError(ex);}
  finally{
   Trace("finally-cleanup");_disposed=true;_visible=false;_timer.Stop();_attachmentStop.Cancel();_attachmentStop.Dispose();_audio.Dispose();
   DetachImageSession();SettingsStore.Shared.Changed-=ApplyLiveMaterial;_pendingWindow?.Close();_gallery?.Dispose();_imageWindow?.Dispose();
   _allowClose=true;try{AppWindow.Hide();base.Close();}finally{_mask?.Close();_life.Dispose();_closed.TrySetResult();}
  }
 }
 private async Task<bool> PrepareHiddenFrameAsync()
 {
  // Prepare only this real HWND. No second host, animated opacity, scale or position.
  WindowPlumbing.CloakNotification(this,true);
  try{
   if(!_native)WindowMaterialService.ApplyNotificationNow(this,Root,_appearance.NotificationMaterial,_appearance.SolidColor);
   MeasureAndPosition();AppWindow.Show(false);
   await _loaded.Task.WaitAsync(TimeSpan.FromSeconds(5),_life.Token);_scale=Root.XamlRoot.RasterizationScale;
   if(_nativeBackdrop is not null&&!await _nativeBackdrop.Ready.WaitAsync(TimeSpan.FromSeconds(5),_life.Token))throw new InvalidOperationException("原生弹窗的透明父窗口未就绪；未切换为其他样式。");
   if(_native)WindowPlumbing.ConfigureNativeNotificationHost(this);
   MeasureAndPosition();_layoutAfterShow=false;Root.UpdateLayout();
   await TransparentBackdrop.WaitForFrameAsync(Root,_life.Token);
   if(_layoutAfterShow){_layoutAfterShow=false;MeasureAndPosition();Root.UpdateLayout();await TransparentBackdrop.WaitForFrameAsync(Root,_life.Token);}
   Trace("prepared-cloaked-frame");
   ServiceDiagnostics.Set("通知首帧准备","布局及 Rendering 事件已完成；画面仍需验收");
   return true;
  }catch(TimeoutException){
   if(_native)throw;
   ServiceDiagnostics.Set("通知首帧准备","超时；回退普通显示，不报告动画成功");
   Trace("cloaked-frame-timeout");return false;
  }finally{
   AppWindow.Hide();WindowPlumbing.CloakNotification(this,false);
  }
 }
 private async Task StartPreparedAudioAsync(NotificationAudioService audio,Task ready,IncomingNotice notice,AudioOptions options,int generation)
 {
  try{await ready;if(_disposed||generation!=_generation||_requested.Task.IsCompleted)return;await audio.StartAsync(notice.Kind,options,AudioMixPolicy.Text(notice.Sender,notice.Message,options));}
  catch(Exception ex){ServiceDiagnostics.Error("通知音频启动",ex);}
 }
 private void MeasureAndPosition([System.Runtime.CompilerServices.CallerMemberName]string reason="")
 {
  Trace("measure-begin:"+reason);
  var probe=new TextBlock{Text=_notice.Message,FontSize=_appearance.MessageSize};probe.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity,double.PositiveInfinity));
  double preferred=Math.Clamp(Math.Max(_senderMarquee.NaturalWidth+48,probe.DesiredSize.Width+48),360,Math.Min(560,Math.Max(360,_appearance.Width)));
  _limit=NotificationLayout.Fit(_appearance with{Width=preferred},_workArea.Width/_scale,_workArea.Height/_scale);
  double height=_native?FitNativeContent(_limit.Height):FitWindowContent(_limit.Height);
  double padX=_native?Math.Max(16,_limit.Width*.025):0,padY=_native?Math.Max(16,height*.025):0;
  if(_dialog is not null){_dialog.Width=_fullscreenHost?double.NaN:_limit.Width;_dialog.Height=double.NaN;}
  int w=(int)Math.Ceiling((_limit.Width+2*padX)*_scale),h=(int)Math.Ceiling((height+2*padY)*_scale);
  var bounds=_fullscreenHost?_screenArea:new RectInt32(_workArea.X+(_workArea.Width-w)/2,_workArea.Y+(_workArea.Height-h)/2,w,h);
  if(AppWindow.Position.X!=bounds.X||AppWindow.Position.Y!=bounds.Y||AppWindow.Size.Width!=bounds.Width||AppWindow.Size.Height!=bounds.Height){Trace("before-MoveAndResize:"+reason);AppWindow.MoveAndResize(bounds);Trace("after-MoveAndResize:"+reason);}
 }
 public void SetPending(IncomingNotice[] pending,int count){_pending=pending;_pendingCount=count;if(_visible)ShowPending();}
 private void ShowPending(){if(_disposed)return;if(_pendingCount==0){_pendingWindow?.Hide();return;}_pendingWindow??=new PendingNoticeWindow(Root.ActualTheme);_pendingWindow.Update(_pending,_pendingCount,this,_workArea,_scale,SettingsStore.Shared.Current.Appearance.AlwaysOnTop,CardAnchor(),_fullscreenHost);}
 private RectInt32? CardAnchor(){if(_dialog is null)return null;var card=NativeNotificationWindow.FindPart(_dialog,"BackgroundElement");if(card is null||card.ActualWidth<=0)return null;var rect=card.TransformToVisual(Root).TransformBounds(new global::Windows.Foundation.Rect(0,0,card.ActualWidth,card.ActualHeight));return new RectInt32(AppWindow.Position.X+(int)Math.Round(rect.X*_scale),AppWindow.Position.Y+(int)Math.Round(rect.Y*_scale),(int)Math.Ceiling(rect.Width*_scale),(int)Math.Ceiling(rect.Height*_scale));}
 private void RequestLayout()
 {
  if(_disposed||_requested.Task.IsCompleted)return;if(DiagnosticCapture.Active)DiagnosticCapture.Write("window.layout-request",new{key=_notice.SourceKey,_visible,_generation});if(!_visible||_transitioning){_layoutAfterShow=true;return;}if(_layoutPending)return;_layoutPending=true;int generation=_generation;
  DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,()=>{_layoutPending=false;if(_disposed||_requested.Task.IsCompleted||generation!=_generation)return;if(!_visible||_transitioning){_layoutAfterShow=true;return;}try{double offset=_messageScroll.VerticalOffset;RenderAttachments();MeasureAndPosition();Root.UpdateLayout();_contentScroll.RestoreOffset(offset);PositionImages();_contentScroll.Start(false);ShowPending();}catch(Exception ex){ServiceDiagnostics.Error("通知重排",ex);}});
 }
 private double FitNativeContent(double limit)
 {
  if(_fittingContent||_disposed)return limit;
  _fittingContent=true;
  try{
   if(_dialog is null||Root.XamlRoot is null)return limit; // Provisional hidden HWND size only, never fixes Dialog.Height.
   _dialog.XamlRoot=Root.XamlRoot;_dialog.Height=double.NaN;_dialog.Width=_fullscreenHost?double.NaN:_limit.Width;
   _dialog.ApplyTemplate();
   var card=NativeNotificationWindow.FindPart(_dialog,"BackgroundElement")??throw new InvalidOperationException("原生模板没有 BackgroundElement，无法测量，未替换模板。");
   // Read padding from the real template content surface instead of estimating title/buttons.
   var nativeScroll=NativeNotificationWindow.FindPart(_dialog,"ContentScrollViewer") as ScrollViewer;
   var padding=nativeScroll?.Content is Grid surface?surface.Padding:new Thickness(24);
   double width=Math.Max(1,_limit.Width-padding.Left-padding.Right);
   _content.Width=width;_senderMarquee.MaxWidth=width;
   _messageScroll.Height=0;_messageScroll.MaxHeight=0;_bodyMarquee.SetViewport(0);
   var constraint=new global::Windows.Foundation.Size(width,double.PositiveInfinity);
   var cardConstraint=new global::Windows.Foundation.Size(_limit.Width,double.PositiveInfinity);
   _content.InvalidateMeasure();card.InvalidateMeasure();card.Measure(cardConstraint);
   double fixedHeight=card.DesiredSize.Height;
   if(!double.IsFinite(fixedHeight)||fixedHeight<=0)throw new InvalidOperationException("原生模板测量未就绪，未回退到估算高度；请提供调试采样。");
   double natural=_bodyMarquee.MeasureWrapped(width),extra=0;
   for(int i=1;i<_message.Children.Count;i++){_message.Children[i].Measure(constraint);extra+=_message.Children[i].DesiredSize.Height+_message.Spacing;}
   var fit=NotificationLayout.FitNativeViewport(limit,fixedHeight,natural,extra);
   _bodyMarquee.SetViewport(natural);_messageScroll.MaxHeight=fit.ScrollHeight;_messageScroll.Height=fit.ScrollHeight;
   _content.InvalidateMeasure();card.InvalidateMeasure();card.Measure(cardConstraint);
   double measuredHeight=card.DesiredSize.Height;
   _duration=_settings.Duration(_notice.Kind); // Countdown starts after the first automatic reading pass.
   ServiceDiagnostics.Set("正文显示",$"原生模板实测：固定区 {fixedHeight:F0}，卡片 {measuredHeight:F0}，正文 {natural:F0}/视口 {fit.BodyHeight:F0}；高度自动。");
   if(DiagnosticCapture.Active)DiagnosticCapture.Write("window.native-measure",new{key=_notice.SourceKey,fixedHeight,measuredHeight,width,limit,natural,extra,fit.BodyHeight,fit.ScrollHeight,fullscreen=_fullscreenHost});
   return Math.Min(limit,Math.Max(1,measuredHeight));
  }finally{_fittingContent=false;}
 }
 private double FitWindowContent(double limit)
 {
  if(_fittingContent||_disposed)return limit;
  _fittingContent=true;
  try{
   double width=Math.Max(1,_limit.Width-_content.Margin.Left-_content.Margin.Right);
   _commands.Measure(new global::Windows.Foundation.Size(_limit.Width,double.PositiveInfinity));
   double chrome=_commands.DesiredSize.Height+_content.Margin.Top+_content.Margin.Bottom;
   var constraint=new global::Windows.Foundation.Size(width,double.PositiveInfinity);
   _content.Children[0].Measure(constraint);_content.Children[2].Measure(constraint);
   double fixedContent=_content.Children[0].DesiredSize.Height+_content.Children[2].DesiredSize.Height+2*_content.RowSpacing;
   double natural=_bodyMarquee.MeasureWrapped(width),extra=0;
   for(int i=1;i<_message.Children.Count;i++){_message.Children[i].Measure(constraint);extra+=_message.Children[i].DesiredSize.Height+_message.Spacing;}
   double available=Math.Max(1,limit-chrome-fixedContent);
   double bodyViewport=Math.Min(natural,Math.Max(1,available-Math.Min(extra,available/3)));
   _bodyMarquee.SetViewport(natural);
   _messageScroll.MaxHeight=available;_messageScroll.Height=Math.Min(available,natural+extra);
   _duration=_settings.Duration(_notice.Kind); // Countdown starts after the first automatic reading pass.
   ServiceDiagnostics.Set("正文显示",$"整窗卡片：正文 {_notice.Message.Length} 字符；内容高度 {natural:F0}，正文视口 {bodyViewport:F0}；无外围材质留白。");
   return Math.Min(limit,Math.Max(180,chrome+fixedContent+_messageScroll.Height));
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
 private void RequestDismiss(bool hideNative=true){if(_disposed)return;bool firstRequest=!_nativeDismissRequested;DetachImageSession();_callingEffect?.Stop();_contentScroll.Stop();_imageWindow?.Dispose();_imageWindow=null;_attachmentStop.Cancel();_audio.Dispose();_nativeDismissRequested=true;_requested.TrySetResult();if(hideNative&&firstRequest&&_nativeDialogVisible)_dialog?.Hide();}
 public new void Close(){_closingAll=true;RequestDismiss();}
 [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool GetCursorPos(out PointInt32 point);
 [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr window);
}
