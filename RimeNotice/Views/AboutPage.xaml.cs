using System;
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
        Loaded += (_, _) => { UpdateLogo(); AdaptLogo(); };
        ProductGrid.SizeChanged += (_, _) => AdaptLogo();
        ActualThemeChanged += (_, _) => UpdateLogo();
    }
    private async void ProjectLink_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not HyperlinkButton{Tag:string url})return;
        try{if(!await global::Windows.System.Launcher.LaunchUriAsync(new Uri(url)))throw new InvalidOperationException("请检查默认浏览器。");LinkError.IsOpen=false;}
        catch(Exception ex){LinkError.Title="无法打开链接";LinkError.Message=url+"，请复制到浏览器访问。";LinkError.IsOpen=true;ServiceDiagnostics.Error("关于链接",ex);}
    }
    private void UpdateLogo() => DeveloperLogo.Source = new BitmapImage(new Uri(ActualTheme == ElementTheme.Dark ? "ms-appx:///Assets/MYXMJY-white.png" : "ms-appx:///Assets/MYXMJY-black.png"));
    private void AdaptLogo()
    {
        bool narrow = ProductGrid.ActualWidth < 460;
        ProductGrid.ColumnDefinitions[2].Width = new GridLength(narrow ? 0 : 160);
        Grid.SetColumn(DeveloperPanel, narrow ? 0 : 2);
        Grid.SetRow(DeveloperPanel, narrow ? 1 : 0);
        Grid.SetColumnSpan(DeveloperPanel, narrow ? 2 : 1);
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
