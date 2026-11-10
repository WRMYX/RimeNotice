using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RimeNotice.Services;

/// <summary>Bounded startup diagnostics. No arguments, settings or message contents are recorded.</summary>
internal static class StartupDiagnostics
{
    private static readonly object Gate = new();
    private static readonly string Session = Guid.NewGuid().ToString("N");

    [ModuleInitializer]
    internal static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("fatal.domain", e.ExceptionObject as Exception);
        Write("managed.entry");
        Write("environment", detail: $"os={Environment.OSVersion.Version}; runtime={RuntimeInformation.FrameworkDescription}; architecture={RuntimeInformation.ProcessArchitecture}; version={typeof(StartupDiagnostics).Assembly.GetName().Version}");
        try
        {
            string xaml = Path.Combine(AppContext.BaseDirectory, "Microsoft.UI.Xaml.dll");
            Write(File.Exists(xaml) ? "winui.version" : "winui.runtime-managed", detail: File.Exists(xaml) ? System.Diagnostics.FileVersionInfo.GetVersionInfo(xaml).FileVersion : "Windows App SDK runtime");
        }
        catch (Exception ex) { Write("winui.version.unavailable", ex); }
    }

    internal static void Write(string stage, Exception? error = null, string? detail = null)
    {
        // Logging must never prevent startup, including on a read-only/full profile.
        try
        {
            lock (Gate)
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimeNotice");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "startup.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                    File.Move(path, path + ".previous", true);
                string failure = error is null ? "" : $"\nHRESULT=0x{error.HResult:X8}\n{error}";
                File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] pid={Environment.ProcessId} session={Session} {stage} {detail}{failure}\n");
            }
        }
        catch (Exception) { }
    }
}
