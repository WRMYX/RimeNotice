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
        bool startup = IsStartup(activation) || Array.Exists(Environment.GetCommandLineArgs(), a => a == "--startup");
        _instance = AppInstance.FindOrRegisterForKey("RimeNotice.Main");
        if (!_instance.IsCurrent)
        {
            await _instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit(); return;
        }
        _updateMutex=new System.Threading.Mutex(false,@"Local\RimeNotice.SingleInstance");
        try{_ownsUpdateMutex=_updateMutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){_ownsUpdateMutex=true;}
        if(!_ownsUpdateMutex){_updateMutex.Dispose();_updateMutex=null;Exit();return;}
        Main = new MainWindow();
        _instance.Activated += (_, activationArgs) => { if (!IsStartup(activationArgs)) Main.DispatcherQueue.TryEnqueue(() => ShowMain()); };
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
        if (startup && _tray is not null)
            await QQNotificationListener.Shared.StartAsync(requestPermission: false);
        else Main.Activate();
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
