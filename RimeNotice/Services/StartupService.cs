using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;
namespace RimeNotice.Services;

public sealed record StartupStatus(bool Enabled, bool Blocked, string Message);
public static class StartupService
{
    public const string TaskId = "RimeNoticeStartup";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "RimeNotice";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool Packaged
    {
        get { try { _ = Package.Current.Id; return true; } catch (Exception ex) when (ex is InvalidOperationException or COMException) { return false; } }
    }
    private static string Command => $"\"{Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径")}\" --startup";
    private static StartupStatus FromTask(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => new(true, false, "已启用，登录 Windows 后在后台接收消息。"),
        StartupTaskState.EnabledByPolicy => new(true, true, "系统策略已强制启用自启动。"),
        StartupTaskState.DisabledByUser => new(false, true, "Windows 已禁用此启动项，请前往系统启动应用设置开启。"),
        StartupTaskState.DisabledByPolicy => new(false, true, "系统策略禁止自启动。"),
        StartupTaskState.Disabled => new(false, false, "登录 Windows 时自动启动 RimeNotice，默认关闭。"),
        _ => new(false, true, "无法识别自启动状态。")
    };
    private static StartupStatus ReadPortable()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        string? current = run?.GetValue(ValueName) as string;
        using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKey);
        bool disabled = approval?.GetValue(ValueName) is byte[] { Length: >= 4 } data && BitConverter.ToUInt32(data, 0) is 3 or 7;
        if (current is not null && disabled) return new(false, true, "Windows 已禁用此启动项，请前往系统启动应用设置开启。");
        bool ours = string.Equals(current, Command, StringComparison.OrdinalIgnoreCase);
        return new(ours, false, ours ? "已为当前便携版启用登录自启动。" : current is null ? "未启用登录自启动。" : "启动项指向其他位置，启用后将使用当前程序。");
    }
    public static async Task<StartupStatus> GetStatusAsync()
    {
        await Gate.WaitAsync();
        try { return Packaged ? FromTask((await StartupTask.GetAsync(TaskId)).State) : ReadPortable(); }
        catch (Exception ex) { QQNotificationListener.Shared.ReportPresentationError(ex); return new(false, true, "无法读取自启动状态，请查看调试诊断。"); }
        finally { Gate.Release(); }
    }
    public static async Task<StartupStatus> SetEnabledAsync(bool enabled)
    {
        await Gate.WaitAsync();
        try
        {
            if (Packaged)
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (enabled) return FromTask(await task.RequestEnableAsync());
                if (task.State == StartupTaskState.EnabledByPolicy) return FromTask(task.State);
                task.Disable(); return FromTask(task.State);
            }
            var status = ReadPortable();
            if (enabled && status.Blocked) return status;
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command);
            else if (string.Equals(key.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase)) key.DeleteValue(ValueName, false);
            return ReadPortable();
        }
        catch (Exception ex) { QQNotificationListener.Shared.ReportPresentationError(ex); return new(false, true, "无法更改自启动状态，请查看调试诊断。"); }
        finally { Gate.Release(); }
    }
}
