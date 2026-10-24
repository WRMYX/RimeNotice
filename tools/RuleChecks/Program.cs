using RimeNotice.Models;
using RimeNotice.Services;
int checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
try {
Check(NoticeColor.Normalize("#123abc")=="#FF123ABC","RGB color serialized with opaque alpha");
Check(NoticeColor.Normalize("#00123456")=="#FF123456","transparent custom color cannot make status unreadable");
Check(NoticeColor.Normalize("1234567")==""&&NoticeColor.Normalize("#nothex")=="","malformed color reverts to theme");
var visualOptions=new AppearanceOptions{CommandTint=double.NaN,NormalColor="invalid",MaterialMotion=new(){Entrance=(MaterialEntrance)99,Exit=(MaterialExit)99,EntranceMilliseconds=0,ExitMilliseconds=99999}}.Normalize();
Check(visualOptions.CommandTint==.42&&visualOptions.NormalColor=="","corrupt appearance values use safe defaults");
Check(visualOptions.MaterialMotion is {Entrance:MaterialEntrance.Fade,Exit:MaterialExit.Fade,EntranceMilliseconds:80,ExitMilliseconds:1500},"invalid animation parameters normalize safely");
var customizationRoot=Path.Combine(Path.GetTempPath(),"RN-Custom-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(customizationRoot);
try{
 string path=Path.Combine(customizationRoot,"settings.json");var store=new SettingsStore(path);
 store.Save(store.Current with{Appearance=store.Current.Appearance with{ShowQQLogo=false,ShowDeveloperLogo=false,ImportantColor="#ffaa22",CommandTint=.6,MaterialMotion=new(){Entrance=MaterialEntrance.Left,Exit=MaterialExit.Right}}});
 var options=new SettingsStore(path).Current.Appearance;
 foreach(string voiceId in new[]{"legacy-winrt-voice-id",@"sapi:HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech\Voices\Tokens\Example"}){
  store.Save(store.Current with{Audio=store.Current.Audio with{VoiceId=voiceId}});Check(new SettingsStore(path).Current.Audio.VoiceId==voiceId,"voice identity persists without rewriting its source");
 }
 Check(!options.ShowQQLogo&&!options.ShowDeveloperLogo&&options.ImportantColor=="#FFFFAA22"&&options.CommandTint==.6,"logo, status color and material tint persist");
 Check(options.MaterialMotion is {Entrance:MaterialEntrance.Left,Exit:MaterialExit.Right},"independent entrance and exit persist");
 File.WriteAllText(path,"{\"RuleDefaultsVersion\":1,\"Appearance\":{\"Mask\":false,\"MaterialMotion\":null}}");
 var legacy=new SettingsStore(path).Current.Appearance;
 Check(legacy.ShowQQLogo&&legacy.ShowDeveloperLogo&&!legacy.Mask&&legacy.MaterialMotion is not null,"legacy settings preserve mask and initialize customization defaults");
}finally{Directory.Delete(customizationRoot,true);}
Check(new AppearanceOptions().NativeHost==NativeNotificationHost.Compact,"fullscreen host experiment defaults off");
Check((new AppearanceOptions{NativeHost=(NativeNotificationHost)72}).Normalize().NativeHost==NativeNotificationHost.Compact,"unknown native host safely uses compact");
var compactHost=new AppearanceOptions{Presentation=NotificationPresentation.Native,NativeHost=NativeNotificationHost.Compact,Mask=true};
var fullHost=compactHost with{NativeHost=NativeNotificationHost.Fullscreen};
Check(NotificationPresentationSession.EffectiveHost(fullHost)==NativeNotificationHost.Fullscreen,"fullscreen host requires native mode and mask");
Check(NotificationPresentationSession.EffectiveHost(fullHost with{Mask=false})==NativeNotificationHost.Compact,"no invisible fullscreen input blocker without mask");
Check(NotificationPresentationSession.EffectiveHost(fullHost with{Presentation=NotificationPresentation.Material})==NativeNotificationHost.Compact,"material mode ignores native host experiment");
var hostQueue=new OrderedNoticeQueue<string>();hostQueue.Enqueue("1","第一条");hostQueue.Enqueue("2","第二条");int hostDequeues=0;
string? NextHost(){hostDequeues++;return hostQueue.TryDequeue(out var item)?item:null;}
Check(NotificationPresentationSession.TakeNext(compactHost,fullHost,NextHost) is null&&hostQueue.Count==2&&hostDequeues==0,"host change preserves pending messages before dequeue");
Check(NotificationPresentationSession.TakeNext(fullHost,fullHost,NextHost)=="第一条","new fullscreen session gets original head");
Check(NotificationPresentationSession.TakeNext(fullHost,fullHost with{Mask=false},NextHost) is null&&hostQueue.Count==1,"mask off leaves next message for compact session");
Check(NotificationPresentationSession.TakeNext(fullHost with{Mask=false},fullHost with{Mask=false},NextHost)=="第二条","compact fallback preserves FIFO");
var nativeShort=NotificationLayout.FitNativeViewport(420,264,24,0);
Check(nativeShort.BodyHeight==24&&nativeShort.ScrollHeight==24,"short body uses full measured line height without estimated chrome");
var nativeLong=NotificationLayout.FitNativeViewport(420,264,1800,0);
Check(nativeLong.BodyHeight==156&&nativeLong.ScrollHeight==156,"long body stays within measured template budget");
var nativeImage=NotificationLayout.FitNativeViewport(420,264,300,60);
Check(nativeImage.BodyHeight==104&&nativeImage.ScrollHeight==156,"image and long body share bounded scroll viewport");
Check(NotificationLayout.FitNativeViewport(220,264,300,60)==new NativeContentFit(0,0),"insufficient template budget cannot produce negative viewport");
string hostSettingsRoot=Path.Combine(Path.GetTempPath(),"RN-Host-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(hostSettingsRoot);
try{
 string path=Path.Combine(hostSettingsRoot,"settings.json");
 var store=new SettingsStore(path);store.Save(store.Current with{Appearance=fullHost});
 var loaded=new SettingsStore(path);
 Check(loaded.Current.Appearance.NativeHost==NativeNotificationHost.Fullscreen&&loaded.Current.Appearance.Mask,"host experiment and mask persist together");
 loaded.Save(loaded.Current with{Appearance=loaded.Current.Appearance with{Mask=false}});
 var noMask=new SettingsStore(path);
 Check(noMask.Current.Appearance.NativeHost==NativeNotificationHost.Fullscreen&&NotificationPresentationSession.EffectiveHost(noMask.Current.Appearance)==NativeNotificationHost.Compact,"mask off retains experiment choice but releases fullscreen behaviour");
}finally{Directory.Delete(hostSettingsRoot,true);}
Check(new AppearanceOptions().Presentation==NotificationPresentation.Native,"native presentation defaults for new settings");
Check((new AppearanceOptions{Presentation=(NotificationPresentation)999,NotificationMaterial=WindowMaterial.Acrylic}).Normalize() is {Presentation:NotificationPresentation.Native,NotificationMaterial:WindowMaterial.Acrylic},"invalid presentation falls back without changing chosen material");
var styleQueue=new OrderedNoticeQueue<string>();styleQueue.Enqueue("one","第一条");styleQueue.Enqueue("two","第二条");
int dequeueCalls=0;
string? DequeueStyle(){dequeueCalls++;return styleQueue.TryDequeue(out var next)?next:null;}
Check(NotificationPresentationSession.TakeNext(NotificationPresentation.Native,NotificationPresentation.Material,DequeueStyle) is null&&styleQueue.Count==2&&dequeueCalls==0,"changing presentation leaves every waiting message untouched");
Check(NotificationPresentationSession.TakeNext(NotificationPresentation.Material,NotificationPresentation.Material,DequeueStyle)=="第一条"&&styleQueue.Count==1,"new presentation consumes original first waiting message");
Check(NotificationPresentationSession.TakeNext(NotificationPresentation.Material,NotificationPresentation.Native,DequeueStyle) is null&&styleQueue.Count==1,"switch back does not prematurely dequeue");
Check(NotificationPresentationSession.TakeNext(NotificationPresentation.Native,NotificationPresentation.Native,DequeueStyle)=="第二条"&&styleQueue.Count==0,"switch back preserves FIFO and consumes once");
var presentationRoot=Path.Combine(Path.GetTempPath(),"RN-Presentation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(presentationRoot);
try{
 string path=Path.Combine(presentationRoot,"settings.json");
 File.WriteAllText(path,"{\"RuleDefaultsVersion\":1,\"Appearance\":{\"NotificationMaterial\":2},\"ImportantKeywords\":[\"保留关键词\"]}");
 var store=new SettingsStore(path);
 Check(store.Current.Appearance.Presentation==NotificationPresentation.Native&&store.Current.Appearance.NotificationMaterial==WindowMaterial.Acrylic,"old file missing presentation loads native and preserves material");
 store.Save(store.Current with{Appearance=store.Current.Appearance with{Presentation=NotificationPresentation.Material}});
 var reloaded=new SettingsStore(path);
 Check(reloaded.Current.Appearance.Presentation==NotificationPresentation.Material&&reloaded.Current.ImportantKeywords.SequenceEqual(new[]{"保留关键词"}),"custom presentation persists without resetting rules");
 reloaded.Save(reloaded.Current with{Appearance=reloaded.Current.Appearance with{Presentation=NotificationPresentation.Native}});
 var nativeReloaded=new SettingsStore(path);
 Check(nativeReloaded.Current.Appearance.Presentation==NotificationPresentation.Native&&nativeReloaded.Current.Appearance.NotificationMaterial==WindowMaterial.Acrylic,"native switch persists while retaining saved custom material");
}finally{Directory.Delete(presentationRoot,true);}
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
var fifo=new OrderedNoticeQueue<string>(3);
Check(fifo.Enqueue("id1/body1","第一条")&&fifo.Enqueue("id1/body2","第二条"),"same toast preserves separate bodies");
Check(!fifo.Enqueue("id1/body1","覆盖内容")&&fifo.Preview(2).SequenceEqual(new[]{"第一条","第二条"}),"pending duplicate cannot overwrite");
Check(fifo.Enqueue("id2/body3","第三条")&&!fifo.Enqueue("id3/body4","第四条"),"overflow explicit without overwriting accepted messages");
foreach(string expectedMessage in new[]{"第一条","第二条","第三条"})Check(fifo.TryDequeue(out var actualMessage)&&actualMessage==expectedMessage,"FIFO arrival order");
Check(fifo.Count==0&&!fifo.TryDequeue(out _),"queue drains fully");
var defaultRules=RuleSettings.ForNewUser();
Check(defaultRules.ImportantContacts.SequenceEqual(new[]{"老师"})&&defaultRules.ImportantKeywords.SequenceEqual(new[]{"作业","通知","课代表"}),"new user teaching rules");
Check(engine.Evaluate("张老师","你好",defaultRules with{CooldownSeconds=0}).Kind==NoticePreviewKind.Important,"teacher substring default");
var emptyRules=System.Text.Json.JsonSerializer.Deserialize<RuleSettings>("{\"ImportantContacts\":[],\"ImportantKeywords\":[]}")!.Normalize();
Check(emptyRules.ImportantContacts.Length==0&&emptyRules.ImportantKeywords.Length==0,"explicit empty user rules preserved");
string discoveryRoot=Path.Combine(Path.GetTempPath(),"RN-Discovery-"+Guid.NewGuid().ToString("N"));
try{
 string expectedDirectory=Path.Combine(discoveryRoot,"123456","nt_qq","nt_data","Pic",DateTime.Now.ToString("yyyy-MM"),"Thumb");Directory.CreateDirectory(expectedDirectory);
 Check(QQDirectoryDiscovery.DiscoverInRoots("",new[]{discoveryRoot}).Single().Directory==Path.GetDirectoryName(Path.GetDirectoryName(expectedDirectory)),"known root account and month discovery");
 Check(QQDirectoryDiscovery.DiscoverInRoots("999999",new[]{discoveryRoot}).Count==0,"wrong account not guessed");
 Directory.CreateDirectory(Path.Combine(discoveryRoot,"654321","nt_qq","nt_data","Pic"));
 Check(QQDirectoryDiscovery.DiscoverInRoots("",new[]{discoveryRoot}).Count==2,"multiple candidates require selection");
 Check(QQDirectoryDiscovery.DiscoverInRoots("123456",new[]{discoveryRoot}).Single().Account=="123456","account selects exact directory");
}finally{if(Directory.Exists(discoveryRoot))Directory.Delete(discoveryRoot,true);}
string migrationRoot=Path.Combine(Path.GetTempPath(),"RN-Defaults-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(migrationRoot);
try{
 string file=Path.Combine(migrationRoot,"settings.json");File.WriteAllText(file,"{\"ImportantContacts\":[],\"ImportantKeywords\":[],\"NormalSeconds\":27}");
 var store=new SettingsStore(file);Check(store.Current.RuleDefaultsVersion==1&&store.Current.ImportantContacts.Contains("老师")&&store.Current.NormalSeconds==27,"store migration fills defaults preserving unrelated settings");
 store.Save(store.Current with{ImportantContacts=[],ImportantKeywords=[]});var again=new SettingsStore(file);
 Check(again.Current.ImportantContacts.Length==0&&again.Current.ImportantKeywords.Length==0,"saved intentional clearing persists through real store load");
}finally{Directory.Delete(migrationRoot,true);}
var migratedRules=(new RuleSettings{ImportantContacts=[],ImportantKeywords=[]}).MigrateRuleDefaults();
Check(migratedRules.ImportantContacts.SequenceEqual(new[]{"老师"})&&migratedRules.ImportantKeywords.SequenceEqual(new[]{"作业","通知","课代表"}),"legacy empty rules get visible editable defaults once");
var intentionallyCleared=(migratedRules with{ImportantContacts=[],ImportantKeywords=[]}).MigrateRuleDefaults();
Check(intentionallyCleared.ImportantContacts.Length==0&&intentionallyCleared.ImportantKeywords.Length==0,"clearing after migration survives restart");
Check((new RuleSettings{ImportantContacts=["自定义"],ImportantKeywords=["考试"]}).MigrateRuleDefaults().ImportantKeywords.SequenceEqual(new[]{"考试"}),"migration preserves custom terms");
foreach(var mode in Enum.GetValues<AudioMixMode>()){
 var options=new AudioOptions{MixMode=mode,DuckingGain=.3};var gains=AudioMixPolicy.Gains(options,true);
 Check(gains.Tone==(mode==AudioMixMode.SpeechPriority?.3:1)&&gains.Speech==(mode==AudioMixMode.ReminderPriority?.3:1),"mix mode priority gains");
 Check(AudioMixPolicy.Gains(options,false)==(1d,1d),"one stream never ducked");
}
Check(AudioMixPolicy.Text("张老师","作业",new())=="作业","speech defaults body only");
Check(AudioMixPolicy.Text("张老师","作业",new(){ReadSender=true}).Contains("张老师。\n作业"),"speech can include sender");
Check(AudioMixPolicy.Text("张老师","作业",new(){ReadSender=true,ReadMessage=false})=="张老师","speech sender only");
Check(AudioMixPolicy.Text("张老师","作业",new(){ReadSender=false,ReadMessage=false})=="","speech all content disabled");
string nested=Path.Combine(Path.GetTempPath(),"RN-Pic-"+Guid.NewGuid().ToString("N"));
try{
 string pic=Path.Combine(nested,"Pic"),ori=Path.Combine(pic,"2026-10","Ori"),thumb=Path.Combine(pic,"2026-10","Thumb");Directory.CreateDirectory(ori);Directory.CreateDirectory(thumb);
 File.WriteAllText(Path.Combine(ori,"unique.png"),"fixture");
 var opts=new ReceptionOptions{PreviewImages=true,CacheDirectory=thumb};
 Check(NotificationAttachmentResolver.Exact("unique.png",opts)?.Path==Path.Combine(ori,"unique.png"),"old Thumb setting finds explicit Ori file");
 Check(NotificationAttachmentResolver.Exact("[图片]",opts) is null,"Pic subtree never guessed for bare image notice");
 File.WriteAllText(Path.Combine(thumb,"unique.png"),"different fixture");
 Check(NotificationAttachmentResolver.Exact("unique.png",opts) is null,"ambiguous Ori and Thumb names rejected");
}finally{if(Directory.Exists(nested))Directory.Delete(nested,true);}
string recentRoot=Path.Combine(Path.GetTempPath(),"RN-Recent-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(recentRoot);
try{
 var now=DateTimeOffset.UtcNow;string image=Path.Combine(recentRoot,"first.png");File.WriteAllBytes(image,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aK3cAAAAASUVORK5CYII="));File.SetLastWriteTimeUtc(image,now.UtcDateTime);
 var options=new ReceptionOptions{PreviewImages=true,InferRecentImages=true,CacheDirectory=recentRoot};
 Check(NotificationAttachmentResolver.Recent(options,now)?.Inferred==true,"time match is explicitly inferred");
 Check(NotificationAttachmentResolver.Recent(options with{InferRecentImages=false},now) is null,"time inference is opt in");
 Check(NotificationAttachmentResolver.Recent(options,now.AddMinutes(1)) is null,"old files never reused for later message");
 string other=Path.Combine(recentRoot,"second.png");File.WriteAllText(other,"second fixture");File.SetLastWriteTimeUtc(other,now.UtcDateTime);
 Check(NotificationAttachmentResolver.Recent(options,now) is null,"similar timestamp candidates rejected");
 File.SetLastWriteTimeUtc(other,now.UtcDateTime.AddMinutes(-2));File.SetCreationTimeUtc(other,now.UtcDateTime.AddMinutes(-2));
 var resolved=NotificationAttachmentResolver.ResolveAsync("[图片]",options,CancellationToken.None,now).GetAwaiter().GetResult();
 Check(resolved?.Path==image&&resolved.Inferred,"stable new file can be inferred");
 Check(NotificationAttachmentResolver.Recent(options,now) is null,"inferred image not assigned twice");
 Check(NotificationAttachmentResolver.Exact("first.png",options)?.Inferred==false,"explicit identity remains preferred");
}finally{Directory.Delete(recentRoot,true);}
Check(NotificationAttachmentResolver.IsImageMessage("[Image]")&&NotificationAttachmentResolver.IsImageMessage("发送了一张图片")&&NotificationAttachmentResolver.IsImageMessage("照片"),"recognize QQ image variants");
Check(!NotificationAttachmentResolver.IsImageMessage("作业的图片在文件夹里"),"ordinary text does not trigger image guessing");
string watchRoot=Path.Combine(Path.GetTempPath(),"RN-Watch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(watchRoot);
try{
 var now=DateTimeOffset.UtcNow;var options=new ReceptionOptions{PreviewImages=true,InferRecentImages=true,CacheDirectory=watchRoot};
 using var index=new QQImageIndex();index.SnapshotAsync(options,CancellationToken.None).GetAwaiter().GetResult();
 string path=Path.Combine(watchRoot,"late.png");File.WriteAllBytes(path,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aK3cAAAAASUVORK5CYII="));File.SetLastWriteTimeUtc(path,now.AddYears(-1).UtcDateTime);
 ImageFileEntry[] entries=[];var deadline=DateTime.UtcNow.AddSeconds(4);
 do{Thread.Sleep(100);entries=index.SnapshotAsync(options,CancellationToken.None).GetAwaiter().GetResult();}while((entries.Length==0||entries[0].ObservedAt is null)&&DateTime.UtcNow<deadline);
 Check(entries.Length==1&&entries[0].ObservedAt is not null,"watcher observes files arriving after startup with old write time");
 Check(NotificationAttachmentResolver.SelectRecent(entries,options,now)?.Path==path,"event time associates old-timestamp new download");
 Check(NotificationAttachmentResolver.ImageHeaderValid(path),"real PNG header supported");
 string broken=Path.Combine(watchRoot,"broken.jpg");File.WriteAllText(broken,"not a picture");Check(!NotificationAttachmentResolver.ImageHeaderValid(broken),"invalid image data rejected");
 var delayed=entries.Select(e=>e with{ObservedAt=now.AddSeconds(12)}).ToArray();Check(NotificationAttachmentResolver.SelectRecent(delayed,options,now) is not null,"delayed cache within enlarged window");
 var stale=entries.Select(e=>e with{ObservedAt=now.AddMinutes(-1),WrittenUtc=now.AddMinutes(-1).UtcDateTime,CreatedUtc=now.AddMinutes(-1).UtcDateTime}).ToArray();Check(NotificationAttachmentResolver.SelectRecent(stale,options,now) is null,"stale cache rejected");
 using var canceled=new CancellationTokenSource();canceled.Cancel();var result=NotificationAttachmentResolver.ResolveAsync("[图片]",options,canceled.Token,now).GetAwaiter().GetResult();Check(result is null,"cancelled attachment request releases cleanly");
 string replacement=Path.Combine(watchRoot,"Other");Directory.CreateDirectory(replacement);
 var switched=index.SnapshotAsync(options with{CacheDirectory=replacement},CancellationToken.None).GetAwaiter().GetResult();Check(switched.Length==0,"switching directories discards previous index");
 index.Dispose();Check(index.Describe().Contains("监听已停止"),"disposing index stops watcher");
 string pic=Path.Combine(watchRoot,"Pic"),ori=Path.Combine(pic,"2026-10","Ori"),thumb=Path.Combine(pic,"2026-10","Thumb");Directory.CreateDirectory(ori);Directory.CreateDirectory(thumb);
 string hash="0123456789abcdef0123456789abcdef.png";string original=Path.Combine(ori,hash),thumbnail=Path.Combine(thumb,hash);File.Copy(path,original);File.Copy(path,thumbnail);
 var twins=new[]{original,thumbnail}.Select(p=>new ImageFileEntry(p,100,now.UtcDateTime,now.UtcDateTime,now)).ToArray();
 Check(NotificationAttachmentResolver.SelectRecent(twins,options with{CacheDirectory=pic},now)?.Path==original,"same QQ cache hash coalesces Ori and Thumb and prefers Ori");
}finally{Directory.Delete(watchRoot,true);}
// Physical window bounds and top-left content layout must share a screen center.

Check((int)SpeechEngine.Windows==0&&(int)SpeechEngine.Edge==1&&(int)SpeechEngine.CosyVoice==2,"legacy engine values stable");
foreach(int retired in new[]{3,4,5}){
 var legacy=System.Text.Json.JsonSerializer.Deserialize<AudioOptions>("{\"Engine\":"+retired+",\"VoiceId\":\"sapi:kept\",\"SpeechVolume\":0.65,\"Offline\":{\"Speaker\":58}}")!.Normalize();
 Check(legacy.Engine==SpeechEngine.Windows&&legacy.VoiceId=="sapi:kept"&&legacy.SpeechVolume==.65,"retired engine falls back without losing local voice settings");
}

var freshDefaults=RuleSettings.ForNewUser().Normalize();
Check(freshDefaults.Appearance.Mask&&freshDefaults.Appearance.TouchFriendlyMenus,"chosen new defaults for mask and menus");
Check(freshDefaults.Reception.PreviewImages&&freshDefaults.Reception.InferRecentImages&&freshDefaults.Audio.MixMode==AudioMixMode.Parallel,"chosen image and audio defaults");
Check(!freshDefaults.Calling.Breathing&&freshDefaults.Calling.BeatsPerMinute==30,"calling visual opt-in defaults");
Check(!(new RuleSettings{Calling=new(){Breathing=true}}).Normalize().Calling.Breathing,"inert legacy calling field must not activate at upgrade");
var opted=(new RuleSettings{CallingEffectsVersion=1,Calling=new(){Breathing=true,BeatsPerMinute=90}}).Normalize();
Check(opted.Calling.Breathing&&opted.Normalize().Calling.Breathing,"current calling user preference persists");
var untouched=(new RuleSettings{SchemaVersion=6,RuleDefaultsVersion=1,Appearance=new(){Mask=false,TouchFriendlyMenus=false},Reception=new(){PreviewImages=false,InferRecentImages=false},Audio=new(){MixMode=AudioMixMode.ReminderPriority}}).Normalize();
Check(!untouched.Appearance.Mask&&!untouched.Reception.PreviewImages&&untouched.Audio.MixMode==AudioMixMode.ReminderPriority,"upgrade does not adopt fresh defaults");
foreach(bool fresh in new[]{false,true})foreach(bool enabled in new[]{false,true})foreach(bool blocked in new[]{false,true})foreach(bool existing in new[]{false,true})
 Check(StartupInitializationPolicy.ShouldEnable(fresh,enabled,blocked,existing)==(fresh&&!enabled&&!blocked&&!existing),"startup fresh and disabled guards");
var state=new AutoScrollState();state.Reset(0);double offset=0;
for(int i=1;i<50;i++)offset=state.Advance(i*.1,offset,100,24,true);
Check(offset>0&&offset<100,"automatic viewport advances at configured rate");
state.User(5,3,true);Check(state.Advance(20,45,100,24,true)==45,"held pointer never fights auto scroll");
state.User(20,3,false);Check(state.Advance(22,60,100,24,true)==60,"manual idle wait retains actual offset");
state.Advance(23.1,60,100,24,true);Check(state.Advance(24.2,60,100,24,true)>60,"resume from user's actual location");
state.Reset(0);Check(state.Advance(10,30,100,24,false)==30,"disabled auto scroll does not move content");
state.Advance(10.1,100,100,24,false);Check(state.FirstPassCompleted,"manual scroll to end completes reading pass");
state.Reset(0);state.Advance(.1,0,0,24,true);Check(state.FirstPassCompleted,"short content completes immediately");
state.Advance(.2,0,300,24,true);Check(!state.FirstPassCompleted,"late image growth restarts first reading pass");
var normalizedImages=new ImageDisplayOptions{Placement=(ImagePlacement)44,Side=(ImageSide)77,ScrollSpeed=double.NaN,PanelWidth=99999,ResumeSeconds=-5}.Normalize();
Check(normalizedImages is{Placement:ImagePlacement.Inline,Side:ImageSide.Right,ScrollSpeed:24,PanelWidth:800,ResumeSeconds:1},"image display range normalization");
foreach(double dpi in new[]{1,1.25,1.5,2})foreach(int origin in new[]{-1920,0,1920}){
 var work=new MotionBounds(origin,-600,(int)(1920*dpi),(int)(1080*dpi));var card=new MotionBounds(origin+(int)(700*dpi),-600+(int)(300*dpi),(int)(480*dpi),(int)(400*dpi));
 foreach(var side in new[]{ImageSide.Left,ImageSide.Right}){
  var panel=ImagePreviewLayout.Beside(card,work,dpi,480,side);Check(panel is not null,"side panel fits wide screen");
  var v=panel!.Value;Check(v.X>=work.X&&v.Y>=work.Y&&v.X+v.Width<=work.X+work.Width&&v.Y+v.Height<=work.Y+work.Height,"side panel remains in monitor work area");
  Check(side==ImageSide.Left?v.X+v.Width<card.X:v.X>card.X+card.Width,"selected side honored");
 }
}
Check(ImagePreviewLayout.Beside(new(20,20,480,400),new(0,0,540,600),1,480,ImageSide.Right) is null,"narrow screen falls back inline");
var batchRoot=Path.Combine(Path.GetTempPath(),"RN-Batch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(batchRoot);
try{
 var options=new ReceptionOptions{PreviewImages=true,InferRecentImages=true,CacheDirectory=batchRoot,AttachmentWaitSeconds=0};var now=DateTimeOffset.UtcNow;
 string ori=Path.Combine(batchRoot,"Pic","2026-10","Ori"),thumb=Path.Combine(batchRoot,"Pic","2026-10","Thumb");Directory.CreateDirectory(ori);Directory.CreateDirectory(thumb);
 byte[] png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aK3cAAAAASUVORK5CYII=");
 var entries=new List<ImageFileEntry>();
 for(int i=1;i<=3;i++)foreach(string dir in new[]{thumb,ori}){
  string file=Path.Combine(dir,i.ToString("x32")+(dir==thumb?"_720":"")+".png");File.WriteAllBytes(file,png);
  entries.Add(new(file,png.Length,now.UtcDateTime,now.UtcDateTime,now.AddMilliseconds(i*60)));
 }
 options=options with{CacheDirectory=Path.Combine(batchRoot,"Pic")};
 var images=NotificationAttachmentResolver.BatchCandidates(entries,options,now,false,"[图片]");
 Check(images.Length==3&&images.All(a=>a.Path.Contains("Ori")),"three simultaneous images group Thumb/Ori and retain all originals");
 Check(NotificationAttachmentResolver.ClaimBatch(images,entries),"atomic batch claims all candidates");
 Check(!NotificationAttachmentResolver.ClaimBatch(images,entries)&&NotificationAttachmentResolver.BatchCandidates(entries,options,now,false,"[图片]").Length==0,"two concurrent consumers cannot claim same image batch");
 Check(NotificationAttachmentResolver.BatchCandidates(entries.Select(e=>e with{ObservedAt=now.AddSeconds(1)}),options,now,false,"[图片]").Length==0,"late Thumb/Ori timestamps do not reassign already claimed identity");
 var explicitBatch=NotificationAttachmentResolver.BatchCandidates(entries,options,now,true,string.Join(" ",images.Select(a=>Path.GetFileName(a.Path))));
 Check(explicitBatch.Length==3&&explicitBatch.All(a=>!a.Inferred),"explicit multi-file names stay explicit");
 var coordinator=new ImageBatchCoordinator();using var cancel=new CancellationTokenSource();
 var a=coordinator.Register("source","sender","[图片]","a",now,options,cancel.Token);
 var bb=coordinator.Register("source","sender","[图片]","b",now.AddMilliseconds(10),options,cancel.Token);
 Check(a.Leader&&!bb.Leader&&ReferenceEquals(a.Task,bb.Task),"same conversation burst shares one album task");
 var ordinary=coordinator.Register("source","sender","图片作业说明","text",now,options with{PreviewImages=false},cancel.Token);
 Check(ordinary.Leader&&!ReferenceEquals(a.Task,ordinary.Task),"ordinary text is never swallowed into image batch");
 cancel.Cancel();a.Task.GetAwaiter().GetResult();ordinary.Task.GetAwaiter().GetResult();
 var exactOptions=options with{InferRecentImages=false};
 var complete=NotificationAttachmentResolver.ResolveBatchAsync(string.Join(" ",images.Select(a=>Path.GetFileName(a.Path))),exactOptions,CancellationToken.None,now,["explicit"]).GetAwaiter().GetResult();
 Check(complete.Items.Length==3&&!complete.Inferred,"resolver returns full explicit gallery");
 for(int i=4;i<=6;i++){
  string file=Path.Combine(ori,i.ToString("x32")+".png");File.WriteAllBytes(file,png);
 }
 var inferred=NotificationAttachmentResolver.ResolveBatchAsync("[图片]",options,CancellationToken.None,DateTimeOffset.UtcNow,["one","two","three"]).GetAwaiter().GetResult();
 Check(inferred.Items.Length==3&&inferred.Inferred&&inferred.SourceKeys.Length==3,"end-to-end inferred batch keeps three files and raw notice keys");
 string sameNameOne=Path.Combine(ori,"ambiguous.png"),sameNameTwo=Path.Combine(thumb,"ambiguous.png");File.WriteAllBytes(sameNameOne,png);File.WriteAllBytes(sameNameTwo,png);
 var ambiguous=new[]{sameNameOne,sameNameTwo}.Select(f=>new ImageFileEntry(f,png.Length,now.UtcDateTime,now.UtcDateTime,null));
 Check(NotificationAttachmentResolver.BatchCandidates(ambiguous,options,now,true,"ambiguous.png").Length==0,"unrelated identical basenames cannot become exact associations");
 var missingKeys=NotificationAttachmentResolver.ResolveBatchAsync("[图片]",options with{PreviewImages=false},CancellationToken.None,now,["a","b"]).GetAwaiter().GetResult();
 Check(missingKeys.SourceKeys.Length==2,"unresolved album retains all original keys");

 using var cancelled=new CancellationTokenSource();cancelled.Cancel();
 Check(NotificationAttachmentResolver.ResolveBatchAsync("[图片]",options,cancelled.Token,now,["cancelled"]).GetAwaiter().GetResult().Items.Length==0,"batch cancellation completes safely");
}finally{QQImageIndex.Shared.Configure(new ReceptionOptions());Directory.Delete(batchRoot,true);}

foreach(int old in new[]{2,99}){
 var removed=new MaterialMotionOptions{Entrance=(MaterialEntrance)old,Exit=(MaterialExit)old,EntranceExponent=7,ExitExponent=4}.Normalize();
 Check(removed is{Entrance:MaterialEntrance.Fade,Exit:MaterialExit.Fade,EntranceExponent:7,ExitExponent:4},"removed animation normalizes without resetting easing");
}
foreach(int direction in new[]{0,1,3,4,5,6}){
 var saved=System.Text.Json.JsonSerializer.Deserialize<MaterialMotionOptions>("{\"Entrance\":"+direction+",\"Exit\":"+direction+",\"BounceStrength\":1,\"Expansion\":0.3}")!.Normalize();
 Check((int)saved.Entrance==direction&&(int)saved.Exit==direction,"old directions keep numeric meaning after deleted middle entry");
 Check(!System.Text.Json.JsonSerializer.Serialize(saved).Contains("BounceStrength")&&!System.Text.Json.JsonSerializer.Serialize(saved).Contains("Expansion"),"removed animation parameters no longer persist");
}
Check(new ImageDisplayOptions().BatchGapSeconds==2&&new ImageDisplayOptions{BatchGapSeconds=double.NaN}.Normalize().BatchGapSeconds==2,"batch gap default and finite validation");
Check(new ImageDisplayOptions{BatchGapSeconds=.1}.Normalize().BatchGapSeconds==.5&&new ImageDisplayOptions{BatchGapSeconds=50}.Normalize().BatchGapSeconds==5,"batch gap bounds");
var sessionRoot=Path.Combine(Path.GetTempPath(),"RN-Sessions-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sessionRoot);
try{
 byte[] png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aK3cAAAAASUVORK5CYII=");
 int fileId=100;
 (ReceptionOptions Options,ImageFileEntry[] Files) Fixtures(int count,int seconds=2){
  string pic=Path.Combine(sessionRoot,Guid.NewGuid().ToString("N"),"Pic"),ori=Path.Combine(pic,"2026-10","Ori");Directory.CreateDirectory(ori);
  var files=new List<ImageFileEntry>();for(int i=0;i<count;i++){
   string file=Path.Combine(ori,(++fileId).ToString("x32")+".png");File.WriteAllBytes(file,png);var f=new FileInfo(file);files.Add(new(file,f.Length,f.LastWriteTimeUtc,f.CreationTimeUtc,null));
  }
  return(new ReceptionOptions{PreviewImages=true,InferRecentImages=true,CacheDirectory=pic,AttachmentWaitSeconds=seconds,Images=new(){BatchGapSeconds=.5}},files.ToArray());
 }
 var f=Fixtures(3);var started=DateTimeOffset.UtcNow;var elapsed=System.Diagnostics.Stopwatch.StartNew();var history=new List<NoticeAttachmentBatch>();
 Task<ImageFileEntry[]> Delayed(ReceptionOptions o,CancellationToken t){t.ThrowIfCancellationRequested();return Task.FromResult(f.Files.Select((file,i)=>file with{ObservedAt=started.AddSeconds(i*.35)}).Where((file,i)=>elapsed.Elapsed.TotalSeconds>=i*.35).ToArray());}
 using(var session=new NoticeImageSession("[图片]",f.Options,started,["one-notification"],CancellationToken.None,snapshot:Delayed)){
  session.Updated+=batch=>{lock(history)history.Add(batch);};
  var first=session.FirstBatch.GetAwaiter().GetResult();Check(first.Items.Length>=1&&first.IsWaiting&&!session.Completion.IsCompleted,"single notification publishes first image before collection completes");
  session.Completion.GetAwaiter().GetResult();var final=session.Snapshot;
  Check(final.Items.Length==3&&!final.IsWaiting&&final.SourceKeys.SequenceEqual(new[]{"one-notification"}),"one notification eventually collects three delayed images");
  Check(final.Items.All(i=>i.Inferred)&&final.Inferred,"continued album clearly labelled inferred");
  Check(history.All(h=>h.BatchId==final.BatchId)&&history.Zip(history.Skip(1)).All(pair=>pair.First.Revision<pair.Second.Revision),"incremental snapshots share batch and increase revision");
  Check((final.Items.Last().Path==f.Files[2].Path),"adjacent time chain includes third image beyond first-image gap");
 }
 var twin=Fixtures(2);string thumb=Path.Combine(twin.Options.CacheDirectory,"2026-10","Thumb");Directory.CreateDirectory(thumb);
 string thumbnail=Path.Combine(thumb,Path.GetFileNameWithoutExtension(twin.Files[0].Path)+"_720.png");File.WriteAllBytes(thumbnail,png);var ti=new FileInfo(thumbnail);
 started=DateTimeOffset.UtcNow;elapsed.Restart();
 Task<ImageFileEntry[]> Twins(ReceptionOptions o,CancellationToken t){
  var files=new List<ImageFileEntry>{new(thumbnail,ti.Length,ti.LastWriteTimeUtc,ti.CreationTimeUtc,started)};
  if(elapsed.Elapsed.TotalSeconds>=.65)files.Add(twin.Files[0] with{ObservedAt=started.AddSeconds(.65)});
  if(elapsed.Elapsed.TotalSeconds>=1.2)files.Add(twin.Files[1] with{ObservedAt=started.AddSeconds(1.2)});
  return Task.FromResult(files.ToArray());
 }
 using(var session=new NoticeImageSession("[图片]",twin.Options,started,["thumb-ori"],CancellationToken.None,snapshot:Twins)){
  session.Completion.GetAwaiter().GetResult();
  Check(session.Snapshot.Items.Length==1&&session.Snapshot.Items[0].Path==twin.Files[0].Path,"late Ori upgrades existing image without becoming a new timing bridge");
 }
 var cut=Fixtures(2);started=DateTimeOffset.UtcNow;elapsed.Restart();
 Task<ImageFileEntry[]> CutSnapshot(ReceptionOptions o,CancellationToken t)=>Task.FromResult(cut.Files.Select((file,i)=>file with{ObservedAt=started.AddSeconds(i*.4)}).Where((file,i)=>elapsed.Elapsed.TotalSeconds>=i*.4).ToArray());
 using(var session=new NoticeImageSession("[图片]",cut.Options,started,["older-notification"],CancellationToken.None,snapshot:CutSnapshot)){
  session.CloseAt(started.AddSeconds(.3));session.Completion.GetAwaiter().GetResult();
  Check(session.Snapshot.Items.Length==1&&session.Snapshot.Items[0].Path==cut.Files[0].Path,"new notification boundary prevents old session taking newer image");
 }
 var zero=Fixtures(2,0);started=DateTimeOffset.UtcNow;elapsed.Restart();int scans=0;
 Task<ImageFileEntry[]> Once(ReceptionOptions o,CancellationToken t){Interlocked.Increment(ref scans);return Task.FromResult(zero.Files.Select((file,i)=>file with{ObservedAt=started.AddSeconds(i*.25)}).Where((file,i)=>elapsed.Elapsed.TotalSeconds>=i*.25).ToArray());}
 using(var session=new NoticeImageSession("[图片]",zero.Options,started,["single-pass"],CancellationToken.None,snapshot:Once)){
  session.Completion.GetAwaiter().GetResult();Check(session.Snapshot.Items.Length==1&&scans<10,"zero collection window only stabilizes initial candidates");
 }
 var cancelFixture=Fixtures(2,4);started=DateTimeOffset.UtcNow;elapsed.Restart();
 Task<ImageFileEntry[]> CancelFiles(ReceptionOptions o,CancellationToken t)=>Task.FromResult(cancelFixture.Files.Select((file,i)=>file with{ObservedAt=started.AddSeconds(i)}).Where((file,i)=>elapsed.Elapsed.TotalSeconds>=i).ToArray());
 using(var cancelSession=new NoticeImageSession("[图片]",cancelFixture.Options,started,["cancel"],CancellationToken.None,snapshot:CancelFiles)){
  cancelSession.FirstBatch.GetAwaiter().GetResult();cancelSession.Cancel();cancelSession.Completion.GetAwaiter().GetResult();
  Check(cancelSession.Snapshot.Items.Length==1&&!cancelSession.Snapshot.IsWaiting,"manual close cancels future collection and finalizes state");
  int updates=0;cancelSession.Updated+=_=>Interlocked.Increment(ref updates);cancelSession.Dispose();Thread.Sleep(150);Check(updates==0,"disposed session never delivers late updates");
 }
 var window=Fixtures(2,1);started=DateTimeOffset.UtcNow;elapsed.Restart();
 Task<ImageFileEntry[]> Late(ReceptionOptions o,CancellationToken t)=>Task.FromResult(window.Files.Select((file,i)=>file with{ObservedAt=started.AddSeconds(i*1.25)}).Where((file,i)=>elapsed.Elapsed.TotalSeconds>=i*1.25).ToArray());
 using(var session=new NoticeImageSession("[图片]",window.Options,started,["deadline"],CancellationToken.None,snapshot:Late)){
  session.Completion.GetAwaiter().GetResult();Check(session.Snapshot.Items.Length==1&&elapsed.Elapsed.TotalSeconds<2,"absolute collection deadline does not expand forever");
 }
 var explicitFixture=Fixtures(2,1);started=DateTimeOffset.UtcNow;
 Task<ImageFileEntry[]> All(ReceptionOptions o,CancellationToken t)=>Task.FromResult(explicitFixture.Files);
 using(var session=new NoticeImageSession(Path.GetFileName(explicitFixture.Files[0].Path),explicitFixture.Options with{InferRecentImages=false},started,["explicit"],CancellationToken.None,snapshot:All)){
  session.CloseAt(started.AddSeconds(-1));session.Completion.GetAwaiter().GetResult();Check(session.Snapshot.Items.Length==1&&!session.Snapshot.Inferred,"explicit filename takes priority over time cutoff without broadening to nearby files");
 }
 var denied=Fixtures(1,1);started=DateTimeOffset.UtcNow;
 using(var session=new NoticeImageSession("[图片]",denied.Options,started,["bad-decode"],CancellationToken.None,validate:(_,_)=>Task.FromResult(false),snapshot:(_,_)=>Task.FromResult(denied.Files))){
  session.Completion.GetAwaiter().GetResult();Check(session.Snapshot.Items.Length==0,"undecodable image is not published or claimed");
 }
 var freshOptions=new ReceptionOptions{PreviewImages=false};
 using(var coordinator=new ImageBatchCoordinator()){
  var one=coordinator.Register("QQ","group","[图片]","one",DateTimeOffset.UtcNow,freshOptions,CancellationToken.None);
  one.Session.Completion.GetAwaiter().GetResult();Check(!one.Session.Snapshot.IsWaiting,"inactive preview session terminates without hanging queue");
 }

 var quick=Fixtures(1,2);
 using(var coordinator=new ImageBatchCoordinator()){
  var one=coordinator.Register("QQ","quick-group","[图片]","quick-one",DateTimeOffset.UtcNow,quick.Options,CancellationToken.None);
  one.Session.Dispose();
  var two=coordinator.Register("QQ","quick-group","[图片]","quick-two",DateTimeOffset.UtcNow,quick.Options,CancellationToken.None);
  Check(two.Leader&&!ReferenceEquals(one.Session,two.Session),"quick close never merges next notice into disposed session");
  two.Session.Dispose();Task.WhenAll(one.Session.Completion,two.Session.Completion).GetAwaiter().GetResult();
 }
 var reconcile=Fixtures(0,0);
 using(var index=new QQImageIndex()){
  index.SnapshotAsync(reconcile.Options,CancellationToken.None).GetAwaiter().GetResult();
  var field=typeof(QQImageIndex).GetField("_watcher",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
  ((FileSystemWatcher)field.GetValue(index)!).EnableRaisingEvents=false;
  string file=Path.Combine(reconcile.Options.CacheDirectory,"missed-event.png");File.WriteAllBytes(file,png);
  Check(index.SnapshotAsync(reconcile.Options,CancellationToken.None).GetAwaiter().GetResult().Length==0,"fixture simulates a missed watcher event");
  index.ReconcileAsync(reconcile.Options,CancellationToken.None).GetAwaiter().GetResult();
  Check(index.SnapshotAsync(reconcile.Options,CancellationToken.None).GetAwaiter().GetResult().Any(e=>e.Path==file),"bounded reconciliation recovers missed file event");
 }

 var interrupted=Fixtures(2,2);NoticeImageSession? closing=null;int validations=0;
 closing=new NoticeImageSession("[图片]",interrupted.Options,DateTimeOffset.UtcNow,["close-before-publish"],CancellationToken.None,
  validate:(_,_)=>{if(Interlocked.Increment(ref validations)==2)closing!.Dispose();return Task.FromResult(true);},snapshot:(_,_)=>Task.FromResult(interrupted.Files));
 closing.Completion.GetAwaiter().GetResult();
 Check(closing.Snapshot.Items.Length==0&&!closing.Snapshot.IsWaiting,"close between claim and publication leaves no half-published snapshot");
 Check(NotificationAttachmentResolver.AvailableForSession(NotificationAttachmentResolver.FileIdentity(interrupted.Files[0].Path),"later-session"),"unpublished image reservation released on close");closing.Dispose();
}finally{Directory.Delete(sessionRoot,true);}
Console.WriteLine($"PASS: {checks} offline rule/settings checks. No QQ, UI or registry access.");
} catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex); Environment.ExitCode = 1; }
sealed class ManualClock : TimeProvider {
    private long _timestamp; public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => _timestamp; public void Advance(int seconds) => _timestamp += seconds * 1000L;
}
