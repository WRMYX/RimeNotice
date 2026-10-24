using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimeNotice.Models;
namespace RimeNotice.Services;

// COM objects are created, used and released on their own STA; audio goes to
// memory, never to SAPI's default speaker, so RN keeps volume/mixing/cancellation.
public static class SapiSpeechBackend {
 private static object Create(string name)=>Activator.CreateInstance(Type.GetTypeFromProgID(name,true)!)!;
 private static void Release(object? value){if(value is not null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
 private static Task<T> StaAsync<T>(Func<T> action,CancellationToken token){
  token.ThrowIfCancellationRequested();var done=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
  var thread=new Thread(()=>{try{token.ThrowIfCancellationRequested();done.TrySetResult(action());}catch(OperationCanceledException){done.TrySetCanceled(token);}catch(Exception ex){done.TrySetException(ex);}}){IsBackground=true,Name="RN SAPI"};
  thread.SetApartmentState(ApartmentState.STA);thread.Start();return done.Task;
 }
 public static Task<LocalVoice[]> ScanAsync(CancellationToken token)=>StaAsync(()=>{
  object? voice=null,tokens=null;var found=new List<LocalVoice>();
  try{voice=Create("SAPI.SpVoice");tokens=((dynamic)voice).GetVoices("","");int count=((dynamic)tokens).Count;
   for(int i=0;i<count;i++){token.ThrowIfCancellationRequested();object? item=null;try{item=((dynamic)tokens).Item(i);string language="";try{string hex=((dynamic)item).GetAttribute("Language");if(int.TryParse(hex.Split(';')[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int lcid))language=CultureInfo.GetCultureInfo(lcid).Name;}catch(Exception){/* Optional token metadata. */}
    found.Add(new(LocalVoiceCatalog.SapiPrefix+(string)((dynamic)item).Id,(string)((dynamic)item).GetDescription(0),language,LocalVoiceSource.Sapi));
   }finally{Release(item);}}
   return found.ToArray();
  }finally{Release(tokens);Release(voice);}
 },token);
 public static Task<byte[]> SynthesizeAsync(string text,string voiceId,AudioOptions options,CancellationToken token)=>StaAsync(()=>{
  object? voice=null,tokens=null,selected=null,stream=null,format=null;
  try{
   if(!LocalVoiceCatalog.IsSapi(voiceId))throw new InvalidOperationException("不是 SAPI 声音标识。");
   voice=Create("SAPI.SpVoice");tokens=((dynamic)voice).GetVoices("","");string id=voiceId[LocalVoiceCatalog.SapiPrefix.Length..];
   for(int i=0;i<(int)((dynamic)tokens).Count;i++){object candidate=((dynamic)tokens).Item(i);if(string.Equals((string)((dynamic)candidate).Id,id,StringComparison.OrdinalIgnoreCase)){selected=candidate;break;}Release(candidate);}
   if(selected is null)throw new InvalidOperationException("所选 SAPI 声音已不可用，请刷新并重新选择。");
   stream=Create("SAPI.SpMemoryStream");format=((dynamic)stream).Format;((dynamic)format).Type=22; // SAFT22kHz16BitMono
   ((dynamic)stream).Format=format;((dynamic)voice).AudioOutputStream=stream;((dynamic)voice).Voice=selected;
   ((dynamic)voice).Volume=100;((dynamic)voice).Rate=(int)Math.Clamp(Math.Round(10*Math.Log2(options.SpeakingRate)),-10,10);
   string xml=$"<pitch absmiddle='{(int)Math.Clamp(Math.Round(10*Math.Log2(options.Pitch)),-10,10)}'>{SecurityElement.Escape(text)}</pitch>";
   token.ThrowIfCancellationRequested();((dynamic)voice).Speak(xml,1|8); // asynchronous, SAPI XML
   var clock=Stopwatch.StartNew();
   try{while(!(bool)((dynamic)voice).WaitUntilDone(40)){token.ThrowIfCancellationRequested();if(clock.Elapsed>TimeSpan.FromMinutes(2))throw new TimeoutException("SAPI 语音合成超时。");}}
   finally{if(token.IsCancellationRequested||clock.Elapsed>TimeSpan.FromMinutes(2))((dynamic)voice).Speak("",2);}
   token.ThrowIfCancellationRequested();byte[] pcm=(byte[])((dynamic)stream).GetData();
   if(pcm.Length==0)throw new InvalidOperationException("SAPI 没有生成音频。");
   // SpMemoryStream.GetData returns raw PCM; MediaPlayer expects a WAV container.
   using var wav=new MemoryStream();using(var writer=new BinaryWriter(wav,Encoding.ASCII,true)){
    writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+pcm.Length);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(22050);writer.Write(44100);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(pcm.Length);writer.Write(pcm);
   }return wav.ToArray();
  }finally{Release(format);Release(stream);Release(selected);Release(tokens);Release(voice);}
 },token);
}
