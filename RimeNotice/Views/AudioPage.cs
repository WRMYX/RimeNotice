using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RimeNotice.Models;
using RimeNotice.Services;
namespace RimeNotice.Views;
public sealed partial class AudioPage : SettingsFormPage {
 private NotificationAudioService? _preview;private SpeechService? _speech;
 private readonly ComboBox _voices=new(){Width=260,MinHeight=36};
 private readonly TextBlock _voiceStatus=new(){TextWrapping=TextWrapping.Wrap};
 private LocalVoice[] _localVoices=[];private bool _syncingVoices;private CancellationTokenSource? _scan;
 public AudioPage(){InitializeComponent();var a=Current.Audio;
  Unloaded+=(_,_)=>{_preview?.Dispose();_speech?.Dispose();_scan?.Cancel();};
  Section("提示音");
  Toggle("普通消息提示音",a.NormalEnabled,v=>Save(s=>s with{Audio=s.Audio with{NormalEnabled=v}}));
  Toggle("重要消息提示音",a.ImportantEnabled,v=>Save(s=>s with{Audio=s.Audio with{ImportantEnabled=v}}));
  Toggle("呼叫铃声",a.CallingEnabled,v=>Save(s=>s with{Audio=s.Audio with{CallingEnabled=v}}));
  Number("提示音音量（百分比）",a.Volume*100,0,100,v=>Save(s=>s with{Audio=s.Audio with{Volume=v/100}}));
  Group("自定义提示音与试听",()=>{
   File("普通消息音频",a.NormalFile,v=>Save(s=>s with{Audio=s.Audio with{NormalFile=v}}),[".wav",".mp3",".m4a",".wma"]);
   File("重要消息音频",a.ImportantFile,v=>Save(s=>s with{Audio=s.Audio with{ImportantFile=v}}),[".wav",".mp3",".m4a",".wma"]);
   File("呼叫音频",a.CallingFile,v=>Save(s=>s with{Audio=s.Audio with{CallingFile=v}}),[".wav",".mp3",".m4a",".wma"]);
   Toggle("呼叫循环播放",a.CallingLoop,v=>Save(s=>s with{Audio=s.Audio with{CallingLoop=v}}));
   foreach(var kind in Enum.GetValues<NoticePreviewKind>()){var k=kind;Button("试听"+(k==NoticePreviewKind.Calling?"呼叫铃声":k==NoticePreviewKind.Important?"重要提示音":"普通提示音"),async()=>{_preview?.Dispose();var player=new NotificationAudioService();_preview=player;try{await player.PreviewAsync(k,Current.Audio);}finally{player.Dispose();}});}
  });
  Section("语音播报");Toggle("语音播报",a.SpeechEnabled,v=>Save(s=>s with{Audio=s.Audio with{SpeechEnabled=v}}));
  var engines=new[]{SpeechEngine.Windows,SpeechEngine.Edge};
  var enginePicker=Choice("语音引擎",["Windows 本机语音","Edge 在线语音"],Array.IndexOf(engines,a.Engine),i=>Save(s=>s with{Audio=s.Audio with{Engine=engines[i]}}));
  Row("本机语音",_voices,"自动扫描 Windows 与 SAPI 可用声音，来源分别标注。");
  _voices.SelectionChanged+=(_,_)=>{if(!_syncingVoices&&_voices.SelectedIndex>=0)Save(s=>s with{Audio=s.Audio with{VoiceId=_voices.SelectedIndex==0?"":_localVoices[_voices.SelectedIndex-1].Id}});};
  Button("刷新本机语音",RefreshVoicesAsync);Rows.Children.Add(_voiceStatus);
  Button("下载中文语音包",()=>{App.Instance.Main?.NavigateTo("voices");return Task.CompletedTask;});
  var connection=new InfoBar{IsOpen=false,IsClosable=true};Rows.Children.Add(connection);
  Button("试听所选语音",async()=>{_speech?.Dispose();_speech=new SpeechService();await _speech.SpeakAsync("这是 RimeNotice 的语音试听。",Current.Audio with{FallbackToWindows=false},CancellationToken.None);if(_speech.LastError is{} error)throw new InvalidOperationException(error);connection.Title="试听结束";connection.Message="实际使用："+_speech.LastEngine;connection.IsOpen=true;});
  Button("停止试听",()=>{_preview?.Dispose();_speech?.Dispose();return Task.CompletedTask;});
  Group("朗读内容与声音参数",()=>{
   Toggle("朗读消息正文",a.ReadMessage,v=>Save(s=>s with{Audio=s.Audio with{ReadMessage=v}}));
   Toggle("朗读发送人姓名",a.ReadSender,v=>Save(s=>s with{Audio=s.Audio with{ReadSender=v}}));
   Number("语音音量（百分比）",a.SpeechVolume*100,0,100,v=>Save(s=>s with{Audio=s.Audio with{SpeechVolume=v/100}}));
   var rate=Number("语速",a.SpeakingRate,.5,2,v=>Save(s=>s with{Audio=s.Audio with{SpeakingRate=v}}));var pitch=Number("音调",a.Pitch,.5,2,v=>Save(s=>s with{Audio=s.Audio with{Pitch=v}}));
   void SyncParameters(){pitch.IsEnabled=Current.Audio.Engine==SpeechEngine.Windows;}
   enginePicker.SelectionChanged+=(_,_)=>SyncParameters();SyncParameters();Note("音调仅 Windows 本机语音支持；切换引擎会保留已保存的值。");
  });
  Group("同时播放与混音",()=>{
   Choice("音频优先级",["朗读优先（压低提示音）","提示音优先（压低朗读）","同时播放（不自动压低）"],(int)a.MixMode,i=>Save(s=>s with{Audio=s.Audio with{MixMode=(AudioMixMode)i}}));
   Number("被压低声音的保留音量（百分比）",a.DuckingGain*100,0,100,v=>Save(s=>s with{Audio=s.Audio with{DuckingGain=v/100}}));Note("各自设置的音量会保留，源音频响度不同，听感仍可能不同。");
  });
  Toggle("引擎失败时回退 Windows 本机语音",a.FallbackToWindows,v=>Save(s=>s with{Audio=s.Audio with{FallbackToWindows=v}}),"自动朗读允许回退；试听始终验证所选引擎，不回退。");
  Number("语音合成超时（秒）",a.TimeoutSeconds,5,180,v=>Save(s=>s with{Audio=s.Audio with{TimeoutSeconds=(int)v}}));
  Group("Edge 在线语音",()=>{
   Toggle("允许发送文本到语音服务",a.OnlineSpeechEnabled,v=>Save(s=>s with{Audio=s.Audio with{OnlineSpeechEnabled=v}}),"开启后会将消息文本发送至微软在线服务，默认关闭。");
   Text("Edge 中文语音",a.EdgeVoice,v=>Save(s=>s with{Audio=s.Audio with{EdgeVoice=v}}));

   Note("常用中文声音：zh-CN-XiaoxiaoNeural、zh-CN-YunxiNeural。在线兼容服务可能因服务端变化暂不可用。");
  });
  Loaded+=async(_,_)=>await RefreshVoicesAsync();
 }
 private async Task RefreshVoicesAsync(){
  _scan?.Cancel();var stop=new CancellationTokenSource();_scan=stop;
  try{
   _voiceStatus.Text="正在扫描系统声音…";var result=await LocalVoiceCatalog.ScanAsync(stop.Token);if(stop.IsCancellationRequested)return;
   _syncingVoices=true;_localVoices=result.Voices;_voices.Items.Clear();_voices.Items.Add("系统默认");foreach(var voice in _localVoices)_voices.Items.Add($"{voice.Name} · {voice.Language} · {voice.SourceName}");
   int selected=Array.FindIndex(_localVoices,v=>v.Id==Current.Audio.VoiceId);bool missing=Current.Audio.VoiceId.Length>0&&selected<0;_voices.SelectedIndex=missing?-1:selected+1;
   _voiceStatus.Text=(missing?"此前选择的声音已不可用，请重新选择；已保留原设置。":$"扫描到 {_localVoices.Length} 个 RN 可用本机声音。")+(result.Warnings.Length>0?"\n"+string.Join("\n",result.Warnings):"");
  }catch(OperationCanceledException){}catch(Exception ex){_voiceStatus.Text="本机声音扫描失败："+ex.Message;ServiceDiagnostics.Error("语音列表",ex);}
  finally{_syncingVoices=false;if(ReferenceEquals(_scan,stop))_scan=null;stop.Dispose();}
 }
}
