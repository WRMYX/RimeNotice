using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.Storage.Streams;
namespace RimeNotice.Services;
public sealed record SourceNotice(string Key,string Sender,string Message,string SourceName,string AppId,RandomAccessStreamReference? Logo=null);
public sealed record SourceSnapshot(bool Ready,string Status,string Detail,IReadOnlyList<SourceNotice> Notices);
public interface IMessageSource { Task<SourceSnapshot> PollAsync(CancellationToken token); }
public sealed class SystemNotificationSource(bool requestPermission) : IMessageSource {
 private bool _checked;
 public async Task<SourceSnapshot> PollAsync(CancellationToken token) {
  token.ThrowIfCancellationRequested();
  try{_=Package.Current.Id;}catch(Exception){return new(false,"需要安装版以访问通知","系统通知接收需要已安装的 RN。",[]);}
  var listener=UserNotificationListener.Current;
  if(!_checked){_checked=true;if(requestPermission&&listener.GetAccessStatus()==UserNotificationListenerAccessStatus.Unspecified)await listener.RequestAccessAsync();}
  string access=listener.GetAccessStatus().ToString();ServiceDiagnostics.Set("通知权限",access);
  if(listener.GetAccessStatus()!=UserNotificationListenerAccessStatus.Allowed)return new(false,"等待通知访问权限","请在 Windows 设置中允许通知访问。",[]);
  var notifications=await listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(token);token.ThrowIfCancellationRequested();
  var result=new List<SourceNotice>();
  foreach(var n in notifications)try {
   string source=n.AppInfo.DisplayInfo.DisplayName;
   if(!RimeNotice.Models.NotificationLayout.AcceptSource(source,new()))continue;
   var binding=n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);if(binding is null)continue;
   var texts=binding.GetTextElements().Select(t=>t.Text).Where(t=>!string.IsNullOrWhiteSpace(t)).ToArray();if(texts.Length==0)continue;
   string name=n.AppInfo.DisplayInfo.DisplayName;RandomAccessStreamReference? logo=null;
   try{logo=n.AppInfo.DisplayInfo.GetLogo(new global::Windows.Foundation.Size(32,32));}catch(Exception ex){ServiceDiagnostics.Error("系统图标",ex);}
   result.Add(new(name+":"+n.Id.ToString(),texts[0]!,string.Join("\n",texts.Skip(1)),name,n.AppInfo.AppUserModelId,logo));
  }catch(Exception ex){ServiceDiagnostics.Error("系统通知解析",ex);}
  return new(true,"正在接收通知","关闭设置窗口后仍继续接收。消息不在 RN 中保存。",result);
 }
}
