using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using RimeNotice.Models;
using RimeNotice.Windows;
namespace RimeNotice.Services;

public sealed class NoticePresenter : IDisposable
{
    private readonly Func<ElementTheme> _theme;
    private readonly OrderedNoticeQueue<IncomingNotice> _queue = new();
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private INotificationWindow? _active;
    private NotificationPresentation _activePresentation;
    private AppearanceOptions _activeAppearance=new();
    private string? _activeKey;
    public NoticePresenter(Func<ElementTheme> theme) { _theme = theme; _ = ReadAsync(); }
    private static string Key(IncomingNotice n) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(n.AppId + "\0" + n.SourceKey + "\0" + n.Sender + "\0" + n.Message)));
    public void Enqueue(IncomingNotice notice)
    {
        if (_stop.IsCancellationRequested) return;
        string key = Key(notice);
        if (_activeKey == key) return;
        if (!_queue.Enqueue(key, notice))
        {
            if (_queue.Count >= 512) QQNotificationListener.Shared.ReportQueueOverflow();
            return;
        }
        UpdatePending();
        if (_signal.CurrentCount == 0) _signal.Release();
    }
    private void UpdatePending()
    {
        _active?.SetPending(_queue.Preview(2), _queue.Count);
        ServiceDiagnostics.Set("通知队列", $"等待：{_queue.Count}；逐条显示，等待时不计时");
    }
    private async Task ReadAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await _signal.WaitAsync(_stop.Token);
                while (_queue.TryDequeue(out var notice)) { await ShowAsync(notice,true); UpdatePending(); }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    public Task ShowTextPreviewAsync(string sender, string message) => ShowAsync(new(sender, message, IsPreview: true));
    public Task ShowPreviewAsync(NoticePreviewKind kind) => ShowAsync(new("班级群 · 示例联系人",
        kind == NoticePreviewKind.Calling ? "示例呼叫：请课代表看到消息后及时回应。" : kind == NoticePreviewKind.Important ? "示例通知：明天上午有考试，请提前准备好文具。" : "这是一条示例消息，用于检查独立通知窗口。", kind, true, QQNotificationListener.Shared.QQLogo));
    private IncomingNotice? TakeNext()
    {
        return NotificationPresentationSession.TakeNext(_activeAppearance,SettingsStore.Shared.Current.Appearance,()=>{
            if(_stop.IsCancellationRequested||!_queue.TryDequeue(out var notice))return null;
            _activeKey=Key(notice);UpdatePending();return notice;
        });
    }
    private async Task ShowAsync(IncomingNotice notice,bool queued=false)
    {
        bool entered = false;
        try
        {
            await _gate.WaitAsync(_stop.Token); entered = true;
            if (_stop.IsCancellationRequested) return;
            _activeAppearance=SettingsStore.Shared.Current.Appearance;_activePresentation=_activeAppearance.Presentation;
            _active = _activePresentation==NotificationPresentation.Native ? new NativeNotificationWindow(notice,_theme()) : new NotificationWindow(notice,_theme());
            ServiceDiagnostics.Set("通知样式",_activePresentation==NotificationPresentation.Native?"原生 ContentDialog":"材质弹窗（自定义）");
            _activeKey = Key(notice);
            UpdatePending(); _active.ShowNotice(queued?TakeNext:null); await _active.Completion;
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { QQNotificationListener.Shared.ReportPresentationError(ex); }
        finally { if (entered) { _active = null; _activeKey = null; _gate.Release(); } }
    }
    public void Dispose() { _stop.Cancel(); _active?.Close(); _queue.Clear(); }
}
