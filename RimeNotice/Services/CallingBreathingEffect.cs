using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
namespace RimeNotice.Services;
// Only a dedicated accent or per-dialog brush is animated. Native dialog state animations remain intact.
internal sealed class CallingBreathingEffect : IDisposable {
 private Storyboard? _story;private Action? _restore;
 private static bool Enabled=>new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled&&!new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
 internal void StartMaterial(Border accent,int bpm){
  Stop();if(!Enabled)return;accent.Visibility=Visibility.Visible;
  _restore=()=>{accent.Opacity=0;accent.Visibility=Visibility.Collapsed;};Animate(accent,bpm);
 }
 internal void StartNative(ContentDialog dialog,int bpm){
  Stop();if(!Enabled)return;
  object original=dialog.ReadLocalValue(Control.BorderBrushProperty);
  var brush=new SolidColorBrush(Microsoft.UI.Colors.Orange){Opacity=.12};
  _restore=()=>{if(ReferenceEquals(original,DependencyProperty.UnsetValue))dialog.ClearValue(Control.BorderBrushProperty);else dialog.SetValue(Control.BorderBrushProperty,original);};
  dialog.BorderBrush=brush;Animate(brush,bpm);
 }
 private void Animate(DependencyObject target,int bpm){
  try{
   var animation=new DoubleAnimation{From=.12,To=.65,Duration=new Duration(TimeSpan.FromSeconds(30d/Math.Clamp(bpm,10,120))),AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EnableDependentAnimation=true,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}};
   Storyboard.SetTarget(animation,target);Storyboard.SetTargetProperty(animation,"Opacity");_story=new Storyboard();_story.Children.Add(animation);_story.Begin();
  }catch{Stop();throw;}
 }
 internal void Stop(){_story?.Stop();_story=null;var restore=_restore;_restore=null;restore?.Invoke();}
 public void Dispose()=>Stop();
}
