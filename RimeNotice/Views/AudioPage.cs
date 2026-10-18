using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using RimeNotice.Models;
using RimeNotice.Services;
namespace RimeNotice.Views;
public sealed partial class AudioPage : SettingsFormPage {
 private NotificationAudioService? _preview;private SpeechService? _speech;
 public AudioPage(){InitializeComponent();
  Unloaded+=(_,_)=>{_preview?.Dispose();_speech?.Dispose();};var a=Current.Audio;
  Section("提示音");Toggle("普通消息提示音",a.NormalEnabled,v=>Save(s=>s with{Audio=s.Audio with{NormalEnabled=v}}));
  File("普通消息音频",a.NormalFile,v=>Save(s=>s with{Audio=s.Audio with{NormalFile=v}}),[".wav",".mp3",".m4a",".wma"]);
  Toggle("重要消息提示音",a.ImportantEnabled,v=>Save(s=>s with{Audio=s.Audio with{ImportantEnabled=v}}));
  File("重要消息音频",a.ImportantFile,v=>Save(s=>s with{Audio=s.Audio with{ImportantFile=v}}),[".wav",".mp3",".m4a",".wma"]);
  Toggle("呼叫铃声",a.CallingEnabled,v=>Save(s=>s with{Audio=s.Audio with{CallingEnabled=v}}));
  File("呼叫音频",a.CallingFile,v=>Save(s=>s with{Audio=s.Audio with{CallingFile=v}}),[".wav",".mp3",".m4a",".wma"]);
  Toggle("呼叫循环播放",a.CallingLoop,v=>Save(s=>s with{Audio=s.Audio with{CallingLoop=v}}));
  Number("提示音音量（百分比）",a.Volume*100,0,100,v=>Save(s=>s with{Audio=s.Audio with{Volume=v/100}}));
  foreach(var kind in Enum.GetValues<NoticePreviewKind>()){var k=kind;Button("试听"+(k==NoticePreviewKind.Calling?"呼叫铃声":k==NoticePreviewKind.Important?"重要提示音":"普通提示音"),async()=>{
    _preview?.Dispose();var player=new NotificationAudioService();_preview=player;await player.StartAsync(k,Current.Audio with{NormalEnabled=true,ImportantEnabled=true,CallingEnabled=true,SpeechEnabled=false},"");await Task.Delay(k==NoticePreviewKind.Calling?5000:2000);player.Dispose();});}
  Section("语音播报");Toggle("语音播报",a.SpeechEnabled,v=>Save(s=>s with{Audio=s.Audio with{SpeechEnabled=v}}));
  Choice("语音引擎",["Windows 本机语音","Edge 在线语音"],Math.Clamp((int)a.Engine,0,1),i=>Save(s=>s with{Audio=s.Audio with{Engine=(SpeechEngine)i}}));
  Toggle("允许发送文本到语音服务",a.OnlineSpeechEnabled,v=>Save(s=>s with{Audio=s.Audio with{OnlineSpeechEnabled=v}}),"开启后，Edge 将消息发送至微软在线服务。默认关闭。");
  Toggle("服务失败时回退本机语音",a.FallbackToWindows,v=>Save(s=>s with{Audio=s.Audio with{FallbackToWindows=v}}));
  Number("在线服务超时（秒）",a.TimeoutSeconds,5,180,v=>Save(s=>s with{Audio=s.Audio with{TimeoutSeconds=(int)v}}));
  Text("Edge 中文语音",a.EdgeVoice,v=>Save(s=>s with{Audio=s.Audio with{EdgeVoice=v}}));
  Note("常用中文声音：zh-CN-XiaoxiaoNeural、zh-CN-YunxiNeural。Edge 为可选兼容服务，可能因服务端变化暂不可用。");
  var connection=new Microsoft.UI.Xaml.Controls.InfoBar{IsOpen=false,IsClosable=true};Rows.Children.Add(connection);
  try{var voices=SpeechSynthesizer.AllVoices.ToArray();Choice("本机语音",new[]{"系统默认"}.Concat(voices.Select(v=>v.DisplayName)).ToArray(),Array.FindIndex(voices,v=>v.Id==a.VoiceId)+1,i=>Save(s=>s with{Audio=s.Audio with{VoiceId=i==0?"":voices[i-1].Id}}));}
  catch(Exception ex){ServiceDiagnostics.Error("语音列表",ex);Note("本机语音组件不可用，请检查 Windows 语音功能。");}
  Number("语音音量（百分比）",a.SpeechVolume*100,0,100,v=>Save(s=>s with{Audio=s.Audio with{SpeechVolume=v/100}}));
  Number("语速",a.SpeakingRate,.5,2,v=>Save(s=>s with{Audio=s.Audio with{SpeakingRate=v}}));Number("音调",a.Pitch,.5,2,v=>Save(s=>s with{Audio=s.Audio with{Pitch=v}}));
  Button("试听所选语音",async()=>{_speech?.Dispose();_speech=new SpeechService();await _speech.SpeakAsync("这是 RimeNotice 的语音试听。",Current.Audio,CancellationToken.None);if(_speech.LastError is{} error)throw new InvalidOperationException(error);connection.Title="试听结束";connection.Message="实际使用："+_speech.LastEngine+"。可在调试页查看回退原因。";connection.Severity=Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;connection.IsOpen=true;});
  Button("停止试听",()=>{_preview?.Dispose();_speech?.Dispose();return Task.CompletedTask;});
  Note("在线语音未启用时使用本机 Windows 语音。播报时提示音会降低音量，关闭通知后停止。");
 }
}
