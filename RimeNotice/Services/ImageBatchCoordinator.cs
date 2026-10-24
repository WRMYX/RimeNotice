using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed class ImageBatchCoordinator : IDisposable {
 private readonly object _gate=new();private readonly List<(string Conversation,DateTimeOffset Opened,NoticeImageSession Session)> _sessions=[];
 private Task _tail=Task.CompletedTask;
 public (bool Leader,Task<NoticeAttachmentBatch> Task,NoticeImageSession Session) Register(string source,string sender,string message,string key,DateTimeOffset timestamp,ReceptionOptions options,CancellationToken token,Func<string,CancellationToken,Task<bool>>? validate=null){
  bool imageOnly=Regex.IsMatch(message,@"^\s*(?:\[(?:图片|图像|image|photo)\]|发送了?\s*[一二三四五六七八九十0-9]*\s*张?(?:图片|照片)|图片|照片|🖼|[0-9]+张图片)[\s×x0-9]*$",RegexOptions.IgnoreCase);
  string conversation=source+"\0"+sender;
  lock(_gate){
   foreach(var old in _sessions.Where(s=>s.Session.Completion.IsCompleted).ToArray()){_sessions.Remove(old);old.Session.Dispose();}
   var joined=_sessions.LastOrDefault(s=>imageOnly&&s.Session.IsCollecting&&s.Conversation==conversation&&(DateTimeOffset.UtcNow-s.Opened).TotalMilliseconds<750);
   if(joined.Session is not null){joined.Session.AddSourceKey(key);return(false,joined.Session.FirstBatch,joined.Session);}
   bool ordered=options.InferRecentImages&&NotificationAttachmentResolver.IsImageMessage(message);
   if(ordered)foreach(var old in _sessions)old.Session.CloseAt(timestamp);
   var session=new NoticeImageSession(message,options,timestamp,[key],token,validate,predecessor:ordered?_tail:Task.CompletedTask);
   _sessions.Add((conversation,DateTimeOffset.UtcNow,session));if(ordered)_tail=session.FirstInspection;
   return(true,session.FirstBatch,session);
  }
 }
 public void Dispose(){lock(_gate){foreach(var item in _sessions)item.Session.Dispose();_sessions.Clear();}}
}
