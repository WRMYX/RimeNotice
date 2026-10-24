using System;
using System.Linq;
namespace RimeNotice.Models;
public enum WindowMaterial { Mica, MicaAlt, Acrylic, Solid }
public enum NotificationPresentation { Native, Material }
public enum NativeNotificationHost { Compact, Fullscreen }
public static class NotificationPresentationSession {
 // A mode change ends the current display session before touching the waiting queue.
 public static T? TakeNext<T>(NotificationPresentation active,NotificationPresentation selected,Func<T?> dequeue) where T:class => active==selected?dequeue():null;
 public static NativeNotificationHost EffectiveHost(AppearanceOptions options) => options.Presentation==NotificationPresentation.Native&&options.NativeHost==NativeNotificationHost.Fullscreen&&options.Mask?NativeNotificationHost.Fullscreen:NativeNotificationHost.Compact;
 public static T? TakeNext<T>(AppearanceOptions active,AppearanceOptions selected,Func<T?> dequeue) where T:class => active.Presentation==selected.Presentation&&EffectiveHost(active)==EffectiveHost(selected)?dequeue():null;
}
public sealed record AppearanceOptions {
 public NotificationPresentation Presentation { get; init; } = NotificationPresentation.Native;
 public NativeNotificationHost NativeHost { get; init; } = NativeNotificationHost.Compact;
 public WindowMaterial SettingsMaterial { get; init; } = WindowMaterial.Mica;
 public WindowMaterial NotificationMaterial { get; init; } = WindowMaterial.Mica;
 public string SolidColor { get; init; } = "";
 public int NotificationTheme { get; init; }
 public double Width { get; init; } = 480;
 public double Height { get; init; } = 420;
 public double SenderSize { get; init; } = 28;
 public double MessageSize { get; init; } = 20;
 public double ButtonSize { get; init; } = 14;
 public bool TouchFriendlyMenus { get; init; }
 public string FontFamily { get; init; } = "Segoe UI Variable";
 public string SenderFontFamily { get; init; } = "";
 public string MessageFontFamily { get; init; } = "";
 public string SenderFontFile { get; init; } = "";
 public string MessageFontFile { get; init; } = "";
 public bool Animation { get; init; } = true;
 public bool Shadow { get; init; } = true;
 public bool Mask { get; init; } = false;
 public bool AlwaysOnTop { get; init; } = true;
 public bool ShowQQLogo { get; init; } = true;
 public bool ShowDeveloperLogo { get; init; } = true;
 public double CommandTint { get; init; } = .42;
 public string NormalColor { get; init; } = "";
 public string ImportantColor { get; init; } = "";
 public string CallingColor { get; init; } = "";
 public MaterialMotionOptions MaterialMotion { get; init; } = new();
 public string Footer { get; init; } = "";
 public string ConfirmText { get; init; } = "收到";
 public string CloseText { get; init; } = "关闭";
 public string ConfirmIcon { get; init; } = "";
 public string CloseIcon { get; init; } = "";
 public AppearanceOptions Normalize() => this with {
  CommandTint=NotificationLayout.Finite(CommandTint,.42,0,1),
  NormalColor=NoticeColor.Normalize(NormalColor),ImportantColor=NoticeColor.Normalize(ImportantColor),CallingColor=NoticeColor.Normalize(CallingColor),
  MaterialMotion=(MaterialMotion??new()).Normalize(),
  Presentation = Enum.IsDefined(Presentation) ? Presentation : NotificationPresentation.Native,
  NativeHost = Enum.IsDefined(NativeHost) ? NativeHost : NativeNotificationHost.Compact,
  SettingsMaterial = Enum.IsDefined(SettingsMaterial) ? SettingsMaterial : WindowMaterial.Mica,
  NotificationMaterial = Enum.IsDefined(NotificationMaterial) ? NotificationMaterial : WindowMaterial.Mica,
  Width = NotificationLayout.Finite(Width,480,360,560), Height = NotificationLayout.Finite(Height,420,220,800),
  SenderSize = NotificationLayout.Finite(SenderSize,28,20,60), MessageSize = NotificationLayout.Finite(MessageSize,20,14,40),
  ButtonSize = NotificationLayout.Finite(ButtonSize,14,12,28), NotificationTheme = Math.Clamp(NotificationTheme,0,2),
  SenderFontFamily=SenderFontFamily??"",MessageFontFamily=MessageFontFamily??"",SenderFontFile=SenderFontFile??"",MessageFontFile=MessageFontFile??"",
  FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI Variable" : FontFamily.Trim(),
  SolidColor = SolidColor ?? "", Footer = Footer ?? "", ConfirmText = string.IsNullOrWhiteSpace(ConfirmText) ? "收到" : ConfirmText,
  CloseText = string.IsNullOrWhiteSpace(CloseText) ? "关闭" : CloseText, ConfirmIcon = ConfirmIcon ?? "", CloseIcon = CloseIcon ?? ""
 };
}
public enum SpeechEngine { Windows=0, Edge=1, CosyVoice=2 }
public enum AudioMixMode { SpeechPriority, ReminderPriority, Parallel }
public sealed record AudioOptions {
 public AudioMixMode MixMode {get;init;} = AudioMixMode.SpeechPriority;
 public double DuckingGain {get;init;} = .25;
 public bool ReadMessage {get;init;} = true;
 public bool ReadSender {get;init;}

