using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RimeNotice.Models;
using Windows.Graphics;
namespace RimeNotice.Services;

// Custom material mode only. Animate the existing HWND and its content together;
// no ContentDialog Visual changes, transition HWND, AnimateWindow or Win2D.
internal static class MaterialNotificationAnimation {
 internal static async Task PlayAsync(Window window,Panel root,AppearanceOptions appearance,RectInt32 monitor,bool entering,CancellationToken token){
  var options=appearance.MaterialMotion.Normalize();
  bool none=entering?options.Entrance==MaterialEntrance.None:options.Exit==MaterialExit.None;
  if(none||!appearance.Animation||!new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled){if(entering)window.AppWindow.Show(false);return;}
  var baseline=new RectInt32(window.AppWindow.Position.X,window.AppWindow.Position.Y,window.AppWindow.Size.Width,window.AppWindow.Size.Height);
  bool alpha=true;
  var easing=MaterialEasing.Create(entering,entering?options.EntranceExponent:options.ExitExponent);
  bool layeredBefore=false;int frames=0;double maxFrame=0;var clock=new Stopwatch();double previous=0;
  bool finalFrame=false;
  var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  void Apply(double t){
   double eased=easing.Ease(Math.Clamp(t,0,1));int x=baseline.X,y=baseline.Y,w=baseline.Width,h=baseline.Height;
   int direction=entering?(int)options.Entrance:(int)options.Exit;
   double distance=entering?1-eased:eased;
   if(direction==3)y=baseline.Y+(int)Math.Round((monitor.Y-baseline.Height-baseline.Y)*distance);
   if(direction==4)y=baseline.Y+(int)Math.Round((monitor.Y+monitor.Height-baseline.Y)*distance);
   if(direction==5)x=baseline.X+(int)Math.Round((monitor.X-baseline.Width-baseline.X)*distance);
   if(direction==6)x=baseline.X+(int)Math.Round((monitor.X+monitor.Width-baseline.X)*distance);
   if(window.AppWindow.Position.X!=x||window.AppWindow.Position.Y!=y||window.AppWindow.Size.Width!=w||window.AppWindow.Size.Height!=h)window.AppWindow.MoveAndResize(new RectInt32(x,y,w,h));
   double opacity=entering?eased:1-eased;
   if(alpha)WindowPlumbing.SetOpacity(window,(byte)Math.Clamp((int)Math.Round(255*opacity),0,255));
  }
  EventHandler<object>? rendering=null;
  try{
   if(alpha)layeredBefore=WindowPlumbing.HasLayeredStyle(window);
   WindowPlumbing.SetNotificationShadow(window,false);
   Apply(0);if(entering)window.AppWindow.Show(false);
   int duration=entering?options.EntranceMilliseconds:options.ExitMilliseconds;
   rendering=(_,_)=>{try{if(finalFrame){done.TrySetResult();return;}if(!clock.IsRunning){clock.Start();previous=0;Apply(0);return;}double now=clock.Elapsed.TotalMilliseconds;maxFrame=Math.Max(maxFrame,now-previous);previous=now;frames++;double t=Math.Min(1,now/duration);Apply(t);if(t>=1)finalFrame=true;}catch(Exception ex){done.TrySetException(ex);}};
   CompositionTarget.Rendering+=rendering;
   await done.Task.WaitAsync(TimeSpan.FromMilliseconds(duration+2500),token);
   ServiceDiagnostics.Set(entering?"通知入场动画":"通知退场动画",$"自定义材质整窗：{(entering?options.Entrance.ToString():options.Exit.ToString())}；{frames} 帧，最大间隔 {maxFrame:F0} ms；材质同步待画面验收");
  }finally{
   if(rendering is not null)CompositionTarget.Rendering-=rendering;
   // Hide before restoring exit geometry/alpha so the final frame cannot flash back.
   if(!entering)window.AppWindow.Hide();
   if(alpha)WindowPlumbing.RestoreOpacity(window,layeredBefore);
   window.AppWindow.MoveAndResize(baseline);
   WindowPlumbing.SetNotificationShadow(window,appearance.Shadow);
  }
 }
}
