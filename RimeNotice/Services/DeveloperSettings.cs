using System;
using System.IO;
using System.Text.Json;

namespace RimeNotice.Services;

public static class DeveloperSettings
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimeNotice", "ui.json");
    public static bool Enabled { get; private set; } = Load();
    private static bool Load()
    {
        try { return File.Exists(FilePath) && JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath))?.DeveloperMode == true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }
    public static void SetEnabled(bool enabled)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new State(enabled)));
        File.Move(temp, FilePath, true);
        Enabled = enabled;
    }
    private sealed record State(bool DeveloperMode);
}
