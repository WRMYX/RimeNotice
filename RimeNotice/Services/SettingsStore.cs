using System;
using System.IO;
using System.Text.Json;
using RimeNotice.Models;
namespace RimeNotice.Services;

public sealed class SettingsStore
{
    public static SettingsStore Shared => SharedHolder.Instance;
    private static class SharedHolder
    {
        internal static readonly SettingsStore Instance = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimeNotice", "settings.json"));
    }
    private readonly string _path;
    public RuleSettings Current { get; private set; } = new();
    public string LastError { get; private set; } = "无";
    public event Action? Changed;
    public SettingsStore(string path)
    {
        _path = path;
        try { if (File.Exists(path)) Current = (JsonSerializer.Deserialize<RuleSettings>(File.ReadAllText(path)) ?? throw new JsonException()).Normalize(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { LastError = $"设置读取失败：{ex.GetType().Name} / 0x{ex.HResult:X8}"; }
    }
    public void Save(RuleSettings settings)
    {
        var normalized = settings.Normalize();
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, true);
            Current = normalized; LastError = "无";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { LastError = $"设置保存失败：{ex.GetType().Name} / 0x{ex.HResult:X8}"; throw; }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        Changed?.Invoke();
    }
}
