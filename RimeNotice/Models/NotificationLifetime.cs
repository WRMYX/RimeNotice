using System;
using System.Collections.Generic;
using System.Threading;
namespace RimeNotice.Models;
/// <summary>Owns cancellation and cleanup for one notification; no UI or media dependencies.</summary>
public sealed class NotificationLifetime : IDisposable {
 private readonly object _gate=new();private readonly List<Action> _release=new();private readonly CancellationTokenSource _source=new();
 private readonly Action<Exception>? _error;public CancellationToken Token {get;}public bool IsStopped {get;private set;}
 public NotificationLifetime(Action<Exception>? error=null){_error=error;Token=_source.Token;}
 public void Register(IDisposable resource)=>Register(resource.Dispose);
 public void Register(Action release){lock(_gate){if(!IsStopped){_release.Add(release);return;}}TryRelease(release);}
 private void TryRelease(Action action){try{action();}catch(Exception ex){_error?.Invoke(ex);}}
 public void Dispose(){Action[] release;lock(_gate){if(IsStopped)return;IsStopped=true;release=_release.ToArray();_release.Clear();}
  try{_source.Cancel();}catch(Exception ex){_error?.Invoke(ex);}foreach(var action in release)TryRelease(action);_source.Dispose();
 }
}
