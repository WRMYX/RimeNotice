using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimeNotice.Models;
namespace RimeNotice.Services;
internal sealed record SessionImageCandidate(string Identity,NoticeAttachment Attachment,DateTimeOffset Time,long Length,DateTime Written);
public sealed class NoticeImageSession : IDisposable {
 private readonly object _gate=new();private readonly CancellationTokenSource _stop;private readonly string _message;
 private readonly ReceptionOptions _options;private readonly DateTimeOffset _timestamp,_opened=DateTimeOffset.UtcNow;
 private readonly Func<string,CancellationToken,Task<bool>> _validate;
 private readonly Func<ReceptionOptions,CancellationToken,Task<ImageFileEntry[]>> _snapshot;private readonly bool _realIndex;
 private readonly Dictionary<string,NoticeAttachment> _published=new(StringComparer.OrdinalIgnoreCase);
 private readonly Dictionary<string,DateTimeOffset> _firstSeen=new(StringComparer.OrdinalIgnoreCase);
 private readonly Dictionary<string,(string Path,long Size,DateTime Write,double Since)> _stable=new(StringComparer.OrdinalIgnoreCase);
 private readonly List<string> _keys;private readonly string _id=Guid.NewGuid().ToString("N");
 private readonly TaskCompletionSource<NoticeAttachmentBatch> _first=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private readonly TaskCompletionSource _inspection=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private NoticeAttachmentBatch _current;private DateTimeOffset? _cutoff;private bool _finished,_disposed;
 public event Action<NoticeAttachmentBatch>? Updated;
 public NoticeAttachmentBatch Snapshot {get{lock(_gate)return _current;}}
 public Task<NoticeAttachmentBatch> FirstBatch=>_first.Task;
 public bool IsCollecting {get{lock(_gate)return !_disposed&&!_finished;}}
 public Task FirstInspection=>_inspection.Task;
 public Task Completion {get;}
 public NoticeImageSession(string message,ReceptionOptions options,DateTimeOffset timestamp,string[] keys,CancellationToken token,
  Func<string,CancellationToken,Task<bool>>? validate=null,Func<ReceptionOptions,CancellationToken,Task<ImageFileEntry[]>>? snapshot=null,Task? predecessor=null){
  _message=message;_options=options.Normalize();_timestamp=timestamp;_keys=keys.ToList();_stop=CancellationTokenSource.CreateLinkedTokenSource(token);
  _validate=validate??((p,t)=>Task.Run(()=>NotificationAttachmentResolver.ImageHeaderValid(p),t));
  _realIndex=snapshot is null;_snapshot=snapshot??QQImageIndex.Shared.SnapshotAsync;
  _current=new(_id,[],keys,false,true);
  Completion=Task.Run(()=>RunAsync(predecessor??Task.CompletedTask));
 }
 public void AddSourceKey(string key){lock(_gate){if(!_finished&&!_keys.Contains(key))_keys.Add(key);}}
 public void CloseAt(DateTimeOffset timestamp){lock(_gate){if(_cutoff is null||timestamp<_cutoff)_cutoff=timestamp;}}
 private async Task RunAsync(Task predecessor){
  var clock=Stopwatch.StartNew();using var bound=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);bound.CancelAfter(TimeSpan.FromSeconds(_options.AttachmentWaitSeconds+3));var token=bound.Token;
  try{
   await predecessor.WaitAsync(token);
   if(!_options.PreviewImages||string.IsNullOrWhiteSpace(_options.CacheDirectory)||!NotificationAttachmentResolver.IsImageMessage(_message)&&!_message.Contains('.'))return;
   clock.Restart();double limit=Math.Max(0,_options.AttachmentWaitSeconds-(DateTimeOffset.UtcNow-_opened).TotalSeconds);bool single=limit==0;var initialIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool initial=true;
   if(_realIndex)await QQImageIndex.Shared.ReconcileAsync(_options,token);
   do{
    token.ThrowIfCancellationRequested();
    // Only the first pass reconciles the bounded directory index. Later passes use watcher metadata.
    var entries=await _snapshot(_options,token);
    var candidates=await Task.Run(()=>NotificationAttachmentResolver.SessionCandidates(entries,_options,_message,_timestamp,_opened.AddSeconds(single?3:_options.AttachmentWaitSeconds),_id),token);
    bool wasInitial=initial;
    if(initial){foreach(var c in candidates)initialIds.Add(c.Identity);initial=false;if(candidates.Length==0)_inspection.TrySetResult();}
    if(single)candidates=candidates.Where(c=>initialIds.Contains(c.Identity)).ToArray();
    else if(clock.Elapsed.TotalSeconds>=limit&&!wasInitial)candidates=candidates.Where(c=>_firstSeen.ContainsKey(c.Identity)).ToArray();
    foreach(var c in candidates)if(!_firstSeen.ContainsKey(c.Identity))_firstSeen[c.Identity]=c.Time;
    DateTimeOffset? cutoff;lock(_gate)cutoff=_cutoff;
    candidates=candidates.Where(c=>!c.Attachment.Inferred||_published.ContainsKey(c.Identity)||cutoff is null||_firstSeen[c.Identity]<cutoff).ToArray();
    var chosen=SelectConnected(candidates);
    bool changed=false;
    foreach(var candidate in chosen){
     token.ThrowIfCancellationRequested();
     string identity=candidate.Identity;var attachment=candidate.Attachment;
     if(_published.TryGetValue(identity,out var existing)&&existing.Path.Equals(attachment.Path,StringComparison.OrdinalIgnoreCase))continue;
     var signature=(attachment.Path,candidate.Length,candidate.Written);
     if(!_stable.TryGetValue(identity,out var previous)||previous.Path!=signature.Path||previous.Size!=signature.Length||previous.Write!=signature.Written){_stable[identity]=(signature.Path,signature.Length,signature.Written,clock.Elapsed.TotalSeconds);continue;}
     if(clock.Elapsed.TotalSeconds-previous.Since<.2)continue;
     // Validate after stable metadata, then recheck path and size immediately before publishing.
     if(attachment.IsImage&&!await _validate(attachment.Path,token))continue;
     token.ThrowIfCancellationRequested();var info=new FileInfo(attachment.Path);
     if(!info.Exists||info.Length!=candidate.Length||info.LastWriteTimeUtc!=candidate.Written)continue;
     lock(_gate){if(_disposed||token.IsCancellationRequested)break;
      if(attachment.Inferred&&_cutoff is not null&&_firstSeen[identity]>=_cutoff&&!_published.ContainsKey(identity))continue;
      if(!NotificationAttachmentResolver.ClaimForSession(identity,_id,attachment.Inferred))continue;
      _published[identity]=attachment;changed=true;
     }
    }
    if(changed){Publish(true);_inspection.TrySetResult();}
    if(candidates.Length==0||clock.Elapsed.TotalSeconds>=.2)_inspection.TrySetResult();
    if(single){if(chosen.Length==0||clock.Elapsed.TotalSeconds>=.6)break;}
    else if(clock.Elapsed.TotalSeconds>=limit&&(clock.Elapsed.TotalSeconds>=limit+.6||chosen.All(c=>_published.TryGetValue(c.Identity,out var a)&&a.Path==c.Attachment.Path)))break;
    await Task.Delay(100,token);
   }while(true);
  }catch(OperationCanceledException)when(token.IsCancellationRequested){}
   catch(Exception ex){ServiceDiagnostics.Error("图片收集会话",ex);}
  finally{lock(_gate)_finished=true;Publish(false);_inspection.TrySetResult();_first.TrySetResult(Snapshot);}
 }
 private SessionImageCandidate[] SelectConnected(SessionImageCandidate[] candidates){
  if(candidates.Length==0)return [];
  var exact=candidates.Where(c=>!c.Attachment.Inferred).ToArray();if(exact.Length>0)return exact;
  var ordered=candidates.OrderBy(c=>_firstSeen[c.Identity]).ToArray();
  int seed=Array.FindIndex(ordered,c=>_published.ContainsKey(c.Identity));
  if(seed<0){var nearest=ordered.OrderBy(c=>Math.Abs((_firstSeen[c.Identity]-_timestamp).TotalSeconds)).First();seed=Array.IndexOf(ordered,nearest);}
  int start=seed,end=seed;double gap=_options.Images.BatchGapSeconds;
  while(start>0&&(_firstSeen[ordered[start].Identity]-_firstSeen[ordered[start-1].Identity]).TotalSeconds<=gap)start--;
  while(end+1<ordered.Length&&(_firstSeen[ordered[end+1].Identity]-_firstSeen[ordered[end].Identity]).TotalSeconds<=gap)end++;
  return ordered.Skip(start).Take(end-start+1).ToArray();
 }
 private void Publish(bool waiting){
  NoticeAttachmentBatch next;lock(_gate){
   if(_disposed)return;
   var items=_published.OrderBy(p=>_firstSeen.GetValueOrDefault(p.Key,_timestamp)).ThenBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(p=>p.Value).ToArray();
   next=new(_id,items,_keys.ToArray(),items.Any(a=>a.Inferred),waiting){Revision=_current.Revision+1};_current=next;
  }
  if(next.Items.Length>0)_first.TrySetResult(next);
  if(DiagnosticCapture.Active)DiagnosticCapture.Write("attachment.session",new{batchId=_id,next.Revision,next.IsWaiting,next.SourceKeys,count=next.Items.Length,next.Inferred});
  var handlers=Updated;if(handlers is null)return;
  foreach(Action<NoticeAttachmentBatch> handler in handlers.GetInvocationList())try{handler(next);}catch(Exception ex){ServiceDiagnostics.Error("图片会话更新",ex);}
 }
 public void Cancel(){lock(_gate){if(_finished||_disposed)return;_stop.Cancel();}}
 public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;Updated=null;
   var announced=_current.Items.Select(a=>NotificationAttachmentResolver.FileIdentity(a.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
   foreach(var identity in _published.Keys.Where(id=>!announced.Contains(id)).ToArray()){NotificationAttachmentResolver.ReleaseUnpublished(identity,_id);_published.Remove(identity);}
   _current=_current with{IsWaiting=false};}_stop.Cancel();_first.TrySetResult(Snapshot with{IsWaiting=false});_inspection.TrySetResult();_ = Completion.ContinueWith(_=>_stop.Dispose(),TaskScheduler.Default);}
}
