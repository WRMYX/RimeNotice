using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
namespace RimeNotice.Services;
public static class AppIconResolver {
 private static readonly Dictionary<string,ImageSource> Cache=new(StringComparer.OrdinalIgnoreCase);
 public static void Clear()=>Cache.Clear();
 public static async Task<ImageSource> ResolveAsync(string sourceName,string appId,RandomAccessStreamReference? reference) {
  string key=string.IsNullOrWhiteSpace(appId)?sourceName:appId;
  if(reference is not null) {
   try{using var stream=await reference.OpenReadAsync();var image=new BitmapImage();await image.SetSourceAsync(stream);Cache[key]=image;
    if(Cache.Count>64){Cache.Clear();Cache[key]=image;}ServiceDiagnostics.Set("应用图标","系统图标已加载");return image;
   }catch(Exception ex){ServiceDiagnostics.Error("应用图标",ex);}
  }
  if(Cache.TryGetValue(key,out var cached))return cached;
  bool qq=sourceName.Equals("QQ",StringComparison.OrdinalIgnoreCase)||sourceName.Equals("腾讯QQ",StringComparison.OrdinalIgnoreCase);
  ServiceDiagnostics.Set("应用图标",qq?"使用包内 QQ 图标":"使用应用通用图标");
  return new BitmapImage(new Uri(qq?"ms-appx:///Assets/QQ.png":"ms-appx:///Assets/icon44.png"));
 }
}
