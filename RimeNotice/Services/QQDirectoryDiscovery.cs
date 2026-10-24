using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace RimeNotice.Services;

public sealed record QQDirectoryCandidate(string Account, string Directory);
public static class QQDirectoryDiscovery
{
    public static IReadOnlyList<QQDirectoryCandidate> Discover(string account, string customRoot = "")
        => DiscoverInRoots(account, new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tencent Files"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Tencent Files"), customRoot });
    public static IReadOnlyList<QQDirectoryCandidate> DiscoverInRoots(string account, IEnumerable<string> roots)
    {
        var result = new List<QQDirectoryCandidate>();
        foreach (string root in roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) continue;
                // Bounded metadata enumeration; no database or credential access.
                foreach (string folder in Directory.EnumerateDirectories(root).Take(128).Prepend(root))
                {
                    string name = Path.GetFileName(folder);
                    if (name.Length < 5 || !name.All(char.IsAsciiDigit) || account.Length > 0 && name != account) continue;
                    string cache = Path.Combine(folder, "nt_qq", "nt_data", "Pic");
                    if (!Directory.Exists(cache)) continue;
                    string path = cache;
                    if (!result.Any(r => r.Directory.Equals(path, StringComparison.OrdinalIgnoreCase))) result.Add(new(name, path));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return result;
    }
}
