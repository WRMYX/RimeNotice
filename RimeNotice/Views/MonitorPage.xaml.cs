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
        try { await WindowsSettingsLauncher.OpenAsync(QQNotificationListener.Shared.UsingCompatibility?"notifications":"privacy-notifications");LinkError.IsOpen=false; }
        catch(Exception ex){LinkError.Message=ex.Message;LinkError.IsOpen=true;}
    }
    private async void GrantPermission_Click(object sender,RoutedEventArgs e)
    {
        GrantPermission.IsEnabled=false;
        try{await QQNotificationListener.Shared.RequestAccessAsync();LinkError.IsOpen=false;}
        catch(Exception ex){LinkError.Message=$"未能请求通知访问：{ex.GetType().Name} / 0x{ex.HResult:X8}。可打开系统设置或改用 UIA。";LinkError.IsOpen=true;ServiceDiagnostics.Error("通知授权",ex);}
        finally{GrantPermission.IsEnabled=true;UpdateStatus();}
    }
    private void UseCompatibility_Click(object sender,RoutedEventArgs e)
    {
        try { var store=SettingsStore.Shared;store.Save(store.Current with{Reception=store.Current.Reception with{UseUIAutomation=true}});LinkError.IsOpen=false;UpdateStatus(); }
        catch(Exception ex){LinkError.Message=ex.Message;LinkError.IsOpen=true;}
    }
    private async void UseSystem_Click(object sender,RoutedEventArgs e)
    {
        UseSystem.IsEnabled=false;
        try { var store=SettingsStore.Shared;store.Save(store.Current with{Reception=store.Current.Reception with{UseUIAutomation=false}});await QQNotificationListener.Shared.RequestAccessAsync();LinkError.IsOpen=false; }
        catch(Exception ex){LinkError.Message=$"系统通知访问未完成：{ex.GetType().Name} / 0x{ex.HResult:X8}";LinkError.IsOpen=true;}
        finally { UseSystem.IsEnabled=true;UpdateStatus(); }
    }
    private void UpdateStatus()
    {
        var listener = QQNotificationListener.Shared;
        ReceptionHint.Severity = listener.IsRunning ? listener.UsingCompatibility?InfoBarSeverity.Informational:InfoBarSeverity.Success :
            listener.AccessStatus is "Denied" or "Unspecified" or "未检查" ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
        ReceptionHint.Title = listener.IsRunning ? listener.UsingCompatibility?"UIA 兼容扫描运行中":"系统通知接口已连接" : "需要处理消息接收状态";
        ReceptionHint.Message = listener.IsRunning ? listener.UsingCompatibility?"扫描运行不代表已识别到 QQ；识别数量和过滤情况可在调试页查看。":"已获授权并完成通知读取，等待新的 QQ 通知；关闭设置后继续在托盘运行。" : listener.Detail;
        if(listener.QueueOverflowCount>0){ReceptionHint.Severity=InfoBarSeverity.Warning;ReceptionHint.Title="接收中，曾有消息超过等待上限";ReceptionHint.Message=$"本次运行有 {listener.QueueOverflowCount} 条消息未能排入 512 条等待队列，请到 QQ 查看。";}
        StatusTitle.Text = listener.Title;
        StatusDetail.Text = QQNotificationListener.Shared.Detail;
        GrantPermission.Visibility=listener.CanRequestAccess?Visibility.Visible:Visibility.Collapsed;
        UseCompatibility.Visibility=!listener.UsingCompatibility&&!listener.IsRunning?Visibility.Visible:Visibility.Collapsed;
        UseSystem.Visibility=listener.UsingCompatibility?Visibility.Visible:Visibility.Collapsed;
        PermissionLink.Content=listener.UsingCompatibility?"打开 Windows 通知设置（UIA 需显示 QQ 横幅）":"打开 Windows 通知访问设置";
        PermissionLink.Visibility = listener.UsingCompatibility||!listener.IsRunning ? Visibility.Visible : Visibility.Collapsed;
    }

}
