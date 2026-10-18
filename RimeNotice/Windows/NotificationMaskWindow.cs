using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RimeNotice.Services;
using Windows.Graphics;
namespace RimeNotice.Windows;
public sealed class NotificationMaskWindow : Window {
 private readonly Grid _root=new(){Background=new SolidColorBrush(global::Windows.UI.Color.FromArgb(56,0,0,0))};
 private readonly TransparentBackdrop _backdrop=new();private bool _closed;private readonly NotificationAnimation _motion;
 public NotificationMaskWindow(bool topmost){Content=_root;_motion=new NotificationAnimation(_root,false);_motion.Prepare(true);_backdrop.Attach(this);AppWindow.IsShownInSwitchers=false;
  if(AppWindow.Presenter is OverlappedPresenter p){p.SetBorderAndTitleBar(false,false);p.IsResizable=false;p.IsMaximizable=false;p.IsMinimizable=false;p.IsAlwaysOnTop=topmost;}
  WindowPlumbing.PreventActivation(this);WindowPlumbing.EnableTransparency(this);WindowPlumbing.RemoveWindowBorder(this);WindowPlumbing.RemoveResizableFrame(this);
  _backdrop.Disconnected+=()=>{if(!_closed)AppWindow.Hide();};Closed+=(_,_)=>{_closed=true;_motion.Dispose();};
 }
 public void SetTopmost(bool value){if(AppWindow.Presenter is OverlappedPresenter p)p.IsAlwaysOnTop=value;}
 public Task FadeInAsync(bool enabled,System.Threading.CancellationToken token)=>_motion.ShowAsync(enabled,token);
 public Task FadeOutAsync(bool enabled,System.Threading.CancellationToken token)=>_motion.HideAsync(enabled,token);
 public async Task<bool> ShowMaskAsync(RectInt32 bounds){try{WindowPlumbing.SetOpacity(this,0);AppWindow.MoveAndResize(bounds);AppWindow.Show(false);
  bool ready;try{ready=await _backdrop.Ready.WaitAsync(TimeSpan.FromSeconds(3));}catch(TimeoutException){ready=false;}if(!ready||_closed){if(!_closed)Close();return false;}
  await TransparentBackdrop.WaitForFrameAsync(_root);if(!_closed)WindowPlumbing.SetOpacity(this,255);return !_closed;
 }catch(Exception ex){ServiceDiagnostics.Error("遮罩",ex);if(!_closed)Close();return false;}}
}
