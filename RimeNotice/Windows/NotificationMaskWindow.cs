using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;
using RimeNotice.Services;
using Windows.Graphics;
namespace RimeNotice.Windows;
public sealed class NotificationMaskWindow : Window {
 private readonly Grid _root=new(){Background=new SolidColorBrush(global::Windows.UI.Color.FromArgb(56,0,0,0))};
 private readonly TransparentBackdrop _backdrop=new();private bool _closed;private Storyboard? _motion;
 public NotificationMaskWindow(bool topmost){Content=_root;_root.Opacity=0;_backdrop.Attach(this);AppWindow.IsShownInSwitchers=false;
  if(AppWindow.Presenter is OverlappedPresenter p){p.SetBorderAndTitleBar(false,false);p.IsResizable=false;p.IsMaximizable=false;p.IsMinimizable=false;p.IsAlwaysOnTop=topmost;}
  WindowPlumbing.PreventActivation(this);WindowPlumbing.EnableTransparency(this);WindowPlumbing.RemoveWindowBorder(this);WindowPlumbing.RemoveResizableFrame(this);
  WindowPlumbing.SetNotificationShadow(this,false);
  _backdrop.Disconnected+=()=>{if(!_closed)AppWindow.Hide();};Closed+=(_,_)=>{_closed=true;_motion?.Stop();};
 }
 public void SetTopmost(bool value){if(AppWindow.Presenter is OverlappedPresenter p)p.IsAlwaysOnTop=value;}
 public Task FadeInAsync(bool enabled,System.Threading.CancellationToken token)=>FadeAsync(true,enabled,token);
 public Task FadeOutAsync(bool enabled,System.Threading.CancellationToken token)=>FadeAsync(false,enabled,token);
 private async Task FadeAsync(bool show,bool enabled,System.Threading.CancellationToken token){
  if(_closed)return;
  _motion?.Stop();
  if(!enabled||!new UISettings().AnimationsEnabled){_root.Opacity=show?1:0;return;}
  _motion=new Storyboard();Timeline animation=show?new FadeInThemeAnimation():new FadeOutThemeAnimation();
  Storyboard.SetTarget(animation,_root);_motion.Children.Add(animation);
  var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);_motion.Completed+=(_,_)=>completed.TrySetResult();_motion.Begin();
  try{await completed.Task.WaitAsync(TimeSpan.FromSeconds(1),token);}catch(TimeoutException){ServiceDiagnostics.Set("遮罩动画","原生主题动画完成事件超时");}finally{_motion.Stop();if(!_closed){_root.Opacity=show?1:0;if(!show)AppWindow.Hide();}}
 }
 public async Task<bool> ShowMaskAsync(RectInt32 bounds){try{WindowPlumbing.SetOpacity(this,0);AppWindow.MoveAndResize(bounds);AppWindow.Show(false);
  bool ready;try{ready=await _backdrop.Ready.WaitAsync(TimeSpan.FromSeconds(3));}catch(TimeoutException){ready=false;}if(!ready||_closed){if(!_closed)Close();return false;}
  await TransparentBackdrop.WaitForFrameAsync(_root);if(!_closed)WindowPlumbing.SetOpacity(this,255);return !_closed;
 }catch(Exception ex){ServiceDiagnostics.Error("遮罩",ex);if(!_closed)Close();return false;}}
}
