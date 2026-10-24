using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed record NoticeAttachment(string Path,bool IsImage,bool Inferred=false);
public static class NotificationAttachmentResolver
{
 private static readonly string[] Images=[".png",".jpg",".jpeg",".webp",".bmp",".gif"];
 private static readonly string[] Files=[".pdf",".docx",".doc",".xlsx",".xls",".pptx",".ppt",".txt"];
 public static bool Allowed(string path)=>Images.Contains(Path.GetExtension(path).ToLowerInvariant())||Files.Contains(Path.GetExtension(path).ToLowerInvariant());
 public static string DirectoryFor(ReceptionOptions options)
 {
  string directory=Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.CacheDirectory));
  if(options.QQAccount.Length>0&&options.QQAccount.All(char.IsAsciiDigit)){
   string pic=Path.Combine(directory,options.QQAccount,"nt_qq","nt_data","Pic");if(Directory.Exists(pic))return pic;
  }
  // Accept old settings pointing at Pic/yyyy-MM/Thumb while covering Ori as well.
  var parent=new DirectoryInfo(directory);
  for(int level=0;parent is not null&&level<=3;level++,parent=parent.Parent)if(parent.Name.Equals("Pic",StringComparison.OrdinalIgnoreCase))return Path.TrimEndingDirectorySeparator(parent.FullName);
  return directory;
 }
 private static bool Inside(string path,string root)=>path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 private static bool Valid(string path,string root)
 {
  if(!Inside(path,root)||!Allowed(path)||!File.Exists(path)||new FileInfo(path).Length>100*1024*1024)return false;
  for(string? node=path;node is not null;node=Path.GetDirectoryName(node)){
   if((File.GetAttributes(node)&FileAttributes.ReparsePoint)!=0)return false;
   if(node.Equals(root,StringComparison.OrdinalIgnoreCase))return true;
  }
  return false;
 }
 private static IEnumerable<string> Candidates(string root)=>QQImageIndex.Enumerate(root);
 public static NoticeAttachment? Exact(string message,ReceptionOptions options)
 {
  if(!options.PreviewImages||string.IsNullOrWhiteSpace(options.CacheDirectory))return null;
  string root=DirectoryFor(options);if(!Directory.Exists(root))return null;
  foreach(Match match in Regex.Matches(message,@"file:///[^\r\n<>]+")){
   if(Uri.TryCreate(match.Value.Trim(),UriKind.Absolute,out var uri)&&uri.IsFile){string path=Path.GetFullPath(uri.LocalPath);if(Valid(path,root))return new(path,Images.Contains(Path.GetExtension(path).ToLowerInvariant()));}
  }
  if(!message.Contains('.',StringComparison.Ordinal))return null;
  var matches=Candidates(root).Where(p=>message.Contains(Path.GetFileName(p),StringComparison.OrdinalIgnoreCase)&&Valid(p,root)).Take(2).ToArray();
  // A name shared by Ori and Thumb is still ambiguous. Never infer a chat association from recency.
  return matches.Length==1?new(matches[0],Images.Contains(Path.GetExtension(matches[0]).ToLowerInvariant())):null;
 }
 private static readonly AsyncLocal<string?> TraceKey=new();
 private static void Trace(string stage,object data){if(DiagnosticCapture.Active)DiagnosticCapture.Write("attachment."+stage,new{key=TraceKey.Value,data});}
 private static readonly object RecentGate=new();
 private static readonly Dictionary<string,DateTime> Used=new(StringComparer.OrdinalIgnoreCase);
 public static bool IsImageMessage(string text)=>Regex.IsMatch(text,@"\[(图片|图像|image|photo)\]|发送了?.{0,4}(图片|照片)|^\s*(图片|照片|image|photo)\s*$|🖼",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
 private static DateTime Time(ImageFileEntry file)=>file.ObservedAt?.UtcDateTime??(file.CreatedUtc>file.WrittenUtc?file.CreatedUtc:file.WrittenUtc);
 private static string Identity(string path){
  var hash=Regex.Match(Path.GetFileNameWithoutExtension(path),@"^[a-fA-F0-9]{32}(?=$|[_-])");
  if(!hash.Success)return path;
  var parent=new DirectoryInfo(Path.GetDirectoryName(path)!);while(parent.Parent is not null&&!parent.Name.Equals("Pic",StringComparison.OrdinalIgnoreCase))parent=parent.Parent;
  return Path.Combine(parent.FullName,hash.Value.ToLowerInvariant());
 }
 public static NoticeAttachment? SelectRecent(IEnumerable<ImageFileEntry> entries,ReceptionOptions options,DateTimeOffset receivedAt)
 {
  if(!options.PreviewImages||!options.InferRecentImages)return null;
  var root=DirectoryFor(options);var start=receivedAt.UtcDateTime.AddSeconds(-15);var end=receivedAt.UtcDateTime.AddSeconds(Math.Max(15,options.AttachmentWaitSeconds));
  lock(RecentGate){
   var groups=entries.Where(f=>f.Length>0&&Allowed(f.Path)&&Images.Contains(Path.GetExtension(f.Path).ToLowerInvariant())&&Time(f)>=start&&Time(f)<=end&&Valid(f.Path,root))
    .GroupBy(f=>Identity(f.Path),StringComparer.OrdinalIgnoreCase)
    .Select(g=>new {Key=g.Key,Entries=g.ToArray(),Time=g.Max(Time)})
    .Where(g=>!Used.TryGetValue(g.Key,out var used)||used<g.Time)
    .OrderBy(g=>Math.Abs((g.Time-receivedAt.UtcDateTime).TotalMilliseconds)).Take(2).ToArray();
   if(DiagnosticCapture.Active)Trace("candidates",new{receivedAt,groups=groups.Select(g=>new{identity=Path.GetFileName(g.Key),g.Time,distanceMs=Math.Abs((g.Time-receivedAt.UtcDateTime).TotalMilliseconds),files=g.Entries.Select(f=>new{relativePath=Path.GetRelativePath(root,f.Path),f.Length,f.ObservedAt,f.CreatedUtc,f.WrittenUtc}).ToArray()}).ToArray()});
   if(groups.Length==0){ServiceDiagnostics.Set("图片匹配","时间窗口内没有新的可支持图片；检查 QQ 是否向所选目录保存了文件");return null;}
   if(groups.Length>1&&Math.Abs((groups[0].Time-receivedAt.UtcDateTime).TotalMilliseconds)+500>=Math.Abs((groups[1].Time-receivedAt.UtcDateTime).TotalMilliseconds)){Trace("ambiguous",new{reason="nearest-two-within-500ms"});ServiceDiagnostics.Set("图片匹配","存在多个不同图片候选，无法区分");return null;}
   var chosen=groups[0].Entries.OrderByDescending(f=>f.Path.Contains(Path.DirectorySeparatorChar+"Ori"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).ThenByDescending(f=>f.Length).First();
   return new(chosen.Path,true,true);
  }
 }
 public static NoticeAttachment? Recent(ReceptionOptions options,DateTimeOffset receivedAt){
  if(!options.PreviewImages||!options.InferRecentImages||string.IsNullOrWhiteSpace(options.CacheDirectory))return null;
  string root=DirectoryFor(options);if(!Directory.Exists(root))return null;
  return SelectRecent(Candidates(root).Select(p=>new FileInfo(p)).Select(f=>new ImageFileEntry(f.FullName,f.Length,f.LastWriteTimeUtc,f.CreationTimeUtc,null)),options,receivedAt);
 }
 public static bool ImageHeaderValid(string path){
  using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);Span<byte> header=stackalloc byte[12];int n=stream.Read(header);
  return n>=8&&header[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||n>=3&&header[0]==255&&header[1]==216&&header[2]==255||n>=6&&(header[..6].SequenceEqual("GIF87a"u8)||header[..6].SequenceEqual("GIF89a"u8))||n>=2&&header[0]==66&&header[1]==77||n>=12&&header[..4].SequenceEqual("RIFF"u8)&&header.Slice(8,4).SequenceEqual("WEBP"u8);
 }
 public static async Task<NoticeAttachment?> ResolveAsync(string message,ReceptionOptions options,CancellationToken token,DateTimeOffset? receivedAt=null,Func<string,CancellationToken,Task<bool>>? validate=null,string? diagnosticKey=null)
 {
  TraceKey.Value=diagnosticKey;
  Trace("start",new{receivedAt,options.PreviewImages,options.InferRecentImages,options.AttachmentWaitSeconds});
  if(!options.PreviewImages||string.IsNullOrWhiteSpace(options.CacheDirectory)){Trace("disabled",new{});ServiceDiagnostics.Set("图片匹配","图片预览关闭或未选择目录");return null;}
  try{
   var timestamp=receivedAt??DateTimeOffset.UtcNow;int wait=Math.Max(options.InferRecentImages?15:0,options.AttachmentWaitSeconds);var deadline=DateTimeOffset.UtcNow.AddSeconds(wait);
   using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(TimeSpan.FromSeconds(wait+2));token=bounded.Token;
   if(!Directory.Exists(DirectoryFor(options))){ServiceDiagnostics.Set("图片匹配","所选目录不存在");return null;}
   bool imageMessage=IsImageMessage(message);
   do{
    token.ThrowIfCancellationRequested();
    var found=await Task.Run(()=>Exact(message,options),token);
    ImageFileEntry[] entries=await QQImageIndex.Shared.SnapshotAsync(options,token);
    found??=imageMessage?SelectRecent(entries,options,timestamp):null;
    if(found is not null){
     var before=new FileInfo(found.Path);long size=before.Length;DateTime write=before.LastWriteTimeUtc;
     await Task.Delay(200,token);var after=new FileInfo(found.Path);
     if(!after.Exists||size==0||size!=after.Length||write!=after.LastWriteTimeUtc){Trace("unstable",new{file=Path.GetFileName(found.Path),size,write});ServiceDiagnostics.Set("图片匹配","候选仍在写入，等待稳定");}
     else if(found.IsImage&&!(validate is null?await Task.Run(()=>ImageHeaderValid(found.Path),token):await validate(found.Path,token))){Trace("decode-failed",new{file=Path.GetFileName(found.Path)});ServiceDiagnostics.Set("图片匹配","候选无法解码或不是受支持的图片格式");}
     else{
      if(found.Inferred){lock(RecentGate){var key=Identity(found.Path);DateTime stamp=entries.Where(e=>Identity(e.Path).Equals(key,StringComparison.OrdinalIgnoreCase)).Select(Time).DefaultIfEmpty(write).Max();
       if(Used.TryGetValue(key,out var used)&&used>=stamp){Trace("claim-conflict",new{identity=Path.GetFileName(key),used,stamp});found=null;}
       else{if(Used.Count>=1024)Used.Remove(Used.OrderBy(e=>e.Value).First().Key);Used[key]=stamp;Trace("claimed",new{identity=Path.GetFileName(key),stamp});}
      }}
      if(found is not null){Trace("resolved",new{relativePath=Path.GetRelativePath(DirectoryFor(options),found.Path),found.IsImage,found.Inferred});ServiceDiagnostics.Set("图片匹配",found.Inferred?"已关联可解码的推测图片":"已匹配明确附件");return found;}
     }
    }
    if(!imageMessage&&!message.Contains('.',StringComparison.Ordinal))break;
    if(imageMessage&&!options.InferRecentImages&&!message.Contains('.',StringComparison.Ordinal)){ServiceDiagnostics.Set("图片匹配","消息无明确文件名，且时间推测关闭");break;}
    if(DateTimeOffset.UtcNow>=deadline)break;
    await Task.Delay(250,token);
   }while(true);
   Trace("unresolved",new{reason="deadline-or-no-explicit-reference"});ServiceDiagnostics.Set("图片目录",QQImageIndex.Shared.Describe());return null;
  }catch(OperationCanceledException){Trace("cancelled",new{});return null;}catch(Exception ex){Trace("error",new{error=ex.GetType().Name,ex.HResult});ServiceDiagnostics.Error("图片匹配",ex);return null;}
 }
 internal static bool IsSupportedImage(string path)=>Images.Contains(Path.GetExtension(path).ToLowerInvariant());
 private static readonly Dictionary<string,string> ClaimOwners=new(StringComparer.OrdinalIgnoreCase);
 internal static bool AvailableForSession(string identity,string owner){lock(RecentGate)return !Used.ContainsKey(identity)||ClaimOwners.TryGetValue(identity,out var actual)&&actual==owner;}
 internal static void ReleaseUnpublished(string identity,string owner){lock(RecentGate){
  if(ClaimOwners.TryGetValue(identity,out var actual)&&actual==owner){ClaimOwners.Remove(identity);Used.Remove(identity);}
 }}
 internal static bool ClaimForSession(string identity,string owner,bool inferred){lock(RecentGate){
  if(inferred&&Used.ContainsKey(identity)&&(!ClaimOwners.TryGetValue(identity,out var actual)||actual!=owner))return false;
  Used[identity]=DateTime.UtcNow;if(!ClaimOwners.ContainsKey(identity))ClaimOwners[identity]=owner;
  while(Used.Count>1024){var oldest=Used.OrderBy(e=>e.Value).First().Key;Used.Remove(oldest);ClaimOwners.Remove(oldest);}
  return true;
 }}
 internal static SessionImageCandidate[] SessionCandidates(ImageFileEntry[] entries,ReceptionOptions options,string message,DateTimeOffset timestamp,DateTimeOffset end,string owner){
  string root=DirectoryFor(options);
  var explicitItems=BatchCandidates(entries,options,timestamp,true,message).ToList();var exact=Exact(message,options);
  if(exact is not null&&!explicitItems.Any(a=>a.Path.Equals(exact.Path,StringComparison.OrdinalIgnoreCase)))explicitItems.Add(exact);
  if(explicitItems.Count>0)return explicitItems.Select(a=>{
   var file=entries.FirstOrDefault(e=>e.Path.Equals(a.Path,StringComparison.OrdinalIgnoreCase));
   var info=new FileInfo(a.Path);return new SessionImageCandidate(Identity(a.Path),a,file is null?new DateTimeOffset(info.LastWriteTimeUtc):new DateTimeOffset(Time(file)),info.Length,info.LastWriteTimeUtc);
  }).ToArray();
  if(!options.InferRecentImages||!IsImageMessage(message))return [];
  return entries.Where(e=>e.Length>0&&IsSupportedImage(e.Path)&&Time(e)>=timestamp.UtcDateTime.AddSeconds(-15)&&Time(e)<=end.UtcDateTime)
   .Where(e=>AvailableForSession(Identity(e.Path),owner)&&Valid(e.Path,root))
   .GroupBy(e=>Identity(e.Path),StringComparer.OrdinalIgnoreCase).Select(g=>{
    var chosen=g.OrderByDescending(e=>e.Path.Contains(Path.DirectorySeparatorChar+"Ori"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).ThenByDescending(e=>e.Length).First();
    // Prefer the earliest observation of this identity; an Ori arriving later is not a new picture.
    return new SessionImageCandidate(g.Key,new(chosen.Path,true,true),new DateTimeOffset(g.Min(Time)),chosen.Length,chosen.WrittenUtc);
   }).OrderBy(c=>c.Time).ToArray();
 }
 internal static string FileIdentity(string path)=>Identity(path);
 internal static bool ValidImage(string path,string root)=>Valid(path,root)&&Images.Contains(Path.GetExtension(path).ToLowerInvariant());
 internal static NoticeAttachment[] BatchCandidates(IEnumerable<ImageFileEntry> entries,ReceptionOptions options,DateTimeOffset timestamp,bool exactOnly,string message){
  string root=DirectoryFor(options);var start=timestamp.UtcDateTime.AddSeconds(-15);var end=timestamp.UtcDateTime.AddSeconds(Math.Max(15,options.AttachmentWaitSeconds));
  var groups=entries.Where(f=>f.Length>0)
   .Where(f=>exactOnly?message.Contains(Path.GetFileName(f.Path),StringComparison.OrdinalIgnoreCase):Images.Contains(Path.GetExtension(f.Path).ToLowerInvariant())&&Time(f)>=start&&Time(f)<=end)
   .Where(f=>Valid(f.Path,root))
   .GroupBy(f=>Identity(f.Path),StringComparer.OrdinalIgnoreCase)
   .Select(g=>new{Key=g.Key,Stamp=g.Max(Time),Files=g.OrderByDescending(f=>f.Path.Contains(Path.DirectorySeparatorChar+"Ori"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).ThenByDescending(f=>f.Length).ToArray()});
  lock(RecentGate){
   var all=groups.ToArray();
   // A basename repeated under unrelated identities is not an explicit association.
   var eligible=all.Where(g=>exactOnly?g.Files.Any(f=>all.Count(other=>other.Files.Any(candidate=>Path.GetFileName(candidate.Path).Equals(Path.GetFileName(f.Path),StringComparison.OrdinalIgnoreCase)))==1):!Used.ContainsKey(g.Key)).OrderBy(g=>g.Stamp).ToArray();
   if(!exactOnly&&eligible.Length>0){
    var nearest=eligible.OrderBy(g=>Math.Abs((g.Stamp-timestamp.UtcDateTime).TotalMilliseconds)).First();
    // Same short disk burst is an explicitly labelled inferred album, never an exact chat identity.
    eligible=eligible.Where(g=>Math.Abs((g.Stamp-nearest.Stamp).TotalSeconds)<=2).ToArray();
   }
   return eligible.Select(g=>new NoticeAttachment(g.Files[0].Path,Images.Contains(Path.GetExtension(g.Files[0].Path).ToLowerInvariant()),!exactOnly)).ToArray();
  }
 }
 internal static bool ClaimBatch(NoticeAttachment[] items,IEnumerable<ImageFileEntry> entries){
  lock(RecentGate){
   var stamps=items.Select(a=>new{Key=Identity(a.Path),Stamp=entries.Where(e=>Identity(e.Path).Equals(Identity(a.Path),StringComparison.OrdinalIgnoreCase)).Select(Time).DefaultIfEmpty(File.GetLastWriteTimeUtc(a.Path)).Max()}).ToArray();
   if(stamps.Any(s=>Used.ContainsKey(s.Key)))return false;
   foreach(var stamp in stamps)Used[stamp.Key]=stamp.Stamp;
   while(Used.Count>1024)Used.Remove(Used.OrderBy(e=>e.Value).First().Key);
   return true;
  }
 }
 public static async Task<NoticeAttachmentBatch> ResolveBatchAsync(string message,ReceptionOptions options,CancellationToken token,DateTimeOffset timestamp,string[] keys,Func<string,CancellationToken,Task<bool>>? validate=null){
  using var session=new NoticeImageSession(message,options,timestamp,keys,token,validate);
  await session.Completion;return session.Snapshot;
 }
}
