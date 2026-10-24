using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RimeNotice.Models;
using Windows.UI.ViewManagement;
namespace RimeNotice.Services;
public static class WindowMaterialService {
 private static readonly ConditionalWeakTable<Window,MaterialState> States=new();
 public static void Apply(Window window,Panel root,WindowMaterial material,string color="") {
  if(!window.DispatcherQueue.HasThreadAccess){window.DispatcherQueue.TryEnqueue(()=>Apply(window,root,material,color));return;}
  var state=States.GetValue(window,w=>new MaterialState(w,root));state.Material=material;state.Color=color;state.Request();
 }
 // Notification first-frame preparation runs on its UI thread before uncloaking.
 public static void ApplyNotificationNow(Window window,Panel root,WindowMaterial material,string color="") {
  if(!window.DispatcherQueue.HasThreadAccess)throw new InvalidOperationException("通知材质必须在所属 UI 线程初始化。");
  var state=States.GetValue(window,w=>new MaterialState(w,root));state.Material=material;state.Color=color;state.ApplyNow();
 }
 private sealed class MaterialState {
  private readonly Window _window;private readonly Panel _root;private bool _pending,_closed;
  public WindowMaterial Material;public string Color="";
  public MaterialState(Window window,Panel root){_window=window;_root=root;root.ActualThemeChanged+=ThemeChanged;window.Closed+=(_,_)=>{_closed=true;root.ActualThemeChanged-=ThemeChanged;};}
  private void ThemeChanged(FrameworkElement sender,object args)=>Request();
  public void Request(){if(_closed||_pending)return;_pending=true;if(!_window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,Update))_pending=false;}
  public void ApplyNow(){if(!_closed)Update();}
  private void Update(){
   _pending=false;if(_closed)return;
   var window=_window;var root=_root;var material=Material;var color=Color;
  try {
   bool transparent=new UISettings().AdvancedEffectsEnabled && !new AccessibilitySettings().HighContrast;
   transparent &= material switch {WindowMaterial.Mica or WindowMaterial.MicaAlt=>Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported(),WindowMaterial.Acrylic=>Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported(),_=>false};
   var current=window.SystemBackdrop;
   bool matches=!transparent?current is null:material switch {
    WindowMaterial.Mica=>current is MicaBackdrop m && m.Kind==Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base,
    WindowMaterial.MicaAlt=>current is MicaBackdrop m && m.Kind==Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
    WindowMaterial.Acrylic=>current is DesktopAcrylicBackdrop,_=>false};
   // Native backdrops track theme changes themselves. Reuse the instance on theme-only updates.
   if(!matches)window.SystemBackdrop=transparent ? material switch {
    WindowMaterial.Mica=>new MicaBackdrop(),WindowMaterial.MicaAlt=>new MicaBackdrop {Kind=Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt},
    WindowMaterial.Acrylic=>new DesktopAcrylicBackdrop(),_=>null
   }:null;
   root.Background=window.SystemBackdrop is null ? Solid(root,color) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
   ServiceDiagnostics.Set(window is MainWindow?"设置窗口材质":"通知材质",window.SystemBackdrop is null?"纯色（所选或系统回退）":material.ToString());
  } catch(Exception ex) {CrashLog.Write("材质更新",ex);try{window.SystemBackdrop=null;root.Background=Solid(root,color);}catch(Exception fallback){CrashLog.Write("材质回退",fallback);}}
  }
 }
 private static Brush Solid(FrameworkElement root,string color) {
  if(new AccessibilitySettings().HighContrast)return new SolidColorBrush(new UISettings().GetColorValue(UIColorType.Background));
  if(color.Length==9 && color[0]=='#' && uint.TryParse(color.AsSpan(1),System.Globalization.NumberStyles.HexNumber,null,out uint c))
   return new SolidColorBrush(global::Windows.UI.Color.FromArgb(255,(byte)(c>>16),(byte)(c>>8),(byte)c));
  var theme=root.RequestedTheme==ElementTheme.Default?root.ActualTheme:root.RequestedTheme;
  return new SolidColorBrush(theme==ElementTheme.Dark?global::Windows.UI.Color.FromArgb(255,32,32,32):global::Windows.UI.Color.FromArgb(255,243,243,243));
 }
}
