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

    private void UpdateStatus()
    {
        var listener = QQNotificationListener.Shared;
        ReceptionHint.Severity = listener.IsRunning ? InfoBarSeverity.Success :
            listener.AccessStatus is "Denied" or "Unspecified" or "未检查" ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
        ReceptionHint.Title = listener.IsRunning ? "后台接收已开启" : "需要处理消息接收状态";
        ReceptionHint.Message = listener.IsRunning ? "关闭设置窗口后，RimeNotice 仍会在托盘中接收所选应用的消息。" : listener.Detail;
        StatusTitle.Text = listener.Title;
        StatusDetail.Text = QQNotificationListener.Shared.Detail;
        PermissionLink.Visibility = QQNotificationListener.Shared.IsRunning ? Visibility.Collapsed : Visibility.Visible;
    }

}
