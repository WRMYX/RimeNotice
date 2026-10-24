using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RimeNotice.Services;
using Windows.Graphics;

namespace RimeNotice.Windows;

// Waiting cards have no countdown, sound, permission request or message-processing loop.
public sealed class PendingNoticeWindow : Window
{
    private readonly StackPanel _cards = new() { Spacing = 4, IsHitTestVisible = false };
    private readonly TransparentBackdrop _backdrop=new();
    private bool _closed;private int _revision;
    public PendingNoticeWindow(ElementTheme theme)
    {
        _cards.RequestedTheme = theme; Content = _cards;_backdrop.Attach(this);
        Closed+=(_,_)=>{_closed=true;++_revision;};
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter p) { p.SetBorderAndTitleBar(false, false); p.IsResizable = false; p.IsMinimizable = false; p.IsMaximizable = false; }
        WindowPlumbing.RemoveResizableFrame(this); WindowPlumbing.RemoveWindowBorder(this);
        WindowPlumbing.PreventActivation(this);WindowPlumbing.EnableTransparency(this);WindowPlumbing.SetNotificationShadow(this,false);
    }
    public void Hide(){if(_closed)return;++_revision;AppWindow.Hide();}
    public async void Update(IncomingNotice[] pending, int count, Window active, RectInt32 work, double scale, bool topmost,RectInt32? anchor=null,bool aboveActive=false)
    {
        if(_closed)return;int revision=++_revision;
        if (count == 0) { AppWindow.Hide(); return; }
        try{
        _cards.Children.Clear();
        foreach (var item in pending)
        {
            var card = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource LayerFillColorDefaultBrush}' BorderBrush='{ThemeResource CardStrokeColorDefaultBrush}' BorderThickness='1' CornerRadius='8' Padding='12,6'/>");
            card.Background=new SolidColorBrush(_cards.ActualTheme==ElementTheme.Dark?global::Windows.UI.Color.FromArgb(255,40,40,40):global::Windows.UI.Color.FromArgb(255,250,250,250));
            card.Child = new TextBlock { Text = item.Sender + " · 等待显示", FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            _cards.Children.Add(card);
        }
        if (count > pending.Length) _cards.Children.Add(new Border {
            Background=new SolidColorBrush(_cards.ActualTheme==ElementTheme.Dark?global::Windows.UI.Color.FromArgb(255,40,40,40):global::Windows.UI.Color.FromArgb(255,250,250,250)),
            CornerRadius=new CornerRadius(8),Padding=new Thickness(12,6,12,6),
            Child=new TextBlock{Text=$"另有 {count-pending.Length} 条消息等待显示",FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis}
        });
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = topmost;
        var bounds=anchor??new RectInt32(active.AppWindow.Position.X,active.AppWindow.Position.Y,active.AppWindow.Size.Width,active.AppWindow.Size.Height);
        int width = Math.Max(160, bounds.Width - (int)(24 * scale));
        _cards.Measure(new global::Windows.Foundation.Size(width/scale,double.PositiveInfinity));
        int height = Math.Max(1,(int)Math.Ceiling(_cards.DesiredSize.Height*scale));
        int y = Math.Min(bounds.Y + bounds.Height + (int)(4 * scale), work.Y + work.Height - height - 4);
        AppWindow.MoveAndResize(new RectInt32(bounds.X + (int)(12 * scale), y, width, height));
        bool readyBefore=_backdrop.IsReady;if(!readyBefore)WindowPlumbing.SetOpacity(this,0);AppWindow.Show(false);
        bool ready=await _backdrop.Ready.WaitAsync(TimeSpan.FromSeconds(3));
        if(_closed||revision!=_revision)return;
        if(!ready){AppWindow.Hide();return;}
        await TransparentBackdrop.WaitForFrameAsync(_cards);
        if(_closed||revision!=_revision)return;
        WindowPlumbing.SetOpacity(this,255);
        // Place below the active card; do not create another focusable foreground surface.
        SetWindowPos(WinRT.Interop.WindowNative.GetWindowHandle(this),aboveActive?IntPtr.Zero:WinRT.Interop.WindowNative.GetWindowHandle(active), 0, 0, 0, 0, 0x0013);
        }catch(Exception ex){ServiceDiagnostics.Error("等待卡片",ex);if(!_closed)AppWindow.Hide();}
    }
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
