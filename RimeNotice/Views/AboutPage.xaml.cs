using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using RimeNotice.Services;

namespace RimeNotice.Views;

public sealed partial class AboutPage : Page
{
    private int _clicks;
    private DateTime _lastClick;
    public AboutPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        var v = typeof(AboutPage).Assembly.GetName().Version;
        VersionText.Text = "版本 " + (v?.ToString(4) ?? "未知");
        Loaded += (_, _) => UpdateLogo();
        ActualThemeChanged += (_, _) => UpdateLogo();
    }
    private async void ProjectLink_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not FrameworkElement{Tag:string url})return;
        try{if(!await global::Windows.System.Launcher.LaunchUriAsync(new Uri(url)))throw new InvalidOperationException("请检查默认浏览器。");LinkError.IsOpen=false;}
        catch(Exception ex){LinkError.Title="无法打开链接";LinkError.Message=url+"，请复制到浏览器访问。";LinkError.IsOpen=true;ServiceDiagnostics.Error("关于链接",ex);}
    }
    private async void Feedback_Click(object sender, RoutedEventArgs e)
    {
        await OpenLinkAsync("https://github.com/WRMYX/RimeNotice/issues");
    }
    private async Task OpenLinkAsync(string url)
    {
        try { if (!await global::Windows.System.Launcher.LaunchUriAsync(new Uri(url))) throw new InvalidOperationException("请检查默认浏览器。"); LinkError.IsOpen=false; }
        catch(Exception ex){ LinkError.Title="无法打开链接"; LinkError.Message=url+"，请复制到浏览器访问。"; LinkError.IsOpen=true; ServiceDiagnostics.Error("关于链接",ex); }
    }
    private async void Support_Click(object sender, RoutedEventArgs e)
    {
        var dark = ActualTheme == ElementTheme.Dark;
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var wechat = SupportCard("微信", dark ? "ms-appx:///Assets/Support/WeChat-Dark.png" : "ms-appx:///Assets/Support/WeChat-Light.png");
        var alipay = SupportCard("支付宝", dark ? "ms-appx:///Assets/Support/Alipay-Dark.png" : "ms-appx:///Assets/Support/Alipay-Light.png");
        grid.Children.Add(wechat); grid.Children.Add(alipay); Grid.SetColumn(alipay, 1);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light, Title = "支持 RimeNotice", Content = new StackPanel { Spacing = 14, Children = { new TextBlock { Text = "感谢你使用 RimeNotice。项目由个人维护，支持将用于持续改进软件。", TextWrapping = TextWrapping.Wrap }, grid, new TextBlock { Text = "你可以选择微信或支付宝支持项目，金额由你决定。", TextWrapping = TextWrapping.Wrap, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] } } }, CloseButtonText = "关闭", DefaultButton = ContentDialogButton.Close };
        await dialog.ShowAsync();
    }
    private static Border SupportCard(string title, string source)
    {
        var image = new Image { Source = new BitmapImage(new Uri(source)), Width = 180, Height = 180, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Children = { image, new TextBlock { Text = title, HorizontalAlignment = HorizontalAlignment.Center, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] } } };
        return new Border { Padding = new Thickness(12), Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"], CornerRadius = new CornerRadius(8), Child = panel };
    }
    private void UpdateLogo()
    {
        var source = new BitmapImage(new Uri(ActualTheme == ElementTheme.Dark ? "ms-appx:///Assets/MYXMJY-white.png" : "ms-appx:///Assets/MYXMJY-black.png"));
        DeveloperLogo.Source = source;
        DeveloperHeaderLogo.Source = new BitmapImage(new Uri(source.UriSource.AbsoluteUri));
        var github = ActualTheme == ElementTheme.Dark ? "ms-appx:///Assets/GitHub-Mark-White.svg" : "ms-appx:///Assets/GitHub-Mark-Black.svg";
        ProjectGithubLogo.Source = new BitmapImage(new Uri(github));
        FeedbackGithubLogo.Source = new BitmapImage(new Uri(github));
    }
    private void DeveloperLogo_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (DateTime.UtcNow - _lastClick > TimeSpan.FromSeconds(3)) _clicks = 0;
        _lastClick = DateTime.UtcNow;
        if (++_clicks < 5) return;
        _clicks = 0;
        try { DeveloperSettings.SetEnabled(true); App.Instance.Main?.SetDeveloperMode(true); }
        catch (Exception ex) { QQNotificationListener.Shared.ReportPresentationError(ex); }
    }
}
