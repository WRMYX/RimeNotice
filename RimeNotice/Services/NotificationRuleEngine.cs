using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RimeNotice.Models;
namespace RimeNotice.Services;

public sealed record RuleDecision(bool Accepted, NoticePreviewKind Kind, string Reason);
public sealed class NotificationRuleEngine
{
    private readonly Dictionary<string, long> _recent = new();
    private readonly TimeProvider _clock;
    public int RetainedDigestCount => _recent.Count;
    public NotificationRuleEngine(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;
    private static bool Matches(string text, string[] terms) => terms.Any(t => !string.IsNullOrWhiteSpace(t) && text.Contains(t, StringComparison.OrdinalIgnoreCase));
    public RuleDecision Evaluate(string sender, string message, RuleSettings settings)
    {
        string full = sender + "\n" + message;
        if (Matches(full, settings.Blacklist)) return new(false, NoticePreviewKind.Normal, "黑名单");
        if (settings.Whitelist.Length > 0 && !Matches(full, settings.Whitelist)) return new(false, NoticePreviewKind.Normal, "白名单");
        var kind = settings.CallingEnabled && Matches(full, settings.CallingKeywords) ? NoticePreviewKind.Calling :
            Matches(sender, settings.ImportantContacts) || Matches(full, settings.ImportantKeywords) ||
            settings.MentionImportant && (full.Contains("有人@我", StringComparison.OrdinalIgnoreCase) || full.Contains("有人@你", StringComparison.OrdinalIgnoreCase))
            ? NoticePreviewKind.Important : NoticePreviewKind.Normal;
        if (settings.CooldownSeconds > 0)
        {
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sender + "\0" + message)));
            long now = _clock.GetTimestamp();
            if (_recent.TryGetValue(digest, out long previous) && _clock.GetElapsedTime(previous, now).TotalSeconds < settings.CooldownSeconds)
                return new(false, kind, "重复消息冷却");
            if (_recent.Count >= 512 && !_recent.ContainsKey(digest)) _recent.Remove(_recent.MinBy(p => p.Value).Key);
            _recent[digest] = now;
        }
        return new(true, kind, "通过");
    }
}
