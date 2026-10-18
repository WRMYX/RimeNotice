using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed record NoticeAttachment(string Path,bool IsImage);
public static class NotificationAttachmentResolver {
 private static readonly string[] Images=[".png",".jpg",".jpeg",".webp",".bmp"];
 private static readonly string[] Files=[".pdf",".docx",".doc",".xlsx",".xls",".pptx",".ppt",".txt"];
 public static bool Allowed(string path)=>Images.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()) || Files.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());
 public static NoticeAttachment? Exact(string message,ReceptionOptions options) {
  if(!options.PreviewImages || string.IsNullOrWhiteSpace(options.CacheDirectory))return null;
  string directory=DirectoryFor(options);
  if(!Directory.Exists(directory))return null;
  foreach(Match match in Regex.Matches(message,@"file:///[^\r\n<>]+")) {
   if(Uri.TryCreate(match.Value.Trim(),UriKind.Absolute,out var uri) && uri.IsFile) {
    string path=System.IO.Path.GetFullPath(uri.LocalPath);
    if(Inside(path,directory)&&Valid(path))return new(path,Images.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()));
   }
  }
  // A file name must be explicitly present in the notification. Never choose a
  // merely recent QQ thumbnail, which may belong to a different conversation.
  var candidates=Directory.EnumerateFiles(directory).Take(1024).Where(p=>message.Contains(System.IO.Path.GetFileName(p),StringComparison.OrdinalIgnoreCase)&&Valid(p)).Take(2).ToArray();
  return candidates.Length==1?new(candidates[0],Images.Contains(System.IO.Path.GetExtension(candidates[0]).ToLowerInvariant())):null;
 }
 private static string DirectoryFor(ReceptionOptions options){
  string directory=Path.GetFullPath(options.CacheDirectory);
  if(!string.IsNullOrEmpty(options.QQAccount)&&options.QQAccount.All(char.IsDigit)){
   string monthly=System.IO.Path.Combine(directory,options.QQAccount,"nt_qq","nt_data","Pic",DateTime.Now.ToString("yyyy-MM"),"Thumb");
   if(Directory.Exists(monthly))directory=monthly;
  }
  return directory;
 }
 private static bool Valid(string path)=>Allowed(path)&&File.Exists(path)&&new FileInfo(path).Length<=100*1024*1024&&
  (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0;
 private static bool Inside(string path,string directory)=>path.StartsWith(directory.TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 public static async Task<NoticeAttachment?> ResolveAsync(string message,ReceptionOptions options,CancellationToken token) {
  if(!options.PreviewImages)return null;
  try {
   var found=await Task.Run(()=>Exact(message,options),token);if(found is not null)return found;
   if(!message.Contains(".",StringComparison.Ordinal)||!Directory.Exists(options.CacheDirectory)||options.AttachmentWaitSeconds==0)return null;
   using var signal=new SemaphoreSlim(0,1);using var watcher=new FileSystemWatcher(DirectoryFor(options)){IncludeSubdirectories=false,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite};
   void Wake(){try{signal.Release();}catch(SemaphoreFullException){}catch(ObjectDisposedException){}}
   watcher.Created+=(_,_)=>Wake();watcher.Changed+=(_,_)=>Wake();watcher.Renamed+=(_,_)=>Wake();watcher.EnableRaisingEvents=true;
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(options.AttachmentWaitSeconds));
   do{found=await Task.Run(()=>Exact(message,options),timeout.Token);if(found is not null)return found;await signal.WaitAsync(timeout.Token);}while(!timeout.IsCancellationRequested);
   ServiceDiagnostics.Set("附件预览","未取得可验证的文件路径");return null;
  }catch(OperationCanceledException){return null;}catch(Exception ex){ServiceDiagnostics.Error("附件预览",ex);return null;}
 }
}
