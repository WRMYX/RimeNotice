using System;
using System.Numerics;
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
 private Visual? _visual;private bool _disposed,_queued,_pressed;private double _extent,_viewport;private float _distance;
 public double NaturalWidth {get{var probe=new TextBlock{Text=_text.Text,FontSize=_text.FontSize,FontFamily=_text.FontFamily,FontWeight=_text.FontWeight};probe.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity,double.PositiveInfinity));return probe.DesiredSize.Width;}}
 public double FirstPassSeconds=>_vertical&&_extent>_viewport+2?2+Math.Min(110,(_extent-_viewport)/24):0;
 public MarqueeText(string text,double font,string family,bool vertical=false){_vertical=vertical;
  _text=new TextBlock{Text=text,FontSize=font,FontFamily=CreateFont(family),TextWrapping=vertical?TextWrapping.Wrap:TextWrapping.NoWrap,IsTextSelectionEnabled=true,VerticalAlignment=VerticalAlignment.Top};
  if(!vertical)_text.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;
  _scroll=new ScrollViewer{Content=_text,HorizontalContentAlignment=HorizontalAlignment.Left,HorizontalScrollMode=vertical?ScrollMode.Disabled:ScrollMode.Enabled,HorizontalScrollBarVisibility=vertical?ScrollBarVisibility.Disabled:ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=vertical?ScrollBarVisibility.Hidden:ScrollBarVisibility.Disabled};Children.Add(_scroll);
  SetAccessibility(text);Loaded+=(_,_)=>Schedule();SizeChanged+=(_,_)=>Schedule();Unloaded+=(_,_)=>{_resume.Stop();Stop();};
  // Merely appearing under the pointer must not pause the first reading pass.
  PointerPressed+=(_,_)=>{_pressed=true;Pause();};PointerReleased+=(_,_)=>{_pressed=false;DelayResume();};PointerExited+=(_,_)=>{_pressed=false;Resume();};
  PointerWheelChanged+=(_,_)=>{Pause();DelayResume();};_resume.Tick+=(_,_)=>{_resume.Stop();if(!_pressed)Resume();};
 }
 private static FontFamily CreateFont(string family){try{return new FontFamily(family);}catch{return new FontFamily("Segoe UI Variable");}}
 private void SetAccessibility(string text){Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this,text);ToolTipService.SetToolTip(this,_vertical?null:text);}
 public void SetText(string text){Stop();_text.Text=text;SetAccessibility(text);_extent=0;InvalidateMeasure();Schedule();}
 public void Restart(){Stop();Schedule();}
 public void SetViewport(double height){if(_vertical){_viewport=Math.Max(0,height);Height=_viewport;Schedule();}}
 public double MeasureWrapped(double width){_text.Width=Math.Max(1,width);_text.Height=double.NaN;_text.Measure(new global::Windows.Foundation.Size(_text.Width,double.PositiveInfinity));_extent=_text.DesiredSize.Height;_text.Height=_extent;return _extent;}
 private void Schedule(){if(_disposed||_queued)return;_queued=true;DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,()=>{_queued=false;Configure();});}
 private void Configure(){if(_disposed||ActualWidth<=0||XamlRoot is null)return;
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
 private void Pause()=>_visual?.TryGetAnimationController("Translation")?.Pause();
 private void DelayResume(){_resume.Stop();_resume.Start();}
 private void Resume(){_resume.Stop();_visual?.TryGetAnimationController("Translation")?.Resume();}
 private void Stop(){if(_visual is null)return;_visual.StopAnimation("Translation");_visual.Properties.InsertVector3("Translation",Vector3.Zero);_visual=null;}
 public void Dispose(){if(_disposed)return;_disposed=true;_resume.Stop();Stop();}
}
