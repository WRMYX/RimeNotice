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
}
public sealed class QQNotificationListener {
 public static QQNotificationListener Shared {get;}=new();
 private readonly CancellationTokenSource _stop=new();private bool _started;private readonly NotificationRuleEngine _rules=new();
 public long RuleFilteredCount {get;private set;}public long DeliveredCount {get;private set;}public long ScanCount {get;private set;}
 public long ReceivedCount {get;private set;}public long FilteredCount {get;private set;}
 public global::Windows.Storage.Streams.RandomAccessStreamReference? QQLogo {get;private set;}
 public string LastError {get;private set;}="无";public string AccessStatus {get;private set;}="未检查";public string PackageIdentity {get;private set;}="未打包";
 public string Title {get;private set;}="正在检查通知访问权限";public string Detail {get;private set;}="获得权限后自动接收。";public bool IsRunning {get;private set;}
 public event Action? StatusChanged;public event Action<IncomingNotice>? MessageReceived;
 public string DiagnosticSnapshot()=>$"包身份：{PackageIdentity}\n状态：{Title}\n扫描次数：{ScanCount}\n接收到的通知：{ReceivedCount}\n其他来源过滤：{FilteredCount}\n规则过滤：{RuleFilteredCount}\n已投递：{DeliveredCount}\n设置状态：{SettingsStore.Shared.LastError}\n最近错误：{LastError}\n{ServiceDiagnostics.Snapshot()}\n";
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
  using var wake=new NotificationChangeWaiter();
  SettingsStore.Shared.Changed+=wake.Signal;
  try {
  var sdk=new SystemNotificationSource(false);var uia=new UIAutomationMessageSource();
  bool? mode=null;bool initialized=false;var known=new NotificationRevisionTracker();
  while(!_stop.IsCancellationRequested) {
   try {
    var options=SettingsStore.Shared.Current.Reception;
    wake.Configure(options.EventDriven&&!options.UseUIAutomation);
    ServiceDiagnostics.Set("接收方式",options.UseUIAutomation?"UIA 扫描":wake.Subscribed?"通知事件 + 30 秒核对":options.EventDriven?"事件订阅失败，回退扫描":"定时扫描");
    if(mode!=options.UseUIAutomation){known.Clear();initialized=false;mode=options.UseUIAutomation;}
    IMessageSource source=options.UseUIAutomation?uia:sdk;var snapshot=await source.PollAsync(_stop.Token);ScanCount++;
    AccessStatus=options.UseUIAutomation?"UIA":snapshot.Ready?"Allowed":"Denied";
    if(!snapshot.Ready){known.Clear();initialized=false;SetStatus(snapshot.Status,snapshot.Detail,false);}
    else {
     var ids=snapshot.Notices.Select(n=>n.Key).ToHashSet();
     foreach(var n in snapshot.Notices) {
      if(n.SourceName is "QQ" or "腾讯QQ" && n.Logo is not null)QQLogo=n.Logo;
      bool changed=known.Observe(n.Key,n.Sender,n.Message);if(!initialized||!changed)continue;
      if(!NotificationLayout.AcceptSource(n.SourceName,options)){FilteredCount++;continue;}
      ReceivedCount++;var decision=_rules.Evaluate(n.Sender,n.Message,SettingsStore.Shared.Current);
      if(!decision.Accepted){RuleFilteredCount++;continue;}
      DeliveredCount++;var notice=new IncomingNotice(n.Sender,n.Message,decision.Kind,false,n.Logo,n.SourceName,n.AppId,n.Key);OnUi(()=>MessageReceived?.Invoke(notice));
     }
     known.Retain(ids);initialized=true;
     SetStatus("正在接收 QQ 消息",snapshot.Detail,true);
    }
   }catch(OperationCanceledException)when(_stop.IsCancellationRequested){break;}
   catch(Exception ex){SetStatus("接收暂不可用","会自动重试，请查看调试诊断。",false);ReportPresentationError(ex);}
   try{await wake.WaitAsync(TimeSpan.FromSeconds(wake.Subscribed?30:SettingsStore.Shared.Current.Reception.ScanSeconds),_stop.Token);}catch(OperationCanceledException){break;}
  }
 }
 finally { SettingsStore.Shared.Changed-=wake.Signal; }
 }
 private void SetStatus(string title,string detail,bool running){if(Title==title&&Detail==detail&&IsRunning==running)return;Title=title;Detail=detail;IsRunning=running;OnUi(()=>StatusChanged?.Invoke());}
 public void Stop()=>_stop.Cancel();
}
