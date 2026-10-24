using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RimeNotice.Models;
using RimeNotice.Services;

namespace RimeNotice.Views;

public sealed partial class MonitorPage : Page
{
    public MonitorPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        Loaded += (_, _) => { QQNotificationListener.Shared.StatusChanged += UpdateStatus; UpdateStatus(); };
        Unloaded += (_, _) => QQNotificationListener.Shared.StatusChanged -= UpdateStatus;
    }

    private async void Permission_Click(object sender,RoutedEventArgs e)
    {
        try { await WindowsSettingsLauncher.OpenAsync(QQNotificationListener.Shared.PackageIdentity=="未打包"?"notifications":"privacy-notifications");LinkError.IsOpen=false; }
        catch(Exception ex){LinkError.Message=ex.Message;LinkError.IsOpen=true;}
    }
    private async void GrantPermission_Click(object sender,RoutedEventArgs e)
    {
        GrantPermission.IsEnabled=false;
        try{await QQNotificationListener.Shared.RequestAccessAsync();}
        catch(Exception ex){LinkError.Message="未能请求通知权限，请打开 Windows 通知访问设置。";LinkError.IsOpen=true;ServiceDiagnostics.Error("通知授权",ex);}
        finally{GrantPermission.IsEnabled=true;UpdateStatus();}
    }
    private void UpdateStatus()
    {
        var listener = QQNotificationListener.Shared;
        ReceptionHint.Severity = listener.IsRunning ? InfoBarSeverity.Success :
            listener.AccessStatus is "Denied" or "Unspecified" or "未检查" ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
        ReceptionHint.Title = listener.IsRunning ? "后台接收已开启" : "需要处理消息接收状态";
        ReceptionHint.Message = listener.IsRunning ? "关闭设置窗口后，RimeNotice 仍会在托盘中接收QQ 消息。" : listener.Detail;
        if(listener.QueueOverflowCount>0){ReceptionHint.Severity=InfoBarSeverity.Warning;ReceptionHint.Title="接收中，曾有消息超过等待上限";ReceptionHint.Message=$"本次运行有 {listener.QueueOverflowCount} 条消息未能排入 512 条等待队列，请到 QQ 查看。";}
        StatusTitle.Text = listener.Title;
        StatusDetail.Text = QQNotificationListener.Shared.Detail;
        GrantPermission.Visibility=listener.IsRunning||listener.PackageIdentity=="未打包"?Visibility.Collapsed:Visibility.Visible;
        PermissionLink.Content=listener.PackageIdentity=="未打包"?"打开 Windows 通知设置（兼容模式需显示 QQ 横幅）":"打开 Windows 通知访问设置";
        PermissionLink.Visibility = QQNotificationListener.Shared.IsRunning ? Visibility.Collapsed : Visibility.Visible;
    }

}
