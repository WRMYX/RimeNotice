using System;
using System.Linq;
namespace RimeNotice.Models;

public sealed record RuleSettings
{
    public string[] ImportantContacts { get; init; } = [];
    public string[] ImportantKeywords { get; init; } = [];
    public bool MentionImportant { get; init; } = true;
    public string[] Blacklist { get; init; } = [];
    public string[] Whitelist { get; init; } = [];
    public int CooldownSeconds { get; init; } = 3;
    public bool CallingEnabled { get; init; } = true;
    public string[] CallingKeywords { get; init; } = ["呼叫"];
    public int NormalSeconds { get; init; } = 10;
    public int ImportantSeconds { get; init; } = 15;
    public int CallingSeconds { get; init; } = 60;
    public int SchemaVersion { get; init; } = 0;
    public AppearanceOptions Appearance { get; init; } = new();
    public AudioOptions Audio { get; init; } = new();
    public ReceptionOptions Reception { get; init; } = new();
    public CallingOptions Calling { get; init; } = new();
    public int Theme { get; init; }
    public bool SettingsAlwaysOnTop { get; init; } = true;
    public UpdateOptions Updates { get; init; } = new();
    public static string[] Terms(string text) => Clean(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    private static string[] Clean(string[]? terms) => (terms ?? []).Where(t => !string.IsNullOrWhiteSpace(t))
        .Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(512).ToArray();
    public RuleSettings Normalize() => this with
    {
        ImportantContacts = Clean(ImportantContacts), ImportantKeywords = Clean(ImportantKeywords),
        Blacklist = Clean(Blacklist), Whitelist = Clean(Whitelist), CallingKeywords = Clean(CallingKeywords),
        CooldownSeconds = Math.Clamp(CooldownSeconds, 0, 60), NormalSeconds = Math.Clamp(NormalSeconds, 1, 120),
        ImportantSeconds = Math.Clamp(ImportantSeconds, 1, 120), CallingSeconds = Math.Clamp(CallingSeconds, 1, 3600),
        Theme = Math.Clamp(Theme, 0, 2), SchemaVersion = 6,
        Appearance = MigrateAppearance(), Audio = (Audio ?? new()).Normalize(),
        Reception = (Reception ?? new()).Normalize(), Calling = (Calling ?? new()).Normalize(), Updates = (Updates ?? new()).Normalize()
    };
    private AppearanceOptions MigrateAppearance()
    {
        var original = Appearance ?? new();
        var appearance = original.Normalize();
        bool oldDefault = SchemaVersion < 3 && original.Width == 720 && original.Height == 420;
        oldDefault |= SchemaVersion < 4 && original.Width == 600 && original.Height == 340;
        if (oldDefault)
        {
            appearance = appearance with { Width = 480, Height = 250,
                SenderSize = appearance.SenderSize == 36 ? 28 : appearance.SenderSize,
                MessageSize = appearance.MessageSize == 22 ? 20 : appearance.MessageSize };
        }
        if (oldDefault) appearance = appearance with { Mask = false };
        if(SchemaVersion<5&&appearance.Width==480&&appearance.Height==250&&appearance.SenderSize==28&&appearance.MessageSize==20&&appearance.ButtonSize==18) appearance=appearance with{ButtonSize=14};
        if (SchemaVersion < 6 && appearance.Width == 480 && appearance.Height == 250) appearance = appearance with { Height = 420 };
        return appearance;
    }
    public int Duration(NoticePreviewKind kind) => kind switch
    { NoticePreviewKind.Calling => CallingSeconds, NoticePreviewKind.Important => ImportantSeconds, _ => NormalSeconds };
}
