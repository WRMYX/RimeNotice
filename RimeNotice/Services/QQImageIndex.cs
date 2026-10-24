using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed record ImageFileEntry(string Path,long Length,DateTime WrittenUtc,DateTime CreatedUtc,DateTimeOffset? ObservedAt);
// Metadata only. Watch before scanning so files arriving during initialization are retained.
public sealed class QQImageIndex : IDisposable
{
 public static QQImageIndex Shared {get;}=new();
 private readonly object _gate=new();private readonly Dictionary<string,ImageFileEntry> _files=new(StringComparer.OrdinalIgnoreCase);
 private FileSystemWatcher? _watcher;private string _root="";private int _generation;private bool _disposed;private Task _scan=Task.CompletedTask;
 public long EventCount {get;private set;}public string Status {get;private set;}="未启动";
 public void Configure(ReceptionOptions options)
 {
  string root="";try{if(options.PreviewImages&&!string.IsNullOrWhiteSpace(options.CacheDirectory))root=NotificationAttachmentResolver.DirectoryFor(options);}catch(Exception ex){ServiceDiagnostics.Error("图片目录",ex);}
  lock(_gate){
   if(_disposed||root.Equals(_root,StringComparison.OrdinalIgnoreCase)&&_watcher is not null)return;
   _watcher?.Dispose();_watcher=null;_root=root;_files.Clear();int generation=++_generation;EventCount=0;
   if(string.IsNullOrEmpty(root)||!Directory.Exists(root)){Status="图片预览关闭或目录不存在";return;}
   try{
    _watcher=new FileSystemWatcher(root){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.CreationTime|NotifyFilters.Size,InternalBufferSize=32768};
    _watcher.Created+=(_,e)=>{TraceFile("created",e.FullPath,root);Observe(e.FullPath,generation);};_watcher.Changed+=(_,e)=>{TraceFile("changed",e.FullPath,root);Observe(e.FullPath,generation);};_watcher.Renamed+=(_,e)=>{TraceFile("renamed",e.FullPath,root,e.OldFullPath);Remove(e.OldFullPath,generation);Observe(e.FullPath,generation);};_watcher.Deleted+=(_,e)=>{TraceFile("deleted",e.FullPath,root);Remove(e.FullPath,generation);};
    _watcher.Error+=(_,_)=>{lock(_gate){if(generation==_generation&&_scan.IsCompleted){Status="监听溢出，重建索引";_scan=Task.Run(()=>Scan(root,generation));}}};
    _watcher.EnableRaisingEvents=true;Status="目录监听中";_scan=Task.Run(()=>Scan(root,generation));
   }catch(Exception ex){Status="目录监听不可用";ServiceDiagnostics.Error("图片目录监听",ex);}
  }
 }
 private static void TraceFile(string kind,string path,string root,string? oldPath=null){if(!DiagnosticCapture.Active)return;DiagnosticCapture.Write("file.event",new{kind,observedAt=DateTimeOffset.UtcNow,relativePath=Path.GetRelativePath(root,path),oldRelativePath=oldPath is null?null:Path.GetRelativePath(root,oldPath)});}
 private void Remove(string path,int generation){lock(_gate){if(generation==_generation)_files.Remove(path);}}
 private void Observe(string path,int generation){Read(path,generation,DateTimeOffset.UtcNow);}
 private void Read(string path,int generation,DateTimeOffset? observed)
 {
  try{
   bool fromWatcher=observed is not null;
   string root;lock(_gate){if(_disposed||generation!=_generation)return;root=_root;}
   if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return;
   for(string? node=Path.GetDirectoryName(path);node is not null;node=Path.GetDirectoryName(node)){
    if((File.GetAttributes(node)&FileAttributes.ReparsePoint)!=0)return;if(node.Equals(root,StringComparison.OrdinalIgnoreCase))break;
   }
   if(!File.Exists(path))return;var info=new FileInfo(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0)return;
   lock(_gate){if(_disposed||generation!=_generation)return;
    if(_files.TryGetValue(path,out var old)){
     if(observed is null&&old.ObservedAt is not null)return;
     if(old.Length==info.Length&&old.WrittenUtc==info.LastWriteTimeUtc&&old.CreatedUtc==info.CreationTimeUtc)observed=old.ObservedAt??observed;
    }
    _files[path]=new(path,info.Length,info.LastWriteTimeUtc,info.CreationTimeUtc,observed);if(observed is not null)EventCount++;
    if(DiagnosticCapture.Active)DiagnosticCapture.Write("file.index",new{relativePath=Path.GetRelativePath(root,path),origin=fromWatcher?"watcher":"startup-scan",info.Length,writtenUtc=info.LastWriteTimeUtc,createdUtc=info.CreationTimeUtc,storedObservedAt=observed});
    if(_files.Count>12000){var oldest=_files.Values.OrderBy(e=>e.ObservedAt?.UtcDateTime??e.WrittenUtc).First();_files.Remove(oldest.Path);}
   }
  }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){ServiceDiagnostics.Set("图片目录监听","文件暂不可读，等待写入完成");}
 }
 private void Scan(string root,int generation)
 {
  try{foreach(string file in Enumerate(root)){lock(_gate){if(_disposed||generation!=_generation)return;}Read(file,generation,null);}}
  catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){ServiceDiagnostics.Error("图片目录扫描",ex);}
 }
 public static IEnumerable<string> Enumerate(string root)
 {
  var queue=new Queue<(string Path,int Depth)>();queue.Enqueue((root,0));int dirs=0,files=0;
  while(queue.TryDequeue(out var item)&&dirs++<256){
   if((File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)continue;
   foreach(string file in Directory.EnumerateFiles(item.Path)){if(++files>12000)yield break;yield return file;}
   if(item.Depth>=5)continue;
   foreach(string child in Directory.EnumerateDirectories(item.Path).OrderByDescending(p=>p,StringComparer.OrdinalIgnoreCase).Take(48))queue.Enqueue((child,item.Depth+1));
  }
 }
 public async Task<ImageFileEntry[]> SnapshotAsync(ReceptionOptions options,CancellationToken token)
 {
  Configure(options);Task scan;lock(_gate)scan=_scan;await scan.WaitAsync(token);lock(_gate)return _files.Values.ToArray();
 }
 public async Task ReconcileAsync(ReceptionOptions options,CancellationToken token){
  Configure(options);Task scan;
  lock(_gate){if(_disposed||_root.Length==0)return;if(_scan.IsCompleted){string root=_root;int generation=_generation;_scan=Task.Run(()=>Scan(root,generation),token);}scan=_scan;}
  await scan.WaitAsync(token);
 }
 public string Describe(){lock(_gate){var newest=_files.Values.OrderByDescending(f=>f.ObservedAt?.UtcDateTime??f.WrittenUtc).FirstOrDefault();return $"{Status}；索引 {_files.Count} 个文件；变更事件 {EventCount}；最近文件距今 {(newest is null?"未知":Math.Max(0,(DateTime.UtcNow-(newest.ObservedAt?.UtcDateTime??(newest.CreatedUtc>newest.WrittenUtc?newest.CreatedUtc:newest.WrittenUtc))).TotalSeconds).ToString("F0")+" 秒")}";}}
 public void Dispose(){lock(_gate){_disposed=true;++_generation;_watcher?.Dispose();_watcher=null;_files.Clear();Status="监听已停止";}}
}
