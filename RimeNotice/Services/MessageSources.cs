using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.Storage.Streams;
namespace RimeNotice.Services;
public sealed record SourceNotice(string Key,string Sender,string Message,string SourceName,string AppId,RandomAccessStreamReference? Logo=null) { public DateTimeOffset? CreatedAt {get;init;} }
public sealed record SourceSnapshot(bool Ready,string Status,string Detail,IReadOnlyList<SourceNotice> Notices) {
 public string? AccessStatus {get;init;}
}
public interface IMessageSource { Task<SourceSnapshot> PollAsync(CancellationToken token); }
public sealed class SystemNotificationSource : IMessageSource {
 public async Task<SourceSnapshot> PollAsync(CancellationToken token) {
  token.ThrowIfCancellationRequested();
  try {
  var listener=UserNotificationListener.Current;
  var access=listener.GetAccessStatus();ServiceDiagnostics.Set("通知权限",access.ToString());
  // Try the API in both distributions. Package identity is diagnostic information, not a precondition here.
  if(access!=UserNotificationListenerAccessStatus.Allowed)return new(false,
   access==UserNotificationListenerAccessStatus.Denied?"通知访问未获允许":"等待通知访问授权",
   access==UserNotificationListenerAccessStatus.Denied?"可尝试一次授权；若仍被拒绝，请在 Windows 设置允许访问，或选择 UIA 兼容接收。":"点击下方按钮请求通知访问。授权由 Windows 决定；失败时可手动选择 UIA 兼容接收。",[]){AccessStatus=access.ToString()};
  var notifications=await listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(token);token.ThrowIfCancellationRequested();
  var result=new List<SourceNotice>();
  foreach(var n in notifications.OrderBy(n=>n.CreationTime))try {
   string source=n.AppInfo.DisplayInfo.DisplayName;
   if(!RimeNotice.Models.NotificationLayout.AcceptSource(source,new()))continue;
   var binding=n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);if(binding is null)continue;
   var rawTexts=binding.GetTextElements().Select(t=>t.Text).ToArray();
   if(DiagnosticCapture.Active)DiagnosticCapture.Write("source.raw",new{mode="system",key=source+":"+n.Id.ToString()+":"+n.CreationTime.UtcTicks,creationTime=n.CreationTime,observedAt=DateTimeOffset.UtcNow,texts=rawTexts.Select(t=>t is null?null:DiagnosticCapture.Text(t)).ToArray(),lengths=rawTexts.Select(t=>t?.Length??0).ToArray()});
   var texts=rawTexts.Where(t=>!string.IsNullOrWhiteSpace(t)).ToArray();if(texts.Length==0)continue;
   string name=n.AppInfo.DisplayInfo.DisplayName;RandomAccessStreamReference? logo=null;
   try{logo=n.AppInfo.DisplayInfo.GetLogo(new global::Windows.Foundation.Size(32,32));}catch(Exception ex){ServiceDiagnostics.Error("系统图标",ex);}
   result.Add(new(name+":"+n.Id.ToString()+":"+n.CreationTime.UtcTicks,texts[0]!,string.Join("\n",texts.Skip(1)),name,n.AppInfo.AppUserModelId,logo){CreatedAt=n.CreationTime});
  }catch(Exception ex){ServiceDiagnostics.Error("系统通知解析",ex);}
  return new(true,"系统通知接口已就绪","已获授权并完成通知读取；等待新的 QQ 通知。关闭设置窗口后仍继续接收。",result){AccessStatus="Allowed"};
  }catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
  catch(Exception ex){ServiceDiagnostics.Error("系统通知接口",ex);return new(false,"系统通知接口暂不可用",$"{ex.GetType().Name} / 0x{ex.HResult:X8}。可尝试授权或选择 UIA 兼容接收；详情见调试诊断。",[]){AccessStatus="Error"};}
 }
}
