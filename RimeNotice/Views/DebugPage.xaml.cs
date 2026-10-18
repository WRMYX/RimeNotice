using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RimeNotice.Models;
using RimeNotice.Services;

namespace RimeNotice.Views;

public sealed partial class DebugPage : Page
{
    private bool _preview;
    public DebugPage()
    {
        InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required;
        Loaded += (_, _) => Refresh();
    }
    private void Refresh() => Diagnostics.Text = QQNotificationListener.Shared.DiagnosticSnapshot();
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimeNotice", "Diagnostics");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(file, QQNotificationListener.Shared.DiagnosticSnapshot()); ExportStatus.Text = "已导出：" + file;
        }
        catch (Exception ex) { ExportStatus.Text = "导出失败：" + ex.Message; }
    }
    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_preview || MainWindow.Notices is null || sender is not Button b || !Enum.TryParse<NoticePreviewKind>(b.Tag as string, out var kind)) return;
        _preview = true;
        try { await MainWindow.Notices.ShowPreviewAsync(kind); } finally { _preview = false; }
    }
    private async void LongPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_preview || MainWindow.Notices is null) return;
        _preview=true;
        try { await MainWindow.Notices.ShowTextPreviewAsync("张老师", "DroidDeck 是一个专门面向 Steam Deck 掌机的开源项目，核心作用是让 SteamOS 系统可以直接运行超1万款 Windows 游戏，不用额外折腾复杂的兼容层配置。\n它最大的优势是可以直接在 Steam 库中完成游戏的验证和适配，大幅降低了掌机玩 Windows 游戏的门槛，不用手动安装双系统，就能在原生 SteamOS 环境下获得更省心的体验。\n\n我们班主任让我把QQ登录在电脑上，老师们也在群里面发送一些消息，单单是有右下角的Windows通知还是不够，所以有了这个项目。\n其实在之前我也用过其他的软件，参考了一下bilibili的一位up做的软件，但是好像4月份发布之后就一直没有更新，有一些使用上的问题。"); }
        finally { _preview=false; }
    }
    private void ClearCache_Click(object sender, RoutedEventArgs e) { AppIconResolver.Clear(); ExportStatus.Text = "已清理 RN 内存图标缓存；未修改 QQ 文件或设置。"; }
    private void ExitDeveloper_Click(object sender, RoutedEventArgs e)
    {
        try { DeveloperSettings.SetEnabled(false); App.Instance.Main?.SetDeveloperMode(false); }
        catch (Exception ex) { ExportStatus.Text = ex.Message; }
    }
}
