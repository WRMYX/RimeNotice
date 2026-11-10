using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RimeNotice.Views;
using RimeNotice.Services;
using Windows.Graphics;

namespace RimeNotice;

public sealed partial class MainWindow : Window
{
    public static NoticePresenter? Notices { get; private set; }
    private bool _restoringSelection;
    private object? _lastNavigationItem;
    private readonly Dictionary<string, (string Tag, string[] Terms)> _settingSearch = new(StringComparer.OrdinalIgnoreCase)
    {
        ["消息接收"] = ("monitor", new[]{"消息", "QQ", "接收"}),
        ["通知外观"] = ("appearance", new[]{"外观", "材质", "弹窗", "颜色", "字体", "动画", "Logo"}),
        ["声音与语音"] = ("audio", new[]{"声音", "提示音", "朗读", "语音", "音量"}),
        ["语音包管理"] = ("voices", new[]{"语音包", "下载", "MSIX", "声音"}),
        ["接收兼容"] = ("reception", new[]{"接收", "通知权限", "图片", "附件", "QQ"}),
        ["提醒规则"] = ("rules", new[]{"规则", "联系人", "关键词", "黑名单", "白名单", "呼叫"}),
        ["常规"] = ("settings", new[]{"常规", "启动", "自启动", "主题", "托盘"}),
        ["更新"] = ("updates", new[]{"更新", "GitHub", "商店", "版本"}),
        ["关于"] = ("about", new[]{"关于", "项目", "官网", "反馈", "支持"}),
        ["开机自启动"] = ("settings", new[]{"开机自启动", "启动应用", "登录 Windows"}),
        ["应用主题"] = ("settings", new[]{"应用主题", "浅色", "深色"}),
        ["设置窗口材质"] = ("settings", new[]{"设置窗口材质", "Mica", "亚克力", "纯色"}),
        ["系统通知接收方式"] = ("reception", new[]{"系统通知接收方式", "事件驱动", "定时扫描"}),
        ["扫描间隔"] = ("reception", new[]{"扫描间隔", "扫描"}),
        ["图片与附件预览"] = ("reception", new[]{"图片与附件预览", "图片", "附件"}),
        ["QQ 账号"] = ("reception", new[]{"QQ 账号", "账号"}),
        ["提示音音量"] = ("audio", new[]{"提示音音量", "音量"}),
        ["语音播报"] = ("audio", new[]{"语音播报", "朗读", "语音"}),
        ["本机语音"] = ("audio", new[]{"本机语音", "声音", "SAPI"}),
        ["重要联系人"] = ("rules", new[]{"重要联系人", "老师", "联系人"}),
        ["重要关键词"] = ("rules", new[]{"重要关键词", "关键词", "作业", "通知"}),
        ["启动时检查更新"] = ("updates", new[]{"启动时检查更新", "自动检查", "检查更新"}),
        ["项目地址 GitHub"] = ("about", new[]{"项目地址", "GitHub"}),
        ["问题反馈"] = ("about", new[]{"问题反馈", "Issues", "反馈"})
    };
    private static readonly HashSet<string> SearchPageLabels = new(StringComparer.OrdinalIgnoreCase) { "消息接收", "通知外观", "声音与语音", "语音包管理", "接收兼容", "提醒规则", "常规", "更新", "关于" };

