using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimeNotice.Models;
namespace RimeNotice.Services;
internal sealed record SpeechAudio(byte[] Bytes,string ContentType);
internal interface ISpeechProvider {Task<SpeechAudio> SynthesizeAsync(string text,AudioOptions options,CancellationToken token);}
internal sealed class EdgeSpeechProvider:ISpeechProvider {public Task<SpeechAudio> SynthesizeAsync(string text,AudioOptions options,CancellationToken token)=>OnlineSpeechProvider.EdgeAsync(text,options,token);}
internal sealed class CosySpeechProvider:ISpeechProvider {public Task<SpeechAudio> SynthesizeAsync(string text,AudioOptions options,CancellationToken token)=>OnlineSpeechProvider.CosyAsync(text,options,token);}
internal static class OnlineSpeechProvider {
 private const int MaximumBytes=24*1024*1024;
 public static Uri ServiceAddress(string address){
  if(!Uri.TryCreate(address,UriKind.Absolute,out var uri)||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)||!string.IsNullOrEmpty(uri.Query)||
   (uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback)))throw new ArgumentException("服务地址应为本机 HTTP 或 HTTPS 地址，不能包含账号、查询参数或片段。");return uri;
 }
 public static void SaveCredential(string address,string secret){var vault=new global::Windows.Security.Credentials.PasswordVault();string resource="RN.CosyVoice:"+ServiceAddress(address).GetLeftPart(UriPartial.Authority);
  try{vault.Remove(vault.Retrieve(resource,"token"));}catch(Exception){}if(secret.Length>0)vault.Add(new global::Windows.Security.Credentials.PasswordCredential(resource,"token",secret));
 }
 private static string Credential(Uri uri){try{var item=new global::Windows.Security.Credentials.PasswordVault().Retrieve("RN.CosyVoice:"+uri.GetLeftPart(UriPartial.Authority),"token");item.RetrievePassword();return item.Password;}catch(Exception){return "";}}
 public static Task<SpeechAudio> SynthesizeAsync(string text,AudioOptions options,CancellationToken token)=>(options.Engine==SpeechEngine.Edge?(ISpeechProvider)new EdgeSpeechProvider():new CosySpeechProvider()).SynthesizeAsync(text,options,token);
 public static async Task TestConnectionAsync(AudioOptions options,CancellationToken token){
  var uri=ServiceAddress(options.ServiceUrl);using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(uri,"/openapi.json"));string secret=Credential(uri);if(secret.Length>0)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);
  using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
 }
 internal static async Task<SpeechAudio> CosyAsync(string text,AudioOptions o,CancellationToken token){
  var uri=ServiceAddress(o.ServiceUrl);using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});using var request=new HttpRequestMessage(HttpMethod.Post,uri);
  using var form=new MultipartFormDataContent();form.Add(new StringContent(text),"tts_text");form.Add(new StringContent(o.ServiceVoice),"spk_id");request.Content=form;
  string secret=Credential(uri);if(secret.Length>0)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);
  using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
  using var stream=await response.Content.ReadAsStreamAsync(token);var data=await ReadBoundedAsync(stream,token);
  if(data.Length==0)throw new InvalidDataException("CosyVoice 返回空音频。");
  return o.ServiceReturnsPcm?new(SpeechAudioFormat.Wave(data,o.SampleRate),"audio/wav"):new(data,"audio/wav");
 }
 private static async Task<byte[]> ReadBoundedAsync(Stream input,CancellationToken token){using var output=new MemoryStream();byte[] buffer=new byte[32768];int count;while((count=await input.ReadAsync(buffer,token))>0){if(output.Length+count>MaximumBytes)throw new InvalidDataException("语音响应过大。");output.Write(buffer,0,count);}return output.ToArray();}
 internal static async Task<SpeechAudio> EdgeAsync(string text,AudioOptions o,CancellationToken token){
  // Optional compatibility protocol, based on rany2/edge-tts. No challenge retries or auth bypass.
  const string clientToken="6A5AA1D4EAFF4E9FB37E23D68491D6F4";
  long ticks=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+11644473600L;ticks-=ticks%300;
  string gec=Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes((ticks*10000000L).ToString(System.Globalization.CultureInfo.InvariantCulture)+clientToken)));
  using var socket=new ClientWebSocket();socket.Options.Proxy=HttpClient.DefaultProxy;socket.Options.SetRequestHeader("Origin","chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");socket.Options.SetRequestHeader("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
  await socket.ConnectAsync(new Uri($"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1?TrustedClientToken={clientToken}&ConnectionId={Guid.NewGuid():N}&Sec-MS-GEC={gec}&Sec-MS-GEC-Version=2-143.0.3650.75"),token);
  async Task Send(string value)=>await socket.SendAsync(Encoding.UTF8.GetBytes(value),WebSocketMessageType.Text,true,token);
  await Send("X-Timestamp:"+DateTime.UtcNow.ToString("r")+"\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}");
  string voice=SecurityElement.Escape(o.EdgeVoice)!,escaped=SecurityElement.Escape(text)!;int rate=(int)Math.Round((o.SpeakingRate-1)*100),pitch=(int)Math.Round((o.Pitch-1)*100);
  await Send($"X-RequestId:{Guid.NewGuid():N}\r\nContent-Type:application/ssml+xml\r\nX-Timestamp:{DateTime.UtcNow:r}\r\nPath:ssml\r\n\r\n<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='zh-CN'><voice name='{voice}'><prosody rate='{rate:+0;-0;0}%' pitch='{pitch:+0;-0;0}Hz'>{escaped}</prosody></voice></speak>");
  using var audio=new MemoryStream();byte[] buffer=new byte[16384];bool finished=false;
  while(!finished){using var frame=new MemoryStream();WebSocketReceiveResult result;do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token);if(frame.Length+result.Count>MaximumBytes)throw new InvalidDataException("语音帧过大。");frame.Write(buffer,0,result.Count);}while(!result.EndOfMessage);
   var bytes=frame.ToArray();if(result.MessageType==WebSocketMessageType.Close)break;
   if(result.MessageType==WebSocketMessageType.Text){string message=Encoding.UTF8.GetString(bytes);if(message.Contains("Path:turn.end"))finished=true;else if(message.Contains("Path:response")&&message.Contains("error",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Edge 语音服务拒绝了请求。");}
   else if(bytes.Length>=2){int header=(bytes[0]<<8)|bytes[1];if(header+2>bytes.Length)throw new InvalidDataException("语音帧头无效。");string headers=Encoding.UTF8.GetString(bytes,2,header);if(headers.Contains("Path:audio")){if(audio.Length+bytes.Length-header-2>MaximumBytes)throw new InvalidDataException("语音响应过大。");audio.Write(bytes,header+2,bytes.Length-header-2);}}
  }
  if(!finished||audio.Length==0)throw new InvalidDataException("Edge 未返回完整音频。");return new(audio.ToArray(),"audio/mpeg");
 }
}
