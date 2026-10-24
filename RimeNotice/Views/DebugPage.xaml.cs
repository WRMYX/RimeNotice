using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RimeNotice.Models;
using RimeNotice.Services;

namespace RimeNotice.Views;

public sealed partial class DebugPage : Page
{
    private bool _preview;
    private bool _syncingHost=true;
    private static bool _noticeShown;
    public DebugPage()
    {
        InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required;
        SyncHostOptions();
        Loaded += async (_, _) => {
            SyncHostOptions();
            Refresh();
            if (_noticeShown || XamlRoot is null) return;
            _noticeShown = true;
            try { await new ContentDialog { XamlRoot = XamlRoot, Title = "恭喜你找到了一个小彩蛋", Content = "开发者的调试页面。实验功能默认关闭，可自行开启或退出开发者调试。", CloseButtonText = "知道了" }.ShowAsync(); }
            catch (Exception ex) { _noticeShown = false; ServiceDiagnostics.Error("调试提示", ex); }
        };
    }
    private void SyncHostOptions()
    {
        _syncingHost=true;var a=SettingsStore.Shared.Current.Appearance;
        NativeHostChoice.SelectedIndex=(int)a.NativeHost;HostMaskToggle.IsOn=a.Mask;_syncingHost=false;
    }
    private void NativeHost_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(_syncingHost||NativeHostChoice.SelectedIndex<0)return;
        SaveHostOptions();
    }
    private void HostMask_Toggled(object sender,RoutedEventArgs e){if(!_syncingHost)SaveHostOptions();}
    private void SaveHostOptions()
    {
        try{var s=SettingsStore.Shared.Current;SettingsStore.Shared.Save(s with{Appearance=s.Appearance with{NativeHost=(NativeNotificationHost)NativeHostChoice.SelectedIndex,Mask=HostMaskToggle.IsOn}});Refresh();}
        catch(Exception ex){ExportStatus.Text="实验选项保存失败："+ex.Message;ServiceDiagnostics.Error("原生父窗口选项",ex);}
    }
    private void Refresh() { Diagnostics.Text = QQNotificationListener.Shared.DiagnosticSnapshot(); CaptureStatus.Text = DiagnosticCapture.Status + "\n" + DiagnosticCapture.LastPath; }
    private async void StartCapture_Click(object sender, RoutedEventArgs e)
    {
        try {
            DiagnosticCapture.Start(); Refresh();
            var options=SettingsStore.Shared.Current.Reception;
            DiagnosticCapture.Write("capture.options",new{options.PreviewImages,options.InferRecentImages,options.AttachmentWaitSeconds,options.UseUIAutomation,options.EventDriven,options.ScanSeconds,appearance=SettingsStore.Shared.Current.Appearance});
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var files=await QQImageIndex.Shared.SnapshotAsync(options,timeout.Token);
            if(DiagnosticCapture.Active)DiagnosticCapture.Write("file.snapshot",new{total=files.Length,truncated=files.Length>2000,files=files.OrderByDescending(f=>f.ObservedAt?.UtcDateTime??f.WrittenUtc).Take(2000).Select(f=>new{relativePath=Path.GetRelativePath(NotificationAttachmentResolver.DirectoryFor(options),f.Path),f.Length,f.CreatedUtc,f.WrittenUtc,f.ObservedAt}).ToArray()});
        }
        catch (Exception ex) { CaptureStatus.Text = "采样或初始快照异常：" + ex.Message; DiagnosticCapture.Write("capture.snapshot-error",new{error=ex.GetType().Name}); }
    }
    private async void StopCapture_Click(object sender, RoutedEventArgs e)
    {
        await DiagnosticCapture.StopAsync(); Refresh();
    }
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
        try { await MainWindow.Notices.ShowTextPreviewAsync("张老师", "昨日一去不复回～欧耶耶！开心比什么都贵～覆水不能在收回～哦耶耶！桃花🌸谢了有玫瑰🌹～人生💪就像朋友的这杯烈酒🍺🍺烫在我心❤️上温暖我凡事别再放心❤️上🍺🍺～耶✌！往事后不后悔慢慢去体会～此刻朋友👫这杯酒🍺最珍贵！～快把酒🍺满上干了这杯🍺大声歌唱🎤～好朋友好朋友👫今宵多欢畅～理想改变了我们的模样👨也让我懂得了珍惜朋友的肩膀💪～阳光☀总会在风🌪️雨🌧️之后撒向苍茫🍂让我们抬起头🐷学会了坚强💪就像朋友的这杯烈酒🍺🍺烫我心❤️上温暖我凡事别再放"); }
        finally { _preview=false; }
    }
    private void ClearCache_Click(object sender, RoutedEventArgs e) { AppIconResolver.Clear(); ExportStatus.Text = "已清理 RN 内存图标缓存；未修改 QQ 文件或设置。"; }
    private void ExitDeveloper_Click(object sender, RoutedEventArgs e)
    {
        try { DeveloperSettings.SetEnabled(false); App.Instance.Main?.SetDeveloperMode(false); }
        catch (Exception ex) { ExportStatus.Text = ex.Message; }
    }
}
