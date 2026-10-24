using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using RimeNotice.Models;
using RimeNotice.Services;
namespace RimeNotice.Controls;
// Fixed slots retain the complete album while only nearby slots own decoded bitmaps.
public sealed class NotificationImageGallery : Grid,IDisposable {
 private readonly List<NoticeAttachment> _items=[];private readonly List<(Button Button,Image Image)> _tiles=[];
 private readonly ScrollViewer _viewport;private readonly CancellationTokenSource _stop=new();
 private readonly DispatcherTimer _refresh=new(){Interval=TimeSpan.FromMilliseconds(180)};
 private readonly TextBlock _error=new(){TextWrapping=TextWrapping.Wrap,FontSize=12};
 private readonly Grid _pictures=new(){ColumnSpacing=8,RowSpacing=8};private bool _disposed,_loading;private int _columns;
 private readonly HashSet<int> _failed=[];
 public NotificationImageGallery(NoticeAttachmentBatch batch,ScrollViewer viewport){
  _viewport=viewport;
  RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=GridLength.Auto});
  Children.Add(_pictures);Grid.SetRow(_error,1);Children.Add(_error);
  ApplyBatch(batch);
  SizeChanged+=(_,_)=>ArrangeTiles();Loaded+=(_,_)=>{ArrangeTiles();_refresh.Start();};Unloaded+=(_,_)=>_refresh.Stop();
  _refresh.Tick+=async(_,_)=>await RefreshAsync();
 }
 public void ApplyBatch(NoticeAttachmentBatch batch){
  if(_disposed)return;
  foreach(var item in batch.Items.Where(a=>a.IsImage)){
   string identity=NotificationAttachmentResolver.FileIdentity(item.Path);
   int index=_items.FindIndex(a=>NotificationAttachmentResolver.FileIdentity(a.Path).Equals(identity,StringComparison.OrdinalIgnoreCase));
   if(index>=0){if(!_items[index].Path.Equals(item.Path,StringComparison.OrdinalIgnoreCase)){
     _items[index]=item;_tiles[index].Image.Source=null;_failed.Remove(index);
     Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_tiles[index].Button,"打开图片："+Path.GetFileName(item.Path));
     ((HyperlinkButton)((StackPanel)_pictures.Children[index]).Children[1]).Content="打开附件："+Path.GetFileName(item.Path);
    }continue;}
   index=_items.Count;_items.Add(item);int captured=index;
   var image=new Image{Stretch=Stretch.Uniform};var button=new Button{Content=image,Padding=new Thickness(4),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Stretch,Height=210};
   Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,"打开图片："+Path.GetFileName(item.Path));
   button.Click+=async(_,_)=>await OpenAsync(_items[captured].Path);_tiles.Add((button,image));var slot=new StackPanel{Spacing=4};slot.Children.Add(button);
   var link=new HyperlinkButton{Content="打开附件："+Path.GetFileName(item.Path),MaxWidth=300};link.Click+=async(_,_)=>await OpenAsync(_items[captured].Path);slot.Children.Add(link);_pictures.Children.Add(slot);
  }
  ArrangeTiles(true);
 }
 private void ArrangeTiles(bool force=false){
  int columns=ActualWidth>=440?2:1;if(columns==_columns&&!force)return;_columns=columns;
  _pictures.ColumnDefinitions.Clear();_pictures.RowDefinitions.Clear();for(int i=0;i<columns;i++)_pictures.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
  for(int i=0;i<(_tiles.Count+columns-1)/columns;i++)_pictures.RowDefinitions.Add(new(){Height=GridLength.Auto});
  for(int i=0;i<_tiles.Count;i++){Grid.SetColumn((FrameworkElement)_pictures.Children[i],i%columns);Grid.SetRow((FrameworkElement)_pictures.Children[i],i/columns);}
 }
 private async Task RefreshAsync(){
  if(_disposed||_loading||XamlRoot is null||_viewport.ViewportHeight<=0)return;_loading=true;
  try{
   var visible=new List<int>();
   for(int i=0;i<_tiles.Count;i++){
    var button=_tiles[i].Button;if(button.ActualHeight<=0)continue;
    double y=button.TransformToVisual(_viewport).TransformPoint(new global::Windows.Foundation.Point()).Y;
    if(y+button.ActualHeight>=-240&&y<=_viewport.ViewportHeight+240)visible.Add(i);
    else _tiles[i].Image.Source=null;
   }
   var nearby=visible.Take(12).ToHashSet();for(int i=0;i<_tiles.Count;i++)if(!nearby.Contains(i))_tiles[i].Image.Source=null;
   foreach(int index in nearby){
    if(_disposed||_stop.IsCancellationRequested)return;if(_tiles[index].Image.Source is not null||_failed.Contains(index))continue;
    string loadingPath=_items[index].Path;
    try{
     using var stream=await(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(loadingPath)).OpenReadAsync();
     var bitmap=new BitmapImage{DecodePixelWidth=600,DecodePixelHeight=420};await bitmap.SetSourceAsync(stream);
     if(_disposed)return;if(_items[index].Path.Equals(loadingPath,StringComparison.OrdinalIgnoreCase))_tiles[index].Image.Source=bitmap;
    }catch(Exception ex){if(_disposed)return;if(_items[index].Path.Equals(loadingPath,StringComparison.OrdinalIgnoreCase))_failed.Add(index);_error.Text="部分图片暂不可预览，可点击图片或下方链接尝试打开。";ServiceDiagnostics.Error("相册解码",ex);}
   }
  }catch(Exception ex){if(!_disposed)ServiceDiagnostics.Error("相册可见区域",ex);}finally{_loading=false;}
 }
 public async Task OpenAsync(string path){try{
  if(!File.Exists(path))throw new FileNotFoundException("图片已被移动或删除。",path);
  if(!await global::Windows.System.Launcher.LaunchFileAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(path)))throw new IOException("系统未能打开图片。");
 }catch(Exception ex){_error.Text="无法打开图片：文件可能已被移动、删除或暂不可访问。";ServiceDiagnostics.Error("打开图片",ex);}}
 public void Dispose(){if(_disposed)return;_disposed=true;_refresh.Stop();_stop.Cancel();_stop.Dispose();foreach(var tile in _tiles)tile.Image.Source=null;}
}
