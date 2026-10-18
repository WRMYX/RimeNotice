using RimeNotice.Models;
using RimeNotice.Services;
int checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
try {
var s = new RuleSettings { CooldownSeconds = 0, ImportantContacts = ["班主任"], ImportantKeywords = ["考试"], Blacklist = ["广告"], Whitelist = [] };
var engine = new NotificationRuleEngine();
Check(engine.Evaluate("朋友", "你好", s).Kind == NoticePreviewKind.Normal, "normal");
Check(engine.Evaluate("班主任", "你好", s).Kind == NoticePreviewKind.Important, "contact");
Check(engine.Evaluate("朋友", "考试", s).Kind == NoticePreviewKind.Important, "keyword");
Check(engine.Evaluate("群", "[有人@你]你好", s).Kind == NoticePreviewKind.Important, "mention");
Check(engine.Evaluate("群", "@其他人你好", s).Kind == NoticePreviewKind.Normal, "other mention");
Check(engine.Evaluate("班主任", "呼叫：考试", s).Kind == NoticePreviewKind.Calling, "calling priority");
Check(!engine.Evaluate("班主任", "呼叫广告", s).Accepted, "blacklist priority");
Check(!engine.Evaluate("朋友", "你好", s with { Whitelist = ["班主任"] }).Accepted, "whitelist");
Check(engine.Evaluate("朋友", "呼叫", s with { CallingEnabled = false }).Kind == NoticePreviewKind.Normal, "calling disabled");
Check(engine.Evaluate("群", "有人@我", s with { MentionImportant = false }).Kind == NoticePreviewKind.Normal, "mention disabled");
var clock = new ManualClock(); var dedup = new NotificationRuleEngine(clock); var cooldown = s with { CooldownSeconds = 3 };
Check(dedup.Evaluate("甲", "消息", cooldown).Accepted, "first");
Check(!dedup.Evaluate("甲", "消息", cooldown).Accepted, "duplicate");
Check(dedup.Evaluate("乙", "消息", cooldown).Accepted, "different sender");
clock.Advance(3); Check(dedup.Evaluate("甲", "消息", cooldown).Accepted, "cooldown expiry");
for (int i=0;i<600;i++) dedup.Evaluate("群", "消息" + i, cooldown);
Check(dedup.RetainedDigestCount <= 512, "bounded digests");
Check(RuleSettings.Terms(" A\r\na\n B ").SequenceEqual(new[]{"A","B"}), "terms normalization");
var folder = Path.Combine(Path.GetTempPath(), "RN-RuleChecks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
try {
    var file = Path.Combine(folder, "settings.json"); var store = new SettingsStore(file);
    store.Save(s with { NormalSeconds = 21, Theme = 2 }); var restored = new SettingsStore(file);
    Check(restored.Current.NormalSeconds == 21 && restored.Current.Theme == 2 && restored.Current.ImportantContacts.SequenceEqual(s.ImportantContacts), "round trip");
    File.WriteAllText(file, "broken"); var broken = new SettingsStore(file);
    Check(broken.Current.NormalSeconds == 10 && broken.LastError != "无", "corrupt read");
    var blocked = new SettingsStore(folder); bool failed = false; try { blocked.Save(s with { NormalSeconds = 99 }); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed=true; }
    Check(failed && blocked.Current.NormalSeconds == 10 && blocked.LastError != "无", "failed write atomic state");
    var bounded = (s with { NormalSeconds=-1, CallingSeconds=99999, CooldownSeconds=200 }).Normalize();
    Check(bounded.NormalSeconds == 1 && bounded.CallingSeconds == 3600 && bounded.CooldownSeconds == 60, "bounds");
} finally { foreach(var f in Directory.GetFiles(folder)) File.Delete(f); Directory.Delete(folder); }
Check(new RuleSettings().SettingsAlwaysOnTop,"settings window defaults topmost");
Check((new ReceptionOptions{QQOnly=false}).Normalize().QQOnly,"legacy all-app option becomes QQ-only");
Check((new UpdateOptions{SourceId="unknown"}).Normalize().SourceId=="direct","unknown update source falls back");
Check((new RuleSettings{SchemaVersion=5,Appearance=new AppearanceOptions{Height=250}}).Normalize().Appearance.Height==420,"old maximum allows multiline growth");
Check(NotificationLayout.CompactHeight(420,120,40)<NotificationLayout.CompactHeight(420,120,250),"long message grows before cap");
Check(NotificationLayout.CompactHeight(420,120,4000)==420,"large text is capped");
foreach(double available in new[]{240d,400d,800d}) {
 var shortCard=NotificationLayout.MeasureCard(new AppearanceOptions{Height=600},1280,available,120,40);
 var longCard=NotificationLayout.MeasureCard(new AppearanceOptions{Height=600},1280,available,120,2000);
 Check(shortCard.Height<=longCard.Height&&longCard.Height<=available-32,"content grows within work area");
}
var revisions=new NotificationRevisionTracker();
Check(revisions.Observe("QQ:1","张老师","第一段"),"new toast");
Check(!revisions.Observe("QQ:1","张老师","第一段"),"identical toast not repeated");
Check(revisions.Observe("QQ:1","张老师","第一段\n第二段"),"same ID body update preserved");
var longSpeech=string.Concat(Enumerable.Repeat("这是完整正文。😀\n",600));
var chunks=SpeechText.Chunks(longSpeech).ToArray();Check(string.Concat(chunks)==longSpeech&&chunks.All(x=>x.Length<=800),"speech never truncates long payload");
Check(chunks.All(x=>!char.IsHighSurrogate(x[^1])),"speech does not split surrogate pairs");
bool badFont=false;try{RimeNotice.Core.FontMetadata.Family(new byte[16]);}catch(InvalidDataException){badFont=true;}Check(badFont,"malformed font rejected");
string systemFont=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");if(File.Exists(systemFont))Check(RimeNotice.Core.FontMetadata.Family(File.ReadAllBytes(systemFont)).Contains("Arial",StringComparison.OrdinalIgnoreCase),"native TTF family parsed");
var appearance=new AppearanceOptions();var expected=NotificationLayout.Fit(appearance,1920,1080);
Check(expected==new CardSize(480,420),"default landscape");
for(int seconds=3600;seconds>=0;seconds-=9)Check(NotificationLayout.Fit(appearance,1920,1080)==expected,"countdown independent geometry");
Check(NotificationLayout.Fit(appearance,500,400)==new CardSize(468,368),"small work area");
Check(NotificationLayout.Remaining(10,TimeSpan.FromSeconds(9.1))==1&&NotificationLayout.Remaining(10,TimeSpan.FromSeconds(11))==0,"countdown ceil");
Check(!NotificationLayout.AcceptSource("其他应用",new ReceptionOptions())&&!NotificationLayout.AcceptSource("其他应用",new ReceptionOptions{QQOnly=false}),"source filter");
var migrated=System.Text.Json.JsonSerializer.Deserialize<RuleSettings>("{\"NormalSeconds\":23,\"ImportantContacts\":[\"老师\"]}")!.Normalize();
Check(migrated.NormalSeconds==23&&migrated.Appearance.Width==480&&migrated.Audio.SpeechEnabled==false,"old settings migration");
var nullGroups=System.Text.Json.JsonSerializer.Deserialize<RuleSettings>("{\"Appearance\":null,\"Audio\":null,\"Reception\":null}")!.Normalize();
Check(nullGroups.Appearance.Width==480&&nullGroups.Reception.QQOnly,"null settings fallback");
Check((new AppearanceOptions{Width=double.NaN,Height=99999}).Normalize().Width==480,"appearance bounds");
var oldDefaults=(new RuleSettings{SchemaVersion=2,Appearance=new AppearanceOptions{Width=720,Height=420,SenderSize=36,MessageSize=22},NormalSeconds=23,ImportantKeywords=["考试"]}).Normalize();
Check(oldDefaults.SchemaVersion==6&&oldDefaults.Appearance.Width==480&&oldDefaults.Appearance.Height==420&&oldDefaults.Appearance.SenderSize==28&&oldDefaults.Appearance.MessageSize==20&&oldDefaults.NormalSeconds==23&&oldDefaults.ImportantKeywords[0]=="考试","old default migration preserves unrelated settings");
var migratedAgain=oldDefaults.Normalize();
Check(migratedAgain.SchemaVersion==6&&migratedAgain.Appearance==oldDefaults.Appearance&&migratedAgain.ImportantKeywords.SequenceEqual(oldDefaults.ImportantKeywords),"migration idempotence");
var custom=(new RuleSettings{SchemaVersion=2,Appearance=new AppearanceOptions{Width=680,Height=380,SenderSize=30,MessageSize=24}}).Normalize();
Check(custom.Appearance.Width==560&&custom.Appearance.Height==380&&custom.Appearance.SenderSize==30&&custom.Appearance.MessageSize==24,"custom layout preserved");
Check((new RuleSettings{SchemaVersion=4,Appearance=new AppearanceOptions{Width=720,Height=420}}).Normalize().Appearance.Width==560,"current explicit dimensions preserved");
foreach(double scale in new[]{1d,1.25,1.5,2}){
 var fit=NotificationLayout.Fit(new(),1920/scale,1080/scale);
 Check(fit.Width==480&&fit.Height==420&&fit.Width*scale<=1920&&fit.Height*scale<=1080,"DPI dimensions stay in work area");
 foreach(string sender in new[]{"甲",new string('长',200)})foreach(string message in new[]{"短消息",new string('文',4000)}){
  Check(NotificationLayout.Fit(new(),1920/scale,1080/scale)==fit,"sender/message length cannot resize host");
 }
}
Check(NotificationLayout.ContentHeight(244,48,0)==196,"measured viewport padding reserved");
Check(NotificationLayout.ContentHeight(244,48,32)==164,"native title space reserved");
Check(NotificationLayout.ContentHeight(24,48,0)==0,"small viewport nonnegative");
Check(NotificationLayout.StackHeader(280)&&!NotificationLayout.StackHeader(328)&&!NotificationLayout.StackHeader(448),"narrow header stacks");
Check(NotificationLayout.CompactHeight(250,96,80)==180,"short message removes blank space");
Check(NotificationLayout.CompactHeight(250,96,400)==250,"long content respects height cap");
Check(NotificationLayout.CompactHeight(160,96,400)==160,"small work area respects cap");
var priorDefault=(new RuleSettings{SchemaVersion=3,Appearance=new AppearanceOptions{Width=600,Height=340,Mask=true}}).Normalize();
Check(priorDefault.Appearance.Width==480&&priorDefault.Appearance.Height==420&&!priorDefault.Appearance.Mask,"v3 defaults migrate to compact nonmodal notice");
Check((new RuleSettings{SchemaVersion=3,Appearance=new AppearanceOptions{Width=650,Height=400,Mask=true}}).Normalize().Appearance.Mask,"custom mask preference preserved");
var attachmentFolder=Path.Combine(Path.GetTempPath(),"RN-AttachmentChecks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(attachmentFolder);
try {
 File.WriteAllText(Path.Combine(attachmentFolder,"photo.png"),"test fixture");var options=new ReceptionOptions{PreviewImages=true,CacheDirectory=attachmentFolder};
 Check(NotificationAttachmentResolver.Exact("[图片]",options) is null,"never infer latest picture");
 Check(NotificationAttachmentResolver.Exact("photo.png",options)?.IsImage==true,"explicit unique name");
 File.WriteAllText(Path.Combine(attachmentFolder,"payload.exe"),"test fixture");Check(NotificationAttachmentResolver.Exact("payload.exe",options) is null,"reject executables");
 Check(!NotificationAttachmentResolver.Allowed("script.cmd")&&NotificationAttachmentResolver.Allowed("lesson.pdf"),"attachment allowlist");
} finally {foreach(var f in Directory.GetFiles(attachmentFolder))File.Delete(f);Directory.Delete(attachmentFolder);}
int releases=0,cleanupErrors=0;var lifetime=new NotificationLifetime(_=>cleanupErrors++);bool cancellationObserved=false;
using var registration=lifetime.Token.Register(()=>cancellationObserved=true);
lifetime.Register(()=>releases++);lifetime.Register(()=>throw new InvalidOperationException("fake resource release failure"));lifetime.Register(()=>releases++);
lifetime.Dispose();lifetime.Dispose();Check(cancellationObserved&&lifetime.IsStopped&&releases==2&&cleanupErrors==1,"session cancellation and resilient cleanup once");
lifetime.Register(()=>releases++);Check(releases==3,"resource added after cancellation released immediately");
Check(lifetime.Token.IsCancellationRequested,"closed session cannot restart work");
Check(!new AudioOptions().OnlineSpeechEnabled&&new AudioOptions().Engine==SpeechEngine.Windows,"online speech opt in only");
Check(new ReceptionOptions().EventDriven,"new system reception defaults to events");
Check(new AppearanceOptions().ButtonSize==14,"native default button type size");
Check(RimeNotice.Core.GitHubRelease.ParseVersion("v1.0.0.15")==new Version(1,0,0,15),"four part test release tag");
Check(RimeNotice.Core.GitHubRelease.ParseVersion("v1.2.3")==new Version(1,2,3,0),"three part release tag");
foreach(string entry in new[]{"../evil.exe","a/../../evil", "C:/evil", "a\\evil", "/evil"}){
 bool rejected=false;try{RimeNotice.Core.GitHubRelease.SafeEntryPath(Path.GetTempPath(),entry);}catch(InvalidDataException){rejected=true;}Check(rejected,"reject update archive traversal");}
var releaseJson=System.Text.Json.JsonSerializer.Serialize(new{draft=false,prerelease=false,tag_name="v1.0.0.15",body="notes",published_at=DateTimeOffset.UtcNow,assets=new[]{new{name="RimeNotice-QQ-Test-x64-1.0.0.15.zip",browser_download_url="https://github.com/WRMYX/RimeNotice/releases/download/v1.0.0.15/test.zip",digest="sha256:"+new string('a',64),size=100L}}});
Check(RimeNotice.Core.GitHubRelease.Parse(releaseJson,RimeNotice.Core.GitHubUpdateChannel.Msix,"x64").Asset is not null,"MSIX test package asset contract");
Check(RimeNotice.Core.GitHubRelease.Parse(releaseJson,RimeNotice.Core.GitHubUpdateChannel.Msix,"arm64").Asset is null,"wrong architecture not offered");
bool unsafeDownload=false;try{RimeNotice.Core.GitHubDownloadSource.Resolve("https://example.com/evil.zip","direct");}catch(ArgumentException){unsafeDownload=true;}Check(unsafeDownload,"foreign release downloads rejected");
var wav=SpeechAudioFormat.Wave(new byte[]{0,0,1,0},22050);Check(wav.Length==48&&System.Text.Encoding.ASCII.GetString(wav,0,4)=="RIFF"&&BitConverter.ToInt32(wav,24)==22050,"PCM WAV header and sample rate");
bool oddPcm=false;try{SpeechAudioFormat.Wave(new byte[3],22050);}catch(InvalidDataException){oddPcm=true;}Check(oddPcm,"reject partial PCM sample");
using(var signal=new CoalescedSignal()){for(int i=0;i<1000;i++)signal.Signal();Check(await signal.WaitAsync(TimeSpan.Zero,CancellationToken.None),"burst wakes reader");Check(!await signal.WaitAsync(TimeSpan.Zero,CancellationToken.None),"burst coalesces to one read");using var stop=new CancellationTokenSource();stop.Cancel();bool cancelled=false;try{await signal.WaitAsync(TimeSpan.FromSeconds(30),stop.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"event wait cancels promptly");}
Check((new AudioOptions{Engine=SpeechEngine.CosyVoice}).Normalize().Engine==SpeechEngine.Windows,"removed service selection safely migrates to local speech");
Console.WriteLine($"PASS: {checks} offline rule/settings checks. No QQ, UI or registry access.");
} catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex); Environment.ExitCode = 1; }
sealed class ManualClock : TimeProvider {
    private long _timestamp; public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => _timestamp; public void Advance(int seconds) => _timestamp += seconds * 1000L;
}
