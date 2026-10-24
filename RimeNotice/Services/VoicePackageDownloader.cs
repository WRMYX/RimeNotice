using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
namespace RimeNotice.Services;

public sealed record ChineseVoicePackage(string Name,string DisplayName,string PackageName,string Version,string Url);
public static class VoicePackageDownloader {
 private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromMinutes(15)};
 private static readonly SemaphoreSlim Gate=new(1,1);
 public static ChineseVoicePackage[] Catalog()=>JsonSerializer.Deserialize<ChineseVoicePackage[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","Speech","ChineseVoiceCatalog.json")))??[];
 public static string DownloadDirectory(){var id=new Guid("374DE290-123F-4565-9164-39C4925E467B");IntPtr value=IntPtr.Zero;try{Marshal.ThrowExceptionForHR(SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out value));string path=Marshal.PtrToStringUni(value)??throw new IOException("Windows 未返回下载目录。");if(!Path.IsPathFullyQualified(path))throw new IOException("Windows 下载目录无效。");return path;}finally{if(value!=IntPtr.Zero)Marshal.FreeCoTaskMem(value);}}
 public static Task<string> DownloadAsync(ChineseVoicePackage selected,IProgress<string> progress,CancellationToken token)=>DownloadToDirectoryAsync(selected,DownloadDirectory(),Client,progress,token);
 internal static async Task<string> DownloadToDirectoryAsync(ChineseVoicePackage selected,string directory,HttpClient client,IProgress<string> progress,CancellationToken token){
  if(!Catalog().Contains(selected))throw new InvalidOperationException("声音不在内置中文目录中。");
  var uri=new Uri(selected.Url);if(uri.Scheme!="https"||uri.Host!="dl.nvdacn.com")throw new InvalidOperationException("语音下载来源无效。");
  string stem=selected.PackageName+"_"+selected.Version+"_x64";if(stem.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("语音包文件名无效。");
  await Gate.WaitAsync(token);string? temporary=null;
  using var limit=CancellationTokenSource.CreateLinkedTokenSource(token);limit.CancelAfter(TimeSpan.FromMinutes(15));var work=limit.Token;
  try{
   directory=Path.GetFullPath(directory);Directory.CreateDirectory(directory);temporary=Path.Combine(directory,".RN-voice-"+Guid.NewGuid().ToString("N")+".partial.msix");
   using(var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,work)){
    response.EnsureSuccessStatusCode();if(response.RequestMessage?.RequestUri is not {Scheme:"https"})throw new InvalidOperationException("下载被重定向到不安全的连接。");
    long? total=response.Content.Headers.ContentLength;long received=0;int last=-1;const long maximum=2L*1024*1024*1024;
    if(total>maximum)throw new InvalidDataException("语音包超过允许大小。");
    await using var input=await response.Content.ReadAsStreamAsync(work);await using var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);var buffer=new byte[81920];int read;
    while((read=await ReadAsync(input,buffer,work))>0){received+=read;if(received>maximum)throw new InvalidDataException("语音包超过允许大小。");await output.WriteAsync(buffer.AsMemory(0,read),work);int step=(int)(received/1048576);if(step!=last){last=step;progress.Report(total>0?$"下载中：{received*100/total}%（{step} MiB）":$"下载中：{step} MiB");}}
    if(received==0||total.HasValue&&received!=total.Value)throw new IOException("语音包未下载完整，请重试。");
   }
   work.ThrowIfCancellationRequested();progress.Report("下载完成，正在验证语音包身份和签名…");await Task.Run(()=>Validate(temporary,selected),work);work.ThrowIfCancellationRequested();
   string completed=temporary??throw new IOException("临时下载文件缺失。");
   for(int i=0;i<10000;i++){
    string destination=Path.Combine(directory,stem+(i==0?"":$" ({i})")+".msix");
    try{File.Move(completed,destination);temporary=null;return destination;}
    catch(IOException)when(File.Exists(destination)){}
   }
   throw new IOException("同名语音包过多，请整理下载目录后重试。");
  }catch(OperationCanceledException)when(!token.IsCancellationRequested){throw new TimeoutException("语音包下载超时，请检查网络后重试。");}
  finally{try{if(temporary is not null)File.Delete(temporary);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){ServiceDiagnostics.Error("语音下载临时文件清理",ex);}finally{Gate.Release();}}
 }
 private static async Task<int> ReadAsync(Stream stream,byte[] buffer,CancellationToken token){using var idle=CancellationTokenSource.CreateLinkedTokenSource(token);idle.CancelAfter(TimeSpan.FromSeconds(45));try{return await stream.ReadAsync(buffer,idle.Token);}catch(OperationCanceledException)when(!token.IsCancellationRequested){throw new TimeoutException("下载 45 秒未收到数据，请检查网络后重试。");}}
 private static string HistoryPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RimeNotice","VoiceDownloads.json");
 private static Dictionary<string,string> ReadHistory(){try{return File.Exists(HistoryPath)?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(HistoryPath))??new():new();}catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException){ServiceDiagnostics.Error("语音下载记录",ex);return new();}}
 public static string? LastDownload(ChineseVoicePackage selected){var history=ReadHistory();return history.TryGetValue(selected.PackageName+"_"+selected.Version,out var path)&&Path.IsPathFullyQualified(path)?path:null;}
 public static void Remember(ChineseVoicePackage selected,string file){var history=ReadHistory();history[selected.PackageName+"_"+selected.Version]=Path.GetFullPath(file);Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);string temporary=HistoryPath+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,JsonSerializer.Serialize(history));File.Move(temporary,HistoryPath,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
 public static void Reveal(string path){if(!File.Exists(path))throw new FileNotFoundException("文件已移动或删除，请重新下载或手动查找。",path);IntPtr item=ILCreateFromPathW(Path.GetFullPath(path));if(item==IntPtr.Zero)throw new IOException("无法定位语音文件。");try{Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item,0,IntPtr.Zero,0));}finally{ILFree(item);}}
 [DllImport("shell32.dll",ExactSpelling=true)]private static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]private static extern IntPtr ILCreateFromPathW(string path);
 [DllImport("shell32.dll",ExactSpelling=true)]private static extern void ILFree(IntPtr item);
 [DllImport("shell32.dll",ExactSpelling=true)]private static extern int SHOpenFolderAndSelectItems(IntPtr item,uint count,IntPtr children,uint flags);
 internal static void Validate(string file,ChineseVoicePackage selected){
  using(var zip=ZipFile.OpenRead(file)){
   var manifest=zip.GetEntry("AppxManifest.xml")??throw new InvalidDataException("不是有效的 MSIX 语音包。");if(manifest.Length>1024*1024)throw new InvalidDataException("语音包清单异常。");using var input=manifest.Open();var xml=XDocument.Load(input);var identity=xml.Root?.Elements().FirstOrDefault(e=>e.Name.LocalName=="Identity");
   string? publisher=(string?)identity?.Attribute("Publisher");
   if((string?)identity?.Attribute("Name")!=selected.PackageName||(string?)identity?.Attribute("Version")!=selected.Version||(string?)identity?.Attribute("ProcessorArchitecture")!="x64"||publisher is null||!publisher.Contains("O=Microsoft Corporation",StringComparison.Ordinal)||!(publisher.StartsWith("CN=Microsoft Windows,",StringComparison.Ordinal)||publisher.StartsWith("CN=Microsoft Corporation,",StringComparison.Ordinal)))throw new InvalidDataException("语音包身份、版本、架构或发布者与目录不一致。");
  }
  VerifyWindowsSignature(file);
 }
 // Windows SIP verifies the MSIX signature and trust chain; never install a certificate.
 private static void VerifyWindowsSignature(string path){
  var info=new FileInfoNative{Size=(uint)Marshal.SizeOf<FileInfoNative>(),Path=path};var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoNative>());
  try{Marshal.StructureToPtr(info,pointer,false);var trust=new TrustData{Size=(uint)Marshal.SizeOf<TrustData>(),UiChoice=2,UnionChoice=1,File=pointer,StateAction=1};var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");int result;
   try{result=WinVerifyTrust(new IntPtr(-1),ref action,ref trust);}finally{trust.StateAction=2;WinVerifyTrust(new IntPtr(-1),ref action,ref trust);}
   if(result!=0)throw new InvalidOperationException($"语音包 Windows 签名验证未通过（0x{result:X8}），未保存为 MSIX。");
  }finally{Marshal.DestroyStructure<FileInfoNative>(pointer);Marshal.FreeHGlobal(pointer);}
 }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct FileInfoNative{public uint Size;[MarshalAs(UnmanagedType.LPWStr)]public string Path;public IntPtr File,KnownSubject;}
 [StructLayout(LayoutKind.Sequential)]private struct TrustData{public uint Size;public IntPtr Policy,Sip;public uint UiChoice,Revocation,UnionChoice;public IntPtr File;public uint StateAction;public IntPtr State,Url;public uint Flags,Context;public IntPtr SignatureSettings;}
 [DllImport("wintrust.dll",ExactSpelling=true)]private static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
}
