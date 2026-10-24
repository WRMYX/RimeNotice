using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RimeNotice.Controls;
using RimeNotice.Models;
using RimeNotice.Services;
using Windows.Graphics;
namespace RimeNotice.Windows;
// This owned HWND displays real image content; it never carries another window's animation.
internal sealed class NotificationImageWindow : Window,IDisposable {
 private readonly Grid _root=new(){Padding=new Thickness(12),RowSpacing=8};private readonly AutoScrollController _scroll;
 private readonly TextBlock _title=new(){TextWrapping=TextWrapping.Wrap};
 private readonly NotificationImageGallery _gallery;private bool _disposing,_started,_hidden;
 public bool IsUserReading=>!_hidden&&_scroll.IsUserReading;public bool FirstPassCompleted=>_hidden||_scroll.FirstPassCompleted;
 public NotificationImageWindow(NoticeAttachmentBatch batch,ElementTheme theme,ImageDisplayOptions options,AppearanceOptions appearance){
  _root.RequestedTheme=theme;Content=_root;AppWindow.IsShownInSwitchers=false;
  if(AppWindow.Presenter is OverlappedPresenter p){p.SetBorderAndTitleBar(false,false);p.IsAlwaysOnTop=appearance.AlwaysOnTop;p.IsResizable=false;p.IsMinimizable=false;p.IsMaximizable=false;}
  WindowPlumbing.RemoveResizableFrame(this);WindowPlumbing.RemoveWindowBorder(this);WindowPlumbing.UseTransparentNativeBackground(this);
  _root.RowDefinitions.Add(new(){Height=GridLength.Auto});_root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  _title.Text=$"{batch.Items.Length} 张图片"+(batch.Inferred?" · 推测批次":"");_root.Children.Add(_title);
  var viewport=new ScrollViewer{VerticalScrollMode=ScrollMode.Enabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
  _gallery=new NotificationImageGallery(batch,viewport);viewport.Content=_gallery;Grid.SetRow(viewport,1);_root.Children.Add(viewport);
  _scroll=new AutoScrollController(viewport){Enabled=options.AutoScroll,Speed=options.ScrollSpeed,ResumeSeconds=options.ResumeSeconds};
  WindowMaterialService.ApplyNotificationNow(this,_root,appearance.NotificationMaterial,appearance.SolidColor);
  AppWindow.Closing+=(_,e)=>{if(!_disposing){e.Cancel=true;_hidden=true;AppWindow.Hide();_scroll.Stop();}};
 }
 public void ApplyBatch(NoticeAttachmentBatch batch){if(_disposing)return;_gallery.ApplyBatch(batch);_title.Text=$"{batch.Items.Length} 张图片"+(batch.Inferred?" · 推测批次":"")+(batch.IsWaiting?" · 收集中…":"");}
 public void ShowBeside(Window owner,MotionBounds bounds){if(_hidden)return;WindowPlumbing.OwnNotification(this,owner);AppWindow.MoveAndResize(new RectInt32(bounds.X,bounds.Y,bounds.Width,bounds.Height));AppWindow.Show(false);if(!_started){_started=true;_scroll.Start();}}
 public void Dispose(){if(_disposing)return;_disposing=true;_scroll.Dispose();_gallery.Dispose();Close();}
}
