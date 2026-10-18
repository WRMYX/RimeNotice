using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;
namespace RimeNotice.Services;
public sealed class NotificationAnimation : IDisposable {
 private readonly FrameworkElement _surface;private readonly Visual _visual;private bool _disposed;private readonly bool _translate;
 public NotificationAnimation(FrameworkElement surface,bool translate=true){_translate=translate;_surface=surface;ElementCompositionPreview.SetIsTranslationEnabled(surface,true);_visual=ElementCompositionPreview.GetElementVisual(surface);}
 public void Prepare(bool enabled){_visual.Opacity=0;}
 public Task ShowAsync(bool enabled,CancellationToken token)=>AnimateAsync(true,enabled,token);
 public Task HideAsync(bool enabled,CancellationToken token)=>AnimateAsync(false,enabled,token);
 private async Task AnimateAsync(bool show,bool enabled,CancellationToken token){
  if(_disposed)return;
  if(!enabled||!new UISettings().AnimationsEnabled){_visual.Opacity=show?1:0;return;}
  var compositor=_visual.Compositor;using var batch=compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
  // Fluent motion: decelerate in (fast start, soft stop), accelerate out.
  var ease=show?compositor.CreateCubicBezierEasingFunction(new Vector2(.1f,.9f),new Vector2(.2f,1f)):compositor.CreateCubicBezierEasingFunction(new Vector2(.7f,0f),new Vector2(1f,.5f));
  using var opacity=compositor.CreateScalarKeyFrameAnimation();opacity.InsertKeyFrame(0,show?0:1);opacity.InsertKeyFrame(1,show?1:0,ease);opacity.Duration=TimeSpan.FromMilliseconds(show?240:140);
  using var translation=compositor.CreateVector3KeyFrameAnimation();translation.InsertKeyFrame(0,new Vector3(0,show&&_translate?12:0,0));translation.InsertKeyFrame(1,new Vector3(0,show||!_translate?0:12,0),ease);translation.Duration=opacity.Duration;
  var complete=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);batch.Completed+=(_,_)=>complete.TrySetResult();
  _visual.StartAnimation("Opacity",opacity);_visual.StartAnimation("Translation",translation);batch.End();
  try{await complete.Task.WaitAsync(TimeSpan.FromSeconds(1),token);}catch(TimeoutException){_visual.Opacity=show?1:0;}
  finally{_visual.StopAnimation("Opacity");_visual.StopAnimation("Translation");_visual.Properties.InsertVector3("Translation",Vector3.Zero);_visual.Opacity=show?1:0;}
 }
 public void BeginBreathing(bool enabled,int bpm){if(_disposed||!enabled||!new UISettings().AnimationsEnabled)return;using var pulse=_visual.Compositor.CreateScalarKeyFrameAnimation();pulse.InsertKeyFrame(0,1);pulse.InsertKeyFrame(.5f,.82f);pulse.InsertKeyFrame(1,1);pulse.Duration=TimeSpan.FromSeconds(60d/bpm);pulse.IterationBehavior=AnimationIterationBehavior.Forever;_visual.StartAnimation("Opacity",pulse);}
 public void Dispose(){if(_disposed)return;_disposed=true;_visual.StopAnimation("Opacity");_visual.StopAnimation("Translation");}
}
