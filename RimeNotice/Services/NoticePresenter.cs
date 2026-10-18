using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using RimeNotice.Models;
using RimeNotice.Windows;
namespace RimeNotice.Services;
public sealed class NoticePresenter : IDisposable {
 private readonly Func<ElementTheme> _theme;private readonly SemaphoreSlim _gate=new(1,1);private readonly CancellationTokenSource _stop=new();
 private readonly Channel<string> _queue=Channel.CreateBounded<string>(32);private readonly Dictionary<string,IncomingNotice> _pending=new();private NotificationWindow? _active;
 public NoticePresenter(Func<ElementTheme> theme){_theme=theme;_=ReadAsync();}
 private static string Key(IncomingNotice n)=>n.SourceKey.Length>0?n.AppId+"\0"+n.SourceKey+"\0"+n.Sender:Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(n.Sender+"\0"+n.Message)));
 public void Enqueue(IncomingNotice notice){if(_stop.IsCancellationRequested)return;if(_active?.TryUpdate(notice)==true)return;string key=Key(notice);
  if(_pending.ContainsKey(key)){_pending[key]=notice;return;}_pending[key]=notice;if(!_queue.Writer.TryWrite(key)){_pending.Remove(key);ServiceDiagnostics.Set("通知队列","队列已满，未排入新通知");}else ServiceDiagnostics.Set("通知队列",$"等待：{_pending.Count}");
 }
 public Task ShowTextPreviewAsync(string sender,string message)=>ShowAsync(new IncomingNotice(sender,message,NoticePreviewKind.Normal,true));
 public Task ShowPreviewAsync(NoticePreviewKind kind)=>ShowAsync(new IncomingNotice("班级群 · 示例联系人",kind==NoticePreviewKind.Calling?"示例呼叫：请课代表看到消息后及时回应。":kind==NoticePreviewKind.Important?"示例通知：明天上午有考试，请提前准备好文具。":"这是一条示例消息，用于检查独立通知窗口。",kind,true,QQNotificationListener.Shared.QQLogo));
 private async Task ReadAsync(){try{await foreach(string key in _queue.Reader.ReadAllAsync(_stop.Token)){if(!_pending.Remove(key,out var notice))continue;await ShowAsync(notice);}}catch(OperationCanceledException)when(_stop.IsCancellationRequested){}}
 private async Task ShowAsync(IncomingNotice notice){bool entered=false;try{await _gate.WaitAsync(_stop.Token);entered=true;if(_stop.IsCancellationRequested)return;var window=new NotificationWindow(notice,_theme());_active=window;window.ShowNotice();await window.Completion;}catch(OperationCanceledException)when(_stop.IsCancellationRequested){}catch(Exception ex){QQNotificationListener.Shared.ReportPresentationError(ex);}finally{_active=null;if(entered)_gate.Release();}}
 public void Dispose(){_queue.Writer.TryComplete();_stop.Cancel();_active?.Close();_pending.Clear();}
}
