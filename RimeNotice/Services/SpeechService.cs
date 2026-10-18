using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed class SpeechService : IDisposable {
 public string? LastError {get;private set;}
 public string LastEngine {get;private set;}="Windows 本机语音";
 private MediaPlayer? _player; private SpeechSynthesizer? _synth; private MediaSource? _source;
 private readonly CancellationTokenSource _stop=new(); private bool _disposed;
 public async Task SpeakAsync(string text,AudioOptions options,CancellationToken token) {
  foreach(string part in SpeechText.Chunks(text)){if(_disposed||token.IsCancellationRequested)return;await SpeakChunkAsync(part,options,token);if(LastError is not null)return;}
 }
 private async Task SpeakChunkAsync(string text,AudioOptions options,CancellationToken token) {
  if(_disposed)return;
  using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,_stop.Token);
  try {
   linked.Token.ThrowIfCancellationRequested();LastError=null;
   if(options.Engine!=SpeechEngine.Windows&&options.OnlineSpeechEnabled){
    try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(linked.Token);timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
     LastEngine=options.Engine==SpeechEngine.Edge?"Edge 在线语音":"CosyVoice 独立服务";
     var prepared=System.Diagnostics.Stopwatch.StartNew();var audio=await OnlineSpeechProvider.SynthesizeAsync(text,options,timeout.Token);
     ServiceDiagnostics.Set("在线语音准备",$"音频准备耗时 {prepared.ElapsedMilliseconds} ms，正文 {text.Length} 字符；非首音测量");
     linked.Token.ThrowIfCancellationRequested();if(_disposed)return;
     using var memory=new System.IO.MemoryStream(audio.Bytes);using var random=memory.AsRandomAccessStream();
     await PlayAsync(random,audio.ContentType,options.SpeechVolume,linked.Token);return;
    }catch(OperationCanceledException)when(linked.IsCancellationRequested){throw;}
    catch(Exception ex){Release();ServiceDiagnostics.Error("在线语音",ex);if(!options.FallbackToWindows)throw;ServiceDiagnostics.Set("语音回退","在线服务失败，改用 Windows 本机语音");}
   }
   LastEngine="Windows 本机语音";_synth=new SpeechSynthesizer();
   var voice=SpeechSynthesizer.AllVoices.FirstOrDefault(v=>v.Id==options.VoiceId);
   if(voice is not null)_synth.Voice=voice;
   _synth.Options.SpeakingRate=options.SpeakingRate;_synth.Options.AudioPitch=options.Pitch;_synth.Options.AudioVolume=options.SpeechVolume;
   var operation=_synth.SynthesizeTextToStreamAsync(text);
   using var cancel=linked.Token.Register(()=>{try{operation.Cancel();}catch(Exception){}});
   using var stream=await operation;linked.Token.ThrowIfCancellationRequested();if(_disposed)return;
   await PlayAsync(stream,stream.ContentType,1,linked.Token);
  }catch(OperationCanceledException){}catch(Exception ex){LastError="语音未能播放，请检查服务配置或本机语音组件。";ServiceDiagnostics.Error("语音",ex);}
  finally{Release();}
 }
 private async Task PlayAsync(global::Windows.Storage.Streams.IRandomAccessStream stream,string contentType,double volume,CancellationToken token){
  token.ThrowIfCancellationRequested();if(_disposed)return;try{
  _source=MediaSource.CreateFromStream(stream,contentType);_player=new MediaPlayer{AutoPlay=false,Volume=volume};_player.CommandManager.IsEnabled=false;
  var ended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);_player.MediaEnded+=(_,_)=>ended.TrySetResult();_player.MediaFailed+=(_,_)=>ended.TrySetException(new InvalidOperationException("语音播放失败"));
  _player.Source=_source;_player.Play();await ended.Task.WaitAsync(TimeSpan.FromMinutes(5),token);
  }finally{Release();}
 }
 private void Release(){try{if(_player is not null){_player.Pause();_player.Source=null;_player.Dispose();_player=null;}_source?.Dispose();_source=null;_synth?.Dispose();_synth=null;}catch(Exception ex){ServiceDiagnostics.Error("语音释放",ex);}}
 public void Dispose(){if(_disposed)return;_disposed=true;_stop.Cancel();Release();}
}