 public SpeechEngine Engine {get;init;}
 public bool OnlineSpeechEnabled {get;init;}
 public bool FallbackToWindows {get;init;} = true;
 public int TimeoutSeconds {get;init;} = 30;
 public string EdgeVoice {get;init;} = "zh-CN-XiaoxiaoNeural";
 public string ServiceUrl {get;init;} = "http://127.0.0.1:50000/inference_sft";
 public string ServiceVoice {get;init;} = "中文女";
 public bool ServiceReturnsPcm {get;init;} = true;
 public int SampleRate {get;init;} = 22050;
 public bool NormalEnabled { get; init; }
 public bool ImportantEnabled { get; init; }
 public bool CallingEnabled { get; init; }
 public bool CallingLoop { get; init; } = true;
 public string NormalFile { get; init; } = "";
 public string ImportantFile { get; init; } = "";
 public string CallingFile { get; init; } = "";
 public double Volume { get; init; } = .8;
 public bool SpeechEnabled { get; init; }
 public string VoiceId { get; init; } = "";
 public double SpeakingRate { get; init; } = 1;
 public double Pitch { get; init; } = 1;
 public double SpeechVolume { get; init; } = .8;
 public bool Enabled(NoticePreviewKind k) => k switch { NoticePreviewKind.Calling=>CallingEnabled, NoticePreviewKind.Important=>ImportantEnabled,_=>NormalEnabled };
 public string File(NoticePreviewKind k) => k switch { NoticePreviewKind.Calling=>CallingFile, NoticePreviewKind.Important=>ImportantFile,_=>NormalFile };
 public AudioOptions Normalize() => this with {
  MixMode=Enum.IsDefined(MixMode)?MixMode:AudioMixMode.SpeechPriority,DuckingGain=NotificationLayout.Finite(DuckingGain,.25,0,1),
  Engine=Enum.IsDefined(Engine)&&Engine!=SpeechEngine.CosyVoice?Engine:SpeechEngine.Windows,TimeoutSeconds=Math.Clamp(TimeoutSeconds,5,180),SampleRate=Math.Clamp(SampleRate,8000,96000),
  EdgeVoice=EdgeVoice??"zh-CN-XiaoxiaoNeural",ServiceUrl=ServiceUrl??"",ServiceVoice=ServiceVoice??"",
  Volume=NotificationLayout.Finite(Volume,.8,0,1),SpeechVolume=NotificationLayout.Finite(SpeechVolume,.8,0,1),
  SpeakingRate=NotificationLayout.Finite(SpeakingRate,1,.5,2),Pitch=NotificationLayout.Finite(Pitch,1,.5,2),
  NormalFile=NormalFile??"",ImportantFile=ImportantFile??"",CallingFile=CallingFile??"",VoiceId=VoiceId??""
 };
}
public sealed record ReceptionOptions {
 public ImageDisplayOptions Images {get;init;} = new();
 public double ScanSeconds { get; init; } = .3;
 public bool EventDriven { get; init; } = true;
 public bool QQOnly { get; init; } = true;
 public bool UseUIAutomation { get; init; }
 public bool PreviewImages { get; init; }
 public bool InferRecentImages { get; init; }
 public string QQAccount { get; init; } = "";
 public string CacheDirectory { get; init; } = "";
 public int AttachmentWaitSeconds { get; init; } = 5;
 public ReceptionOptions Normalize() => this with { Images=(Images??new()).Normalize(),QQOnly=true,ScanSeconds=NotificationLayout.Finite(ScanSeconds,.3,.1,5),
  AttachmentWaitSeconds=Math.Clamp(AttachmentWaitSeconds,0,30), QQAccount=new string((QQAccount??"").Where(char.IsDigit).Take(20).ToArray()), CacheDirectory=CacheDirectory??"" };
}
public sealed record CallingOptions {
 public bool UntilAcknowledged { get; init; }
 public bool Breathing { get; init; } = false;
 public int BeatsPerMinute { get; init; } = 30;
 public CallingOptions Normalize() => this with { BeatsPerMinute=Math.Clamp(BeatsPerMinute,10,120) };
}
