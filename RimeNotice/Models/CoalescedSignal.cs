using System;
using System.Threading;
using System.Threading.Tasks;
namespace RimeNotice.Models;
public sealed class CoalescedSignal:IDisposable {
 private readonly SemaphoreSlim _signal=new(0,1);
 public void Signal(){try{_signal.Release();}catch(SemaphoreFullException){}catch(ObjectDisposedException){}}
 public Task<bool> WaitAsync(TimeSpan timeout,CancellationToken token)=>_signal.WaitAsync(timeout,token);
 public void Dispose()=>_signal.Dispose();
}