    public MainWindow()
    {
        InitializeComponent();
        WindowRoot.RequestedTheme = SettingsStore.Shared.Current.Theme switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
        Notices = new NoticePresenter(() => Theme);
        QQNotificationListener.Shared.MessageReceived += Notices.Enqueue;
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Resize(new SizeInt32(Math.Min(1000, work.Width - 48), Math.Min(720, work.Height - 48)));
        AppWindow.Move(new PointInt32(work.X + (work.Width - AppWindow.Size.Width) / 2,
            work.Y + (work.Height - AppWindow.Size.Height) / 2));
        WindowRoot.SizeChanged += (_, _) => AdaptLayout();
        Navigation.PaneOpened += (_, _) => AdaptLayout();
        Navigation.PaneClosed += (_, _) => AdaptLayout();
        ContentHost.SizeChanged += (_, _) => AdaptLayout();
        ContentFrame.NavigationFailed += (_, e) =>
        {
            e.Handled = true;
            ReportNavigationError(e.SourcePageType, e.Exception);
        };
        ContentFrame.Navigated += (_, _) => DispatcherQueue.TryEnqueue(AdaptLayout);
        WindowRoot.ActualThemeChanged += (_, _) => ApplyTitleBarTheme();
        ApplyTitleBarTheme();
        SettingsStore.Shared.Changed += ApplySettingsMaterial;
        Closed += (_, _) => { _closed = true; SettingsStore.Shared.Changed -= ApplySettingsMaterial; };
        ApplySettingsMaterial();
        SettingsSearch.ItemsSource = _settingSearch.Keys.Where(k => !SearchPageLabels.Contains(k)).ToArray();
        _ = CheckStartupUpdatesAsync();
    }
    private void SettingsSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        string query = sender.Text.Trim();
        sender.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _settingSearch.Keys.Where(k => !SearchPageLabels.Contains(k)).ToArray()
            : _settingSearch.Where(x => !SearchPageLabels.Contains(x.Key) && MatchesSearch(x.Key, x.Value.Terms, query)).Select(x => x.Key).ToArray();
    }
    private void SettingsSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string query = (args.QueryText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(query)) return;
        var match = _settingSearch.FirstOrDefault(x => !SearchPageLabels.Contains(x.Key) && MatchesSearch(x.Key, x.Value.Terms, query));
        if (!string.IsNullOrEmpty(match.Key)) { NavigateTo(match.Value.Tag); DispatcherQueue.TryEnqueue(async () => { await System.Threading.Tasks.Task.Delay(80); BringSearchTargetIntoView(match.Key); }); }
    }
    private static bool MatchesSearch(string title, string[] terms, string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length == 0 || tokens.All(token => title.Contains(token, StringComparison.OrdinalIgnoreCase) || terms.Any(term => term.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }
    private void BringSearchTargetIntoView(string query)
    {
        if (ContentFrame.Content is not FrameworkElement root) return;
        FrameworkElement? found = null;
        void Walk(DependencyObject node)
        {
            if (found is not null) return;
            if (node is TextBlock text && (text.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)) { found = text; return; }
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) Walk(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
        }
        Walk(root); found?.StartBringIntoView();
    }
    private bool _closed, _themePending;
    private int _requestedTheme;
    private void ApplySettingsMaterial()
    {
        if (_closed) return;
        ApplyWindowBehavior();
        var a = SettingsStore.Shared.Current.Appearance;
        WindowMaterialService.Apply(this, WindowRoot, a.SettingsMaterial, a.SolidColor);
    }
    private void ApplyWindowBehavior()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = false;
    }
    private async System.Threading.Tasks.Task CheckStartupUpdatesAsync()
    {
        if (!SettingsStore.Shared.Current.Updates.CheckOnStartup || GitHubUpdateService.Channel == RimeNotice.Core.GitHubUpdateChannel.Store) return;
        try { var release = await new GitHubUpdateService().CheckAsync(System.Threading.CancellationToken.None);
            if (!_closed && release is not null && release.Version > GitHubUpdateService.InstalledVersion) { UpdateNav.InfoBadge = new InfoBadge { Value = 1 }; }
        } catch (Exception ex) { ServiceDiagnostics.Error("启动检查更新", ex); }
    }
    public void ApplyAppTheme(int theme)
    {
        _requestedTheme = theme;
        if (_closed || _themePending) return;
        _themePending = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _themePending = false;
            if (_closed) return;
            try { WindowRoot.RequestedTheme = _requestedTheme switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default }; }
            catch (Exception ex) { CrashLog.Write("主题切换", ex); }
        })) _themePending = false;
    }

    public ElementTheme Theme => WindowRoot.XamlRoot is null && WindowRoot.RequestedTheme == ElementTheme.Default
        ? (Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light)
        : WindowRoot.ActualTheme;
    public void SetDeveloperMode(bool enabled)
    {
        DeveloperNav.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (!enabled && (Navigation.SelectedItem as NavigationViewItem)?.Tag as string == "debug") NavigateTo("monitor");
    }
    public void NavigateTo(string tag)
    {
        foreach (var entry in Navigation.MenuItems) if (entry is NavigationViewItem item && item.Tag as string == tag) Navigation.SelectedItem = item;
        foreach (var entry in Navigation.FooterMenuItems) if (entry is NavigationViewItem item && item.Tag as string == tag) Navigation.SelectedItem = item;
    }
    private void Navigation_Loaded(object sender, RoutedEventArgs e)
    {
        SetDeveloperMode(DeveloperSettings.Enabled);
        Navigation.IsPaneOpen = true;
        Navigation.SelectedItem = Navigation.MenuItems[0];
        AdaptLayout();
    }

    private void ApplyTitleBarTheme()
    {
        bool dark = WindowRoot.ActualTheme == ElementTheme.Dark;
        var bar = AppWindow.TitleBar;
        bar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        bar.ButtonInactiveForegroundColor = dark ? Colors.Gray : Colors.DarkGray;
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    private void AdaptLayout()
    {
        if (ContentHost.ActualWidth <= 0) return;
        double width = Math.Max(0, Math.Min(1248, ContentHost.ActualWidth - (ContentHost.ActualWidth > 600 ? 72 : 48)));
        SectionHeader.Width = width;
        ContentFrame.Width = width;
        double scale = WindowRoot.XamlRoot?.RasterizationScale ?? 1;
        AppTitleBar.Padding = new Thickness(16, 0, Math.Max(144, AppWindow.TitleBar.RightInset / scale), 0);
        if (ContentFrame.Content is Page page) AdaptRows(page.Content, width < 500);
    }

    // Walk only authored page content; never traverse a native control template.
    private static void AdaptRows(object? node, bool narrow)
    {
        if (node is Grid row && row.Tag as string == "SettingRow") SettingsRowLayout.Apply(row);
        if (node is Panel panel) foreach (var child in panel.Children) AdaptRows(child, narrow);
        else if (node is Border border) AdaptRows(border.Child, narrow);
        else if (node is ContentControl control) AdaptRows(control.Content, narrow);
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (ContentFrame is null || _restoringSelection) return;
        string key = args.SelectedItemContainer?.Tag as string ?? "monitor";
        Type page = key switch
        {
            "appearance" => typeof(NotificationOptionsPage), "audio" => typeof(AudioPage), "reception" => typeof(ReceptionPage), "debug" => typeof(DebugPage), "rules" => typeof(RulesPage), "settings" => typeof(SettingsPage),
            "voices" => typeof(VoicePackagesPage), "updates" => typeof(UpdatesPage), "about" => typeof(AboutPage), _ => typeof(MonitorPage)
        };
        try
        {
            if (ContentFrame.CurrentSourcePageType != page && !ContentFrame.Navigate(page))
            {
                if (!NavigationError.IsOpen) ReportNavigationError(page, new InvalidOperationException("Frame.Navigate returned false."));
                RestoreNavigationSelection();
                return;
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("导航 " + page.Name, ex);
            ReportNavigationError(page, ex);
            RestoreNavigationSelection();
            return;
        }
        _lastNavigationItem = sender.SelectedItem;
        NavigationError.IsOpen = false;
        PageTitle.Text = key switch
        {
            "voices" => "语音包下载", "appearance" => "通知外观", "audio" => "声音与语音", "reception" => "接收兼容", "debug" => "调试", "rules" => "提醒规则", "settings" => "常规", "updates" => "更新", "about" => "关于", _ => "消息接收"
        };
        PageDescription.Text = key switch
        {
            "appearance" => "调整材质、字体、动画和通知行为。",
            "audio" => "配置提示音、本机语音及可选在线语音。",
            "reception" => "配置接收范围、兼容来源和附件预览。",
            "debug" => "检查通知权限、接收流程与运行诊断。",
            "rules" => "设置消息分级、过滤与呼叫提醒。",
            "settings" => "设置自启动、应用主题和托盘菜单。",
            "voices" => "下载中文语音 MSIX，查看并打开文件位置。",
            "updates" => "检查版本、查看更新说明并安装更新。",
            "about" => "了解 RimeNotice。",
            _ => "查看 QQ 消息接收状态。"
        };
        DispatcherQueue.TryEnqueue(AdaptLayout);
    }
    private void ReportNavigationError(Type? page, Exception exception)
    {
        ServiceDiagnostics.Error("页面导航：" + (page?.Name ?? "未知页面"), exception);
        NavigationError.Message = "未能打开此页面，已保留当前页面。错误信息可在调试页查看。";
        NavigationError.IsOpen = true;
    }
    private void RestoreNavigationSelection()
    {
        _restoringSelection = true;
        try { Navigation.SelectedItem = _lastNavigationItem; }
        finally { _restoringSelection = false; }
    }

}
