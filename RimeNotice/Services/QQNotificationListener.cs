using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed record IncomingNotice(string Sender,string Message,NoticePreviewKind Kind=NoticePreviewKind.Normal,bool IsPreview=false,
 global::Windows.Storage.Streams.RandomAccessStreamReference? SourceLogo=null,string SourceName="QQ",string AppId="",string SourceKey="") {
 public DateTimeOffset ReceivedAt {get;init;}=DateTimeOffset.Now;
 public DateTimeOffset? CreatedAt {get;init;}
 public Task<NoticeAttachmentBatch>? Attachment {get;init;}
 public NoticeImageSession? ImageSession {get;init;}
}
public sealed class QQNotificationListener {
 public static QQNotificationListener Shared {get;}=new();
 private readonly ImageBatchCoordinator _images=new();
 private readonly CancellationTokenSource _stop=new();private bool _started;private readonly NotificationRuleEngine _rules=new();
 public int QueueOverflowCount {get;private set;}
 public void ReportQueueOverflow(){QueueOverflowCount++;OnUi(()=>StatusChanged?.Invoke());}
 private Action? _rescan;
 public async Task RequestAccessAsync(){await global::Windows.UI.Notifications.Management.UserNotificationListener.Current.RequestAccessAsync();_rescan?.Invoke();}
 public long RuleFilteredCount {get;private set;}public long DeliveredCount {get;private set;}public long ScanCount {get;private set;}
 public long ReceivedCount {get;private set;}public long FilteredCount {get;private set;}
 public global::Windows.Storage.Streams.RandomAccessStreamReference? QQLogo {get;private set;}
 public string LastError {get;private set;}="无";public string AccessStatus {get;private set;}="未检查";public string PackageIdentity {get;private set;}="未打包";
 public string Title {get;private set;}="正在检查通知访问权限";public string Detail {get;private set;}="获得权限后自动接收。";public bool IsRunning {get;private set;}
 public event Action? StatusChanged;public event Action<IncomingNotice>? MessageReceived;
 public string DiagnosticSnapshot()=>$"包身份：{PackageIdentity}\n状态：{Title}\n扫描次数：{ScanCount}\n接收到的通知：{ReceivedCount}\n其他来源过滤：{FilteredCount}\n规则过滤：{RuleFilteredCount}\n已投递：{DeliveredCount}\n队列超限：{QueueOverflowCount}\n设置状态：{SettingsStore.Shared.LastError}\n最近错误：{LastError}\n{ServiceDiagnostics.Snapshot()}\n";
 private DispatcherQueue? _ui;
 private void OnUi(Action action){
  if(_stop.IsCancellationRequested)return;
  void Deliver(){if(_stop.IsCancellationRequested)return;try{action();}catch(Exception ex){CrashLog.Write("接收事件投递",ex);}}
  var queue=_ui;
  if(queue is null){ServiceDiagnostics.Set("接收事件投递","UI 队列尚未初始化");return;}
  if(queue.HasThreadAccess)Deliver();
  else if(!queue.TryEnqueue(Deliver))ServiceDiagnostics.Set("接收事件投递","UI 队列已停止");
 }
 public void ReportPresentationError(Exception error){LastError=$"{error.GetType().Name} / 0x{error.HResult:X8}";OnUi(()=>StatusChanged?.Invoke());}
 public async Task StartAsync(bool requestPermission=true) {
  if(_started)return;
  _ui=DispatcherQueue.GetForCurrentThread()??throw new InvalidOperationException("消息接收必须从窗口 UI 线程启动。");
  _started=true;try{PackageIdentity=global::Windows.ApplicationModel.Package.Current.Id.FullName;}catch(Exception){}
  // The permission prompt needs the UI thread; polling runs on the thread pool so the window stays responsive.
  if(requestPermission)try{var l=global::Windows.UI.Notifications.Management.UserNotificationListener.Current;if(l.GetAccessStatus()==global::Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Unspecified)await l.RequestAccessAsync();}catch(Exception){}
  await Task.Run(PollLoopAsync);
 }
 private async Task PollLoopAsync() {
  using var wake=new NotificationChangeWaiter();_rescan=wake.Signal;
  SettingsStore.Shared.Changed+=wake.Signal;
  try {
  var sdk=new SystemNotificationSource(false);var uia=new UIAutomationMessageSource();
  bool? mode=null;bool initialized=false;var known=new NotificationRevisionTracker();var diagnosticIds=new HashSet<string>();
  while(!_stop.IsCancellationRequested) {
   try {
    var options=SettingsStore.Shared.Current.Reception;
    QQImageIndex.Shared.Configure(options);
    bool compatible=options.UseUIAutomation||PackageIdentity=="未打包";
    wake.Configure(options.EventDriven&&!compatible);
    ServiceDiagnostics.Set("接收方式",compatible?"UIA 扫描（便携版自动兼容或手动开启）":wake.Subscribed?"通知事件 + 30 秒核对":options.EventDriven?"事件订阅失败，回退扫描":"定时扫描");
    if(mode!=compatible){known.Clear();diagnosticIds.Clear();initialized=false;mode=compatible;}
    IMessageSource source=compatible?uia:sdk;var snapshot=await source.PollAsync(_stop.Token);ScanCount++;
    AccessStatus=compatible?"UIA":snapshot.Ready?"Allowed":"Denied";
    if(!snapshot.Ready){known.Clear();diagnosticIds.Clear();initialized=false;SetStatus(snapshot.Status,snapshot.Detail,false);}
    else {
     var ids=snapshot.Notices.Select(n=>n.Key).ToHashSet();
     foreach(var n in snapshot.Notices) {
      if(n.SourceName is "QQ" or "腾讯QQ" && n.Logo is not null)QQLogo=n.Logo;
      bool changed=known.Observe(n.Key,n.Sender,n.Message);
      if(DiagnosticCapture.Active)DiagnosticCapture.Write("notice.observe",new{key=n.Key,mode=compatible?"UIA":"system",initialized,changed,sender=DiagnosticCapture.Text(n.Sender),message=DiagnosticCapture.Text(n.Message),messageLength=n.Message.Length,observedAt=DateTimeOffset.UtcNow,disposition=!initialized?"baseline":!changed?"unchanged":diagnosticIds.Contains(n.Key)?"revised":"new"});
      if(!initialized||!changed)continue;
      if(!NotificationLayout.AcceptSource(n.SourceName,options)){FilteredCount++;if(DiagnosticCapture.Active)DiagnosticCapture.Write("notice.filtered-source",new{key=n.Key});continue;}
      ReceivedCount++;var decision=_rules.Evaluate(n.Sender,n.Message,SettingsStore.Shared.Current);
      if(!decision.Accepted){RuleFilteredCount++;if(DiagnosticCapture.Active)DiagnosticCapture.Write("notice.filtered-rule",new{key=n.Key});continue;}
      DeliveredCount++;ServiceDiagnostics.Set("来源正文",$"QQ 来源 {n.Message.Length} 字符；接口只提供通知内容，可能是摘要。");var notice=new IncomingNotice(n.Sender,n.Message,decision.Kind,false,n.Logo,n.SourceName,n.AppId,n.Key){CreatedAt=n.CreatedAt};
      if(DiagnosticCapture.Active)DiagnosticCapture.Write("notice.accepted",new{key=n.Key,notice.ReceivedAt,notice.CreatedAt,timestampSource=notice.CreatedAt is null?"observed":"system-created",kind=notice.Kind.ToString(),options.PreviewImages,options.InferRecentImages,options.AttachmentWaitSeconds});
      if(options.PreviewImages&&(NotificationAttachmentResolver.IsImageMessage(n.Message)||n.Message.Contains('.'))){
       var batch=_images.Register(n.AppId+"\0"+n.SourceName,n.Sender,n.Message,n.Key,n.CreatedAt??notice.ReceivedAt,options,_stop.Token,NativeImageDecoder.CanDecodeAsync);
       if(!batch.Leader){ServiceDiagnostics.Set("图片消息合并","连续图片通知已加入同批相册；保留原始通知键");continue;}
       notice=notice with{Attachment=batch.Task,ImageSession=batch.Session};
      }
      OnUi(()=>MessageReceived?.Invoke(notice));
     }
     diagnosticIds=ids;known.Retain(ids);initialized=true;
     SetStatus("正在接收 QQ 消息",snapshot.Detail,true);
    }
   }catch(OperationCanceledException)when(_stop.IsCancellationRequested){break;}
   catch(Exception ex){SetStatus("接收暂不可用","会自动重试，请查看调试诊断。",false);ReportPresentationError(ex);}
   try{await wake.WaitAsync(TimeSpan.FromSeconds(wake.Subscribed?30:SettingsStore.Shared.Current.Reception.ScanSeconds),_stop.Token);}catch(OperationCanceledException){break;}
  }
 }
 finally { _rescan=null;SettingsStore.Shared.Changed-=wake.Signal; }
 }
 private void SetStatus(string title,string detail,bool running){if(Title==title&&Detail==detail&&IsRunning==running)return;Title=title;Detail=detail;IsRunning=running;OnUi(()=>StatusChanged?.Invoke());}
 public void Stop(){_stop.Cancel();_images.Dispose();QQImageIndex.Shared.Dispose();}
}
