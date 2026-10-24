using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RimeNotice.Models;
using Windows.System;
namespace RimeNotice.Controls;
public sealed class AutoScrollController : IDisposable {
 private readonly global::Windows.UI.ViewManagement.UISettings _uiSettings=new();
 private readonly ScrollViewer _view;private readonly Stopwatch _clock=new();private readonly AutoScrollState _state=new();
 private readonly PointerEventHandler _pressed,_released,_wheel;private readonly KeyEventHandler _key;
 private bool _running,_applying,_dragging,_manipulating;private double _target=double.NaN;
 public double Speed {get;set;}=24;public double ResumeSeconds {get;set;}=3;public bool Enabled {get;set;}=true;
 public bool IsUserReading=>_state.Paused(_clock.Elapsed.TotalSeconds);
 public bool FirstPassCompleted=>_state.FirstPassCompleted;
 public double FirstPassSeconds=>_view.ScrollableHeight>2?4+_view.ScrollableHeight/Speed:0;
 public AutoScrollController(ScrollViewer view){
  _view=view;
  _pressed=(_,_)=>{_dragging=true;PauseForUser();};
  _released=(_,_)=>{_dragging=false;PauseForUser();};
  _wheel=(_,_)=>PauseForUser();
  _key=(_,e)=>{if(e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.PageUp or VirtualKey.PageDown or VirtualKey.Home or VirtualKey.End or VirtualKey.Space)PauseForUser();};
  view.AddHandler(UIElement.PointerPressedEvent,_pressed,true);view.AddHandler(UIElement.PointerReleasedEvent,_released,true);
  view.AddHandler(UIElement.PointerCanceledEvent,_released,true);view.AddHandler(UIElement.PointerCaptureLostEvent,_released,true);
  view.AddHandler(UIElement.PointerWheelChangedEvent,_wheel,true);view.AddHandler(UIElement.KeyDownEvent,_key,true);
  view.DirectManipulationStarted+=ManipulationStarted;view.DirectManipulationCompleted+=ManipulationCompleted;
  view.ViewChanging+=Changing;view.ViewChanged+=Changed;
 }
 public void Start(bool reset=true){if(reset){_clock.Restart();_state.Reset(0);_applying=true;_view.ChangeView(null,0,null,true);_applying=false;_target=0;}if(_running)return;_running=true;CompositionTarget.Rendering+=Frame;}
 public void RestoreOffset(double offset){
  double value=Math.Clamp(offset,0,_view.ScrollableHeight);if(Math.Abs(value-_view.VerticalOffset)<.5)return;
  _target=value;_applying=true;try{_view.ChangeView(null,value,null,true);}finally{_applying=false;}
 }
 public void PauseForUser(){_target=double.NaN;_state.User(_clock.Elapsed.TotalSeconds,ResumeSeconds,_dragging||_manipulating);}
 private void ManipulationStarted(object? s,object e){_manipulating=true;PauseForUser();}
 private void ManipulationCompleted(object? s,object e){_manipulating=false;PauseForUser();}
 private void Changing(object? s,ScrollViewerViewChangingEventArgs e){
  if(_applying)return;
  if(_state.Manual||e.IsInertial||double.IsNaN(_target)||Math.Abs(e.NextView.VerticalOffset-_target)>2)PauseForUser();
 }
 private void Changed(object? s,ScrollViewerViewChangedEventArgs e){if(_state.Manual&&!_applying)PauseForUser();}
 private void Frame(object? s,object e){
  double now=_clock.Elapsed.TotalSeconds,offset=_view.VerticalOffset;
  double next=_state.Advance(now,offset,_view.ScrollableHeight,Speed,Enabled&&_uiSettings.AnimationsEnabled);
  if(Math.Abs(next-offset)<.01)return;
  _target=next;_applying=true;try{_view.ChangeView(null,next,null,true);}finally{_applying=false;}
 }
 public void Stop(){if(_running)CompositionTarget.Rendering-=Frame;_running=false;_dragging=_manipulating=false;_clock.Stop();}
 public void Dispose(){Stop();_view.RemoveHandler(UIElement.PointerPressedEvent,_pressed);_view.RemoveHandler(UIElement.PointerReleasedEvent,_released);_view.RemoveHandler(UIElement.PointerCanceledEvent,_released);_view.RemoveHandler(UIElement.PointerCaptureLostEvent,_released);_view.RemoveHandler(UIElement.PointerWheelChangedEvent,_wheel);_view.RemoveHandler(UIElement.KeyDownEvent,_key);_view.DirectManipulationStarted-=ManipulationStarted;_view.DirectManipulationCompleted-=ManipulationCompleted;_view.ViewChanging-=Changing;_view.ViewChanged-=Changed;}
}
