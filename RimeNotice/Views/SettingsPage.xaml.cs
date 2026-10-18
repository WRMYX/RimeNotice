using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RimeNotice.Services;
namespace RimeNotice.Views;
public sealed partial class SettingsPage : Page
{
    private bool _ready, _startupUpdating;
    public SettingsPage()
    {
        InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required;
        Loaded += async (_, _) => {
            _ready = false; var s = SettingsStore.Shared.Current;
            ThemeChoice.SelectedIndex = s.Theme; NormalDuration.Value = s.NormalSeconds;
            ImportantDuration.Value = s.ImportantSeconds; CallingDuration.Value = s.CallingSeconds;
            _ready = true; _startupUpdating = true; StartupToggle.IsEnabled = false;
            try { ApplyStartup(await StartupService.GetStatusAsync()); }
            finally { _startupUpdating = false; }
        };
    }
    private void ApplyStartup(StartupStatus status)
    { StartupToggle.IsOn = status.Enabled; StartupToggle.IsEnabled = !status.Blocked; StartupDescription.Text = status.Message; }
    private async void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready || _startupUpdating) return;
        _startupUpdating = true; StartupToggle.IsEnabled = false;
        try { ApplyStartup(await StartupService.SetEnabledAsync(StartupToggle.IsOn)); }
        finally { _startupUpdating = false; }
    }
    private void Persist(bool themeChanged)
    {
        if (!_ready || double.IsNaN(NormalDuration.Value) || double.IsNaN(ImportantDuration.Value) || double.IsNaN(CallingDuration.Value)) return;
        try
        {
            SettingsStore.Shared.Save(SettingsStore.Shared.Current with {
                Theme = ThemeChoice.SelectedIndex, NormalSeconds = (int)NormalDuration.Value,
                ImportantSeconds = (int)ImportantDuration.Value, CallingSeconds = (int)CallingDuration.Value
            }); SaveError.IsOpen = false;
            if (themeChanged)
            {
                ThemeChoice.IsDropDownOpen = false;
                App.Instance.Main?.ApplyAppTheme(ThemeChoice.SelectedIndex);
            }
        }
        catch (Exception ex) { SaveError.Message = "更改未保存，请检查文件访问权限。"; SaveError.IsOpen = true; QQNotificationListener.Shared.ReportPresentationError(ex); }
    }
    private void DurationChanged(NumberBox sender, NumberBoxValueChangedEventArgs e) => Persist(false);
    private void ThemeChoice_SelectionChanged(object sender, SelectionChangedEventArgs e) => Persist(true);
}
