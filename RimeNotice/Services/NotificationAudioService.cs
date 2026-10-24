using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed class NotificationAudioService : IDisposable
{
 private readonly CancellationTokenSource _stop=new();private readonly SpeechService _speech=new();
 private readonly TaskCompletionSource<bool> _ended=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private readonly object _mixGate=new();
 private MediaPlayer? _player;private MediaSource? _source;private bool _disposed,_tonePlaying;
 private readonly TaskCompletionSource _ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private bool _prepared;
 public async Task PrepareAsync(NoticePreviewKind kind,AudioOptions options)
 {
  if(_disposed||_prepared)return;_prepared=true;
  if(!options.Enabled(kind)){_ready.TrySetResult();_ended.TrySetResult(true);return;}
  try{
   string file=options.File(kind);if(string.IsNullOrWhiteSpace(file))file=Path.Combine(AppContext.BaseDirectory,"Assets","Audio",kind==NoticePreviewKind.Important?"Important.mp3":kind+".wav");
   if(!File.Exists(file))throw new FileNotFoundException("提示音文件不可用");
   _source=MediaSource.CreateFromUri(new Uri(Path.GetFullPath(file)));
   _player=new MediaPlayer{AutoPlay=false,Volume=options.Volume,IsLoopingEnabled=kind==NoticePreviewKind.Calling&&options.CallingLoop};_player.CommandManager.IsEnabled=false;
   _player.MediaOpened+=(_,_)=>_ready.TrySetResult();
   _player.MediaEnded+=(_,_)=>{lock(_mixGate){_tonePlaying=false;_speech.SetMixGain(1);}_ended.TrySetResult(true);};
   _player.MediaFailed+=(_,_)=>{lock(_mixGate){_tonePlaying=false;_speech.SetMixGain(1);}_ready.TrySetResult();_ended.TrySetResult(false);ServiceDiagnostics.Set("提示音","播放失败，请检查格式或媒体组件");};
   _player.Source=_source;
   try{await _ready.Task.WaitAsync(TimeSpan.FromSeconds(2),_stop.Token);}catch(TimeoutException){ServiceDiagnostics.Set("提示音","预加载超时，尝试播放");}
  }catch(OperationCanceledException){}catch(Exception ex){_ended.TrySetResult(false);ServiceDiagnostics.Error("提示音文件",ex);}
 }
 public async Task StartAsync(NoticePreviewKind kind,AudioOptions options,string text)
 {
  try {
   await PrepareAsync(kind,options);if(_disposed)return;
   if(_player is not null){_tonePlaying=true;_player.Play();ServiceDiagnostics.Set("提示音","播放中");}
   if(options.SpeechEnabled&&!string.IsNullOrWhiteSpace(text)){
    lock(_mixGate){
     var gains=AudioMixPolicy.Gains(options,_tonePlaying);
     if(_player is not null)_player.Volume=options.Volume*gains.Tone;
     _speech.SetMixGain(gains.Speech);
    }
    try{await _speech.SpeakAsync(text,options,_stop.Token);}
    finally{if(!_disposed&&_player is not null)_player.Volume=options.Volume;_speech.SetMixGain(1);}
   }
  }catch(OperationCanceledException){}catch(Exception ex){ServiceDiagnostics.Error("提示音",ex);}
 }
 public async Task PreviewAsync(NoticePreviewKind kind,AudioOptions options)
 {
  await StartAsync(kind,options with{NormalEnabled=true,ImportantEnabled=true,CallingEnabled=true,CallingLoop=false,SpeechEnabled=false},"");
  try{if(!await _ended.Task.WaitAsync(TimeSpan.FromMinutes(5),_stop.Token))throw new InvalidOperationException("音频无法完整播放，请检查文件格式或媒体组件。");}
  catch(OperationCanceledException)when(_stop.IsCancellationRequested){}
  catch(TimeoutException){throw new InvalidOperationException("音频试听超时，请停止试听并检查文件。");}
 }
 public void Dispose(){if(_disposed)return;_disposed=true;_stop.Cancel();_ended.TrySetCanceled(_stop.Token);_speech.Dispose();try{if(_player is not null){_player.Pause();_player.Source=null;_player.Dispose();_player=null;}_source?.Dispose();_source=null;}catch(Exception ex){ServiceDiagnostics.Error("提示音释放",ex);}}
}
