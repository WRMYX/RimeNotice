using System;
using System.IO;
namespace RimeNotice.Services;
/// <summary>Appends unhandled errors to %LocalAppData%\RimeNotice\crash.log (no message content).</summary>
public static class CrashLog {
 private static readonly object Gate=new();
 private static readonly string FilePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RimeNotice","crash.log");
 public static void Write(string origin,Exception? error) {
  try {
   lock(Gate) {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
    if(File.Exists(FilePath)&&new FileInfo(FilePath).Length>512*1024)File.Delete(FilePath);
    File.AppendAllText(FilePath,$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {origin}\n{error}\n\n");
   }
   if(error is not null)ServiceDiagnostics.Error("未处理异常（"+origin+"）",error);
  } catch(Exception) { }
 }
}
