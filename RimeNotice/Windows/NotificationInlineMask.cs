using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using RimeNotice.Services;

namespace RimeNotice.Windows;
// A background element in the real notification window; never another HWND.
internal sealed class NotificationInlineMask : Grid
{
 internal NotificationInlineMask()
 {
  Background=new SolidColorBrush(global::Windows.UI.Color.FromArgb(56,0,0,0));Opacity=0;
  PointerPressed+=(_,e)=>e.Handled=true;
 }
 internal async Task FadeAsync(bool show,CancellationToken token)
 {
  if(!new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled){Opacity=show?1:0;return;}
  var motion=new Storyboard();Timeline animation=show?new FadeInThemeAnimation():new FadeOutThemeAnimation();
  Storyboard.SetTarget(animation,this);motion.Children.Add(animation);
  var complete=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  motion.Completed+=(_,_)=>complete.TrySetResult();motion.Begin();
  try{await complete.Task.WaitAsync(TimeSpan.FromSeconds(1),token);}
  catch(TimeoutException){ServiceDiagnostics.Set("同窗口遮罩动画","官方主题动画完成事件超时，画面未通过");}
  finally{motion.Stop();Opacity=show?1:0;}
 }
}
