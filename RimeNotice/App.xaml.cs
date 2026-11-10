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
        StartupDiagnostics.Write("app.construct");
        UnhandledException += (_, e) => { StartupDiagnostics.Write("fatal.ui", e.Exception); CrashLog.Write("UI", e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog.Write("AppDomain", e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) => { CrashLog.Write("Task", e.Exception); e.SetObserved(); };
        try { StartupDiagnostics.Write("xaml.initialize.begin"); InitializeComponent(); StartupDiagnostics.Write("xaml.initialize.ready"); }
        catch (Exception ex) { StartupDiagnostics.Write("fatal.xaml.initialize", ex); throw; }
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { await LaunchAsync(); }
        catch (Exception ex) { StartupDiagnostics.Write("fatal.launch", ex); throw; }
    }
    private async System.Threading.Tasks.Task LaunchAsync()
    {
        StartupDiagnostics.Write("launch.begin");
        try { var package = global::Windows.ApplicationModel.Package.Current; StartupDiagnostics.Write("package.context", detail: $"identity={package.Id.FullName}; signature={package.SignatureKind}"); }
        catch (InvalidOperationException) { StartupDiagnostics.Write("package.context", detail: "unpackaged"); }
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        bool settings = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--settings");
        StartupDiagnostics.Write("instance.register.begin");
        _instance = AppInstance.FindOrRegisterForKey("RimeNotice.Main");
        if (!_instance.IsCurrent)
        {
            await _instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            StartupDiagnostics.Write("instance.redirected.exit");
            Exit(); return;
        }
        _updateMutex=new System.Threading.Mutex(false,@"Local\RimeNotice.SingleInstance");
        try{_ownsUpdateMutex=_updateMutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){_ownsUpdateMutex=true;}
        if(!_ownsUpdateMutex){StartupDiagnostics.Write("instance.mutex.busy.exit");_updateMutex.Dispose();_updateMutex=null;Exit();return;}
        StartupDiagnostics.Write("settings.load.begin");
        var settingsStore=SettingsStore.Shared;
        bool freshInstall=settingsStore.IsFreshInstall;
        // Persist the first-run boundary before attempting any system startup registration.
        if(freshInstall){try{settingsStore.Save(settingsStore.Current);}catch(Exception ex){CrashLog.Write("首次设置保存",ex);freshInstall=false;}}
        StartupDiagnostics.Write("settings.load.ready");
        StartupDiagnostics.Write("window.create.begin");
        Main = new MainWindow();
        StartupDiagnostics.Write("window.create.ready");
        _ = StartupService.InitializeForNewUserAsync(freshInstall);
        _instance.Activated += (_, activationArgs) => { if (activationArgs.Data is global::Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch && Array.Exists(launch.Arguments.Split(' '), a => a == "--settings")) Main.DispatcherQueue.TryEnqueue(() => ShowMain()); };
        try
        {
            StartupDiagnostics.Write("tray.create.begin");
            _tray = new NativeTrayIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"));
            StartupDiagnostics.Write("tray.create.ready");
            _tray.OpenRequested += () => Main.DispatcherQueue.TryEnqueue(() => ShowMain());
            _tray.MenuRequested += location => Main.DispatcherQueue.TryEnqueue(() =>
            {
                _menu ??= new TrayMenuWindow(() => ShowMain("settings"), ExitApplication);
                _menu.ShowAt(location, Main.Theme);
            });
        }
        catch (Exception ex) { StartupDiagnostics.Write("tray.create.failed", ex); QQNotificationListener.Shared.ReportPresentationError(ex); }
        Main.AppWindow.Closing += (_, e) =>
        {
            if (!_exiting && _tray is not null) { e.Cancel = true; Main.AppWindow.Hide(); }
        };
        Main.Closed += (_, _) => { if (!_exiting) ExitApplication(); };
        // Listening must not depend on a visible settings window or its Loaded event.
        _ = QQNotificationListener.Shared.StartAsync(requestPermission: false);
        StartupDiagnostics.Write("listener.start.requested");
        if (_tray is null || settings) Main.Activate();
        else Main.AppWindow.Hide();
        StartupDiagnostics.Write("launch.ready", detail: _tray is null || settings ? "settings-visible" : "tray-only");
    }
    private static bool IsStartup(AppActivationArguments activation) =>
        activation.Kind == ExtendedActivationKind.StartupTask || activation.Data is global::Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch && Array.Exists(launch.Arguments.Split(' '), a => a == "--startup");
    public void ShowMain(string? section = null)
    {
        if (Main is null || _exiting) return;
        if (Main.AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized) p.Restore();
        Main.AppWindow.Show(); Main.Activate();
        StartupDiagnostics.Write("settings.opened");
        if (section is not null) Main.NavigateTo(section);
    }
    public void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        StartupDiagnostics.Write("app.exit.requested");
        QQNotificationListener.Shared.Stop();
        MainWindow.Notices?.Dispose();
        _tray?.Dispose(); _tray = null;
        _menu?.Close(); Main?.Close();
        if(_ownsUpdateMutex){_updateMutex?.ReleaseMutex();_ownsUpdateMutex=false;}_updateMutex?.Dispose();_updateMutex=null;Exit();
    }
}
