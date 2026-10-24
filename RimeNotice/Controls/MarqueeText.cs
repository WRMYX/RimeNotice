using System;
using System.Numerics;
using System.Diagnostics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;
namespace RimeNotice.Controls;
public sealed class MarqueeText : Grid,IDisposable {
 private readonly TextBlock _text;private readonly ScrollViewer _scroll;private readonly bool _vertical;
 private readonly DispatcherTimer _resume=new(){Interval=TimeSpan.FromSeconds(2)};
 private readonly Stopwatch _scrollClock=new(); private bool _rendering,_paused; private double _startOffset,_lastFrame;
 private Visual? _visual;private bool _disposed,_queued,_pressed;private double _extent,_viewport;private float _distance;
 private bool _externalViewport;
 public bool ExternalViewport {get=>_externalViewport;set{
  if(_externalViewport==value)return;_externalViewport=value;if(!_vertical)return;
  Stop();Children.Clear();_scroll.Content=null;
  if(value)Children.Add(_text);else{_scroll.Content=_text;Children.Add(_scroll);}
 }}
 public double NaturalWidth {get{var probe=new TextBlock{Text=_text.Text,FontSize=_text.FontSize,FontFamily=_text.FontFamily,FontWeight=_text.FontWeight};probe.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity,double.PositiveInfinity));return probe.DesiredSize.Width;}}
 public double FirstPassSeconds=>_vertical&&_extent>_viewport+2?3+(_extent-_viewport)/24:0;
 public MarqueeText(string text,double font,string family,bool vertical=false){_vertical=vertical;
  _text=new TextBlock{Text=text,FontSize=font,FontFamily=CreateFont(family),TextWrapping=vertical?TextWrapping.Wrap:TextWrapping.NoWrap,IsTextSelectionEnabled=true,VerticalAlignment=VerticalAlignment.Top};
  if(!vertical)_text.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;
  _scroll=new ScrollViewer{Content=_text,HorizontalContentAlignment=vertical?HorizontalAlignment.Stretch:HorizontalAlignment.Left,VerticalContentAlignment=VerticalAlignment.Top,HorizontalScrollMode=vertical?ScrollMode.Disabled:ScrollMode.Enabled,HorizontalScrollBarVisibility=vertical?ScrollBarVisibility.Disabled:ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=vertical?ScrollBarVisibility.Hidden:ScrollBarVisibility.Disabled};Children.Add(_scroll);
  SetAccessibility(text);Loaded+=(_,_)=>Schedule();SizeChanged+=(_,_)=>Schedule();Unloaded+=(_,_)=>{_resume.Stop();Stop();};
  // Merely appearing under the pointer must not pause the first reading pass.
  PointerPressed+=(_,_)=>{_pressed=true;Pause();};PointerReleased+=(_,_)=>{_pressed=false;DelayResume();};PointerExited+=(_,_)=>{_pressed=false;Resume();};
  PointerWheelChanged+=(_,_)=>{Pause();DelayResume();};_resume.Tick+=(_,_)=>{_resume.Stop();if(!_pressed)Resume();};
 }
 private static FontFamily CreateFont(string family){try{return new FontFamily(family);}catch{return new FontFamily("Segoe UI Variable");}}
 private void SetAccessibility(string text){Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this,text);ToolTipService.SetToolTip(this,_vertical?null:text);}
 public void SetTypography(double size,string family){Stop();_text.FontSize=size;_text.FontFamily=CreateFont(family);_extent=0;InvalidateMeasure();Schedule();}
 public void SetText(string text){Stop();_text.Text=text;SetAccessibility(text);_extent=0;InvalidateMeasure();Schedule();}
 public void Restart(){Stop();if(_vertical&&!ExternalViewport)_scroll.ChangeView(null,0,null,true);Schedule();}
 public void SetViewport(double height){if(_vertical){_viewport=Math.Max(0,height);Height=_viewport;Schedule();}}
 public double MeasureWrapped(double width){_text.Width=Math.Max(1,width);_text.Height=double.NaN;_text.Measure(new global::Windows.Foundation.Size(_text.Width,double.PositiveInfinity));_extent=_text.DesiredSize.Height;_text.Height=_extent;return _extent;}
 private void Schedule(){if(_disposed||_queued)return;_queued=true;DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,()=>{_queued=false;Configure();});}
 private void Configure(){if(_disposed||ActualWidth<=0||XamlRoot is null)return;
  if(_vertical){
   if(ExternalViewport){Stop();return;}
   double verticalDistance=Math.Max(0,_extent-_viewport);
   if(verticalDistance<=2||!new UISettings().AnimationsEnabled){Stop();return;}
   if(_rendering)return;
   _startOffset=_scroll.VerticalOffset;_lastFrame=0;_paused=false;_scrollClock.Restart();
   CompositionTarget.Rendering+=ScrollFrame;_rendering=true;return;
  }
  double extent=_vertical?_extent:NaturalWidth;double viewport=_vertical?_viewport:ActualWidth;
  if(!_vertical)_text.Width=Math.Max(1,extent);
  double distance=Math.Max(0,extent-viewport);
  if(!new UISettings().AnimationsEnabled||distance<=2){Stop();return;}
  if(_visual is not null&&Math.Abs(_distance-distance)<1)return;
  Stop();ElementCompositionPreview.SetIsTranslationEnabled(_text,true);_visual=ElementCompositionPreview.GetElementVisual(_text);
  var host=ElementCompositionPreview.GetElementVisual(_scroll);host.Clip=host.Compositor.CreateInsetClip();_distance=(float)distance;
  double forward=Math.Clamp(distance/(_vertical?24:28),1,110),total=forward+4;
  using var animation=_visual.Compositor.CreateVector3KeyFrameAnimation();var linear=_visual.Compositor.CreateLinearEasingFunction();var end=_vertical?new Vector3(0,-_distance,0):new Vector3(-_distance,0,0);
  animation.InsertKeyFrame(0,Vector3.Zero);animation.InsertKeyFrame((float)(2/total),Vector3.Zero);animation.InsertKeyFrame((float)((2+forward)/total),end,linear);animation.InsertKeyFrame((float)((3+forward)/total),end);animation.InsertKeyFrame(1,Vector3.Zero,linear);
  animation.Duration=TimeSpan.FromSeconds(total);animation.IterationBehavior=AnimationIterationBehavior.Forever;_visual.StartAnimation("Translation",animation);if(_pressed)Pause();
 }
 private void ScrollFrame(object? sender,object args){
  if(_disposed||_paused||_pressed)return;
  double seconds=_scrollClock.Elapsed.TotalSeconds;
  if(seconds-_lastFrame<1d/60)return;_lastFrame=seconds;
  double travel=Math.Max(0,seconds-2)*24;
  double end=Math.Max(0,_extent-_viewport);
  // ChangeView scrolls actual native content rather than translating already-clipped text.
  _scroll.ChangeView(null,Math.Min(end,_startOffset+travel),null,true);
  if(travel>=Math.Max(0,end-_startOffset)+48){_startOffset=0;_scrollClock.Restart();_lastFrame=0;_scroll.ChangeView(null,0,null,true);}
 }
 private void Pause(){_paused=true;_scrollClock.Stop();_visual?.TryGetAnimationController("Translation")?.Pause();}
 private void DelayResume(){_resume.Stop();_resume.Start();}
 private void Resume(){_resume.Stop();if(_vertical){_startOffset=_scroll.VerticalOffset;_lastFrame=0;_scrollClock.Restart();_paused=false;}_visual?.TryGetAnimationController("Translation")?.Resume();}
 private void Stop(){if(_rendering){CompositionTarget.Rendering-=ScrollFrame;_rendering=false;}_scrollClock.Stop();if(_visual is null)return;_visual.StopAnimation("Translation");_visual.Properties.InsertVector3("Translation",Vector3.Zero);_visual=null;}
 public void Dispose(){if(_disposed)return;_disposed=true;_resume.Stop();Stop();}
}
