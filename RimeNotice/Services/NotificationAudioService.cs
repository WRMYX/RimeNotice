using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using RimeNotice.Models;
namespace RimeNotice.Services;
public sealed class NotificationAudioService : IDisposable {
 private readonly CancellationTokenSource _stop=new();private readonly SpeechService _speech=new();
 private MediaPlayer? _player;private MediaSource? _source;private bool _disposed;
 public async Task StartAsync(NoticePreviewKind kind,AudioOptions options,string text) {
  try {
   if(_disposed||_stop.IsCancellationRequested)return;
   if(options.Enabled(kind)) {
    try {
    string file=options.File(kind);if(string.IsNullOrWhiteSpace(file))file=Path.Combine(AppContext.BaseDirectory,"Assets","Audio",kind+".wav");
    if(!File.Exists(file))throw new FileNotFoundException("提示音文件不可用");
    _source=MediaSource.CreateFromUri(new Uri(Path.GetFullPath(file)));_player=new MediaPlayer {Volume=options.Volume,IsLoopingEnabled=kind==NoticePreviewKind.Calling&&options.CallingLoop};
    _player.CommandManager.IsEnabled=false;_player.MediaFailed+=(_,_)=>ServiceDiagnostics.Set("提示音","播放失败，请检查格式或媒体组件");
    _player.Source=_source;_player.Play();ServiceDiagnostics.Set("提示音","播放中");
    } catch(Exception ex){ServiceDiagnostics.Error("提示音文件",ex);}
   }
   if(options.SpeechEnabled&&!string.IsNullOrWhiteSpace(text)) {
    if(_player is not null)_player.Volume=options.Volume*.15;
    await _speech.SpeakAsync(text,options,_stop.Token);
    if(!_disposed&&_player is not null)_player.Volume=options.Volume;
   }
  }catch(OperationCanceledException){}catch(Exception ex){ServiceDiagnostics.Error("提示音",ex);}
 }
 public void Dispose(){if(_disposed)return;_disposed=true;_stop.Cancel();_speech.Dispose();try{if(_player is not null){_player.Pause();_player.Source=null;_player.Dispose();_player=null;}_source?.Dispose();_source=null;}catch(Exception ex){ServiceDiagnostics.Error("提示音释放",ex);}}
}
