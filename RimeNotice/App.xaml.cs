using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using RimeNotice.Services;
using RimeNotice.Windows;

namespace RimeNotice;

public partial class App : Application
{
    public static App Instance => (App)Current;
    public MainWindow? Main { get; private set; }
    private NativeTrayIcon? _tray;
    private TrayMenuWindow? _menu;
    private AppInstance? _instance;
    private bool _exiting;
    private System.Threading.Mutex? _updateMutex;
    private bool _ownsUpdateMutex;
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => { CrashLog.Write("UI", e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog.Write("AppDomain", e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) => { CrashLog.Write("Task", e.Exception); e.SetObserved(); };
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        bool settings = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--settings");
        _instance = AppInstance.FindOrRegisterForKey("RimeNotice.Main");
        if (!_instance.IsCurrent)
        {
            await _instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit(); return;
        }
        _updateMutex=new System.Threading.Mutex(false,@"Local\RimeNotice.SingleInstance");
        try{_ownsUpdateMutex=_updateMutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){_ownsUpdateMutex=true;}
        if(!_ownsUpdateMutex){_updateMutex.Dispose();_updateMutex=null;Exit();return;}
        var settingsStore=SettingsStore.Shared;
        bool freshInstall=settingsStore.IsFreshInstall;
        // Persist the first-run boundary before attempting any system startup registration.
        if(freshInstall){try{settingsStore.Save(settingsStore.Current);}catch(Exception ex){CrashLog.Write("首次设置保存",ex);freshInstall=false;}}
        Main = new MainWindow();
        _ = StartupService.InitializeForNewUserAsync(freshInstall);
        _instance.Activated += (_, activationArgs) => { if (activationArgs.Data is global::Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch && Array.Exists(launch.Arguments.Split(' '), a => a == "--settings")) Main.DispatcherQueue.TryEnqueue(() => ShowMain()); };
        try
        {
            _tray = new NativeTrayIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"));
            _tray.OpenRequested += () => Main.DispatcherQueue.TryEnqueue(() => ShowMain());
            _tray.MenuRequested += location => Main.DispatcherQueue.TryEnqueue(() =>
            {
                _menu ??= new TrayMenuWindow(() => ShowMain("settings"), ExitApplication);
                _menu.ShowAt(location, Main.Theme);
            });
        }
        catch (Exception ex) { QQNotificationListener.Shared.ReportPresentationError(ex); }
        Main.AppWindow.Closing += (_, e) =>
        {
            if (!_exiting && _tray is not null) { e.Cancel = true; Main.AppWindow.Hide(); }
        };
        Main.Closed += (_, _) => { if (!_exiting) ExitApplication(); };
        // Listening must not depend on a visible settings window or its Loaded event.
        _ = QQNotificationListener.Shared.StartAsync(requestPermission: false);
        if (_tray is null || settings) Main.Activate();
        else Main.AppWindow.Hide();
    }
    private static bool IsStartup(AppActivationArguments activation) =>
        activation.Kind == ExtendedActivationKind.StartupTask || activation.Data is global::Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch && Array.Exists(launch.Arguments.Split(' '), a => a == "--startup");
    public void ShowMain(string? section = null)
    {
        if (Main is null || _exiting) return;
        if (Main.AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized) p.Restore();
        Main.AppWindow.Show(); Main.Activate();
        if (section is not null) Main.NavigateTo(section);
    }
    public void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        QQNotificationListener.Shared.Stop();
        MainWindow.Notices?.Dispose();
        _tray?.Dispose(); _tray = null;
        _menu?.Close(); Main?.Close();
        if(_ownsUpdateMutex){_updateMutex?.ReleaseMutex();_ownsUpdateMutex=false;}_updateMutex?.Dispose();_updateMutex=null;Exit();
    }
}
