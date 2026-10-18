using System;
using System.Collections.Generic;
namespace RimeNotice.Services;
public static class ServiceDiagnostics {
 private static readonly object Gate=new();
 private static readonly Dictionary<string,string> Entries=new();
 public static void Set(string service,string status) { lock(Gate) Entries[service]=status; }
 public static void Error(string service,Exception error) => Set(service,$"{error.GetType().Name} / 0x{error.HResult:X8}");
 public static string Snapshot() { lock(Gate) return string.Join("\n",System.Linq.Enumerable.Select(Entries,p=>$"{p.Key}：{p.Value}")); }
}
