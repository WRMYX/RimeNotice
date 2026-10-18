using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications.Management;
namespace RimeNotice.Services;
// A capacity-one wake signal coalesces bursts. One reader owns notification enumeration.
internal sealed class NotificationChangeWaiter : IDisposable {
 private readonly RimeNotice.Models.CoalescedSignal _signal=new();private UserNotificationListener? _listener;private bool _attempted;
 public bool Subscribed=>_listener is not null;
 public void Signal()=>_signal.Signal();
 public void Configure(bool enabled){
  if(!enabled){Detach();_attempted=false;return;}if(_attempted)return;_attempted=true;
  try{var listener=UserNotificationListener.Current;listener.NotificationChanged+=Changed;_listener=listener;}
  catch(Exception ex){ServiceDiagnostics.Error("通知事件订阅，已回退扫描",ex);}
 }
 private void Changed(UserNotificationListener sender,global::Windows.UI.Notifications.UserNotificationChangedEventArgs args)=>Signal();
 public async Task WaitAsync(TimeSpan timeout,CancellationToken token){await _signal.WaitAsync(timeout,token);}
 private void Detach(){if(_listener is not null){try{_listener.NotificationChanged-=Changed;}catch(Exception ex){ServiceDiagnostics.Error("通知事件释放",ex);}_listener=null;}}
 public void Dispose(){Detach();_signal.Dispose();}
}
