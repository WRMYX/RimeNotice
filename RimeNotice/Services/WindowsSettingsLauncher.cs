using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RimeNotice.Services;

public static class WindowsSettingsLauncher
{
    public static async Task OpenAsync(string page)
    {
        var uri = new Uri("ms-settings:" + page);
        try
        {
            if (await global::Windows.System.Launcher.LaunchUriAsync(uri)) return;
        }
        catch (Exception ex) { ServiceDiagnostics.Error("Windows 设置启动", ex); }
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            ServiceDiagnostics.Error("Windows 设置回退", ex);
            throw new InvalidOperationException("无法打开 Windows 设置。请从开始菜单打开设置，再查找通知访问、通知或启动应用。", ex);
        }
    }
}
