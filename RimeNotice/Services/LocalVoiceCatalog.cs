using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
namespace RimeNotice.Services;

public enum LocalVoiceSource { WinRT, Sapi }
public sealed record LocalVoice(string Id,string Name,string Language,LocalVoiceSource Source=LocalVoiceSource.WinRT) {
 public string SourceName=>Source==LocalVoiceSource.Sapi?"SAPI":"Windows";
}
public sealed record LocalVoiceScan(LocalVoice[] Voices,string[] Warnings);
public static class LocalVoiceCatalog {
 public const string SapiPrefix="sapi:";
 public static bool IsSapi(string id)=>id.StartsWith(SapiPrefix,StringComparison.OrdinalIgnoreCase);
 public static LocalVoice[] Scan()=>ScanAsync(CancellationToken.None).GetAwaiter().GetResult().Voices;
 public static async Task<LocalVoiceScan> ScanAsync(CancellationToken token){
  var voices=new List<LocalVoice>();var warnings=new List<string>();
  try{voices.AddRange(await Task.Run(()=>SpeechSynthesizer.AllVoices.Select(v=>new LocalVoice(v.Id,v.DisplayName,v.Language)).ToArray(),token));}
  catch(OperationCanceledException){throw;}catch(Exception ex){warnings.Add("Windows 声音读取失败："+ex.Message);ServiceDiagnostics.Error("WinRT 声音枚举",ex);}
  try{voices.AddRange(await SapiSpeechBackend.ScanAsync(token));}
  catch(OperationCanceledException){throw;}catch(Exception ex){warnings.Add("SAPI 声音读取失败："+ex.Message);ServiceDiagnostics.Error("SAPI 声音枚举",ex);}
  token.ThrowIfCancellationRequested();return new(voices.DistinctBy(v=>v.Id).OrderByDescending(v=>v.Language.StartsWith("zh",StringComparison.OrdinalIgnoreCase)).ThenBy(v=>v.Name).ToArray(),warnings.ToArray());
 }
 public static bool ContainsVoice(string name)=>Scan().Any(v=>v.Name.Contains(name,StringComparison.OrdinalIgnoreCase));
}
