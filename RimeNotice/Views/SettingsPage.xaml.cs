using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RimeNotice.Models;
using RimeNotice.Services;
namespace RimeNotice.Views;
public sealed partial class SettingsPage : SettingsFormPage {
 private bool _startupUpdating,_ready;
 private readonly ToggleSwitch _startup=new(){OnContent="",OffContent="",MinHeight=36};
 private readonly TextBlock _startupDescription=new(){TextWrapping=TextWrapping.Wrap};
 public SettingsPage(){InitializeComponent();
  Section("启动");Row("开机自启动",_startup,"登录 Windows 后在托盘中接收消息。");Rows.Children.Add(_startupDescription);
  Button("打开 Windows 启动应用设置",()=>WindowsSettingsLauncher.OpenAsync("startupapps"));
  Section("应用外观");
  ComboBox? theme=null;theme=Choice("应用主题",["跟随系统","浅色","深色"],Current.Theme,i=>{if(theme is not null)theme.IsDropDownOpen=false;Save(s=>s with{Theme=i});App.Instance.Main?.ApplyAppTheme(Current.Theme);});
  Choice("设置窗口材质",["Mica","Mica Alt","亚克力","纯色"],(int)Current.Appearance.SettingsMaterial,i=>Save(s=>s with{Appearance=s.Appearance with{SettingsMaterial=(WindowMaterial)i}}));
  Section("托盘菜单");Toggle("触摸友好托盘菜单",Current.Appearance.TouchFriendlyMenus,v=>Save(s=>s with{Appearance=s.Appearance with{TouchFriendlyMenus=v}}));
  _startup.Toggled+=async(_,_)=>{if(!_ready||_startupUpdating)return;_startupUpdating=true;_startup.IsEnabled=false;try{ApplyStartup(await StartupService.SetEnabledAsync(_startup.IsOn));}catch(Exception ex){Error(ex);}finally{_startupUpdating=false;}};
  Loaded+=async(_,_)=>{_ready=false;_startupUpdating=true;_startup.IsEnabled=false;try{ApplyStartup(await StartupService.GetStatusAsync());}catch(Exception ex){Error(ex);}finally{_startupUpdating=false;_ready=true;}};
 }
 private void ApplyStartup(StartupStatus status){_startup.IsOn=status.Enabled;_startup.IsEnabled=!status.Blocked;_startupDescription.Text=status.Message;}
}
