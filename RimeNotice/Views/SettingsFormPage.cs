using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RimeNotice.Models;
using RimeNotice.Services;
namespace RimeNotice.Views;
public class SettingsFormPage : Page {
 protected readonly StackPanel Rows=new(){Spacing=8,Padding=new Thickness(0,0,0,32)};
 private readonly InfoBar _error=new(){IsOpen=false,IsClosable=false,Severity=InfoBarSeverity.Error,Title="操作未完成",Visibility=Visibility.Collapsed};
 protected RuleSettings Current=>SettingsStore.Shared.Current;
 public SettingsFormPage(){Content=new ScrollViewer{Content=Rows,HorizontalContentAlignment=HorizontalAlignment.Stretch,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Rows.Children.Add(_error);}
 protected void Section(string text)=>Rows.Children.Add(new TextBlock{Text=text,Style=(Style)Application.Current.Resources["BodyStrongTextBlockStyle"],FontSize=16,Margin=new Thickness(0,Rows.Children.Count==1?0:16,0,4)});
 private static TextBlock SecondaryText(string text,bool caption=false){
  var block=(TextBlock)Microsoft.UI.Xaml.Markup.XamlReader.Load("<TextBlock xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TextWrapping='Wrap' Foreground='{ThemeResource TextFillColorSecondaryBrush}'/>");
  block.Text=text;
  if(caption)block.Style=(Style)Application.Current.Resources["CaptionTextBlockStyle"];
  return block;
 }
 protected void Note(string text)=>Rows.Children.Add(SecondaryText(text));
 protected void Save(Func<RuleSettings,RuleSettings> update){try{SettingsStore.Shared.Save(update(Current));_error.IsOpen=false;_error.Visibility=Visibility.Collapsed;}catch(Exception ex){Error(ex);}}
 protected void Error(Exception ex){_error.Message="请检查文件或系统组件；详细错误已记录在调试页。";_error.Visibility=Visibility.Visible;_error.IsOpen=true;ServiceDiagnostics.Error("设置操作",ex);}
 protected void Row(string title,FrameworkElement editor,string description=""){
  var grid=new Grid{Tag="SettingRow",Padding=new Thickness(16,12,16,12),MinHeight=60,ColumnSpacing=12,RowSpacing=0};
  grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var text=new StackPanel{Spacing=4,VerticalAlignment=VerticalAlignment.Center};text.Children.Add(new TextBlock{Text=title,FontSize=14,TextWrapping=TextWrapping.Wrap});
  if(description.Length>0)text.Children.Add(SecondaryText(description,true));
  grid.Children.Add(text);editor.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(editor,1);grid.Children.Add(editor);
  Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(editor,title);
  grid.SizeChanged+=(_,_)=>SettingsRowLayout.Apply(grid);
  Rows.Children.Add(new Border{Style=(Style)Application.Current.Resources["SettingCardStyle"],Child=grid});
 }
 protected void Toggle(string title,bool value,Action<bool> change,string description=""){var c=new ToggleSwitch{IsOn=value,OnContent="",OffContent="",MinHeight=36};c.Toggled+=(_,_)=>change(c.IsOn);Row(title,c,description);}
 protected void Number(string title,double value,double min,double max,Action<double> change){var c=new NumberBox{Value=value,Minimum=min,Maximum=max,Width=140,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact,MinHeight=36};c.ValueChanged+=(_,_)=>{if(double.IsFinite(c.Value))change(c.Value);};Row(title,c);}
 protected void Choice(string title,string[] labels,int index,Action<int> change){var c=new ComboBox{Width=180,MinHeight=36,SelectedIndex=-1};foreach(string label in labels)c.Items.Add(label);c.SelectedIndex=index;c.SelectionChanged+=(_,_)=>{if(c.SelectedIndex>=0)change(c.SelectedIndex);};Row(title,c);}
 protected void Text(string title,string value,Action<string> change){var c=new TextBox{Text=value,Width=260,MinHeight=36};c.LostFocus+=(_,_)=>{try{change(c.Text);}catch(Exception ex){Error(ex);}};Row(title,c,"编辑后离开输入框保存。");}
 protected void Button(string title,Func<Task> click){var button=new Button{Content=title,MinHeight=36,HorizontalAlignment=HorizontalAlignment.Left};button.Click+=async(_,_)=>{button.IsEnabled=false;try{await click();}catch(Exception ex){Error(ex);}finally{button.IsEnabled=true;}};Rows.Children.Add(button);}
 protected void File(string title,string value,Action<string> change,string[] extensions,string emptyLabel="使用内置资源"){var panel=new Grid{Width=260,ColumnSpacing=8};panel.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});panel.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});panel.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var name=new TextBlock{Text=string.IsNullOrWhiteSpace(value)?emptyLabel:System.IO.Path.GetFileName(value),MaxWidth=180,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};var pick=new Button{Content="选择",MinHeight=36};var clear=new Button{Content="恢复",MinHeight=36};Grid.SetColumn(pick,1);Grid.SetColumn(clear,2);panel.Children.Add(name);panel.Children.Add(pick);panel.Children.Add(clear);
  pick.Click+=async(_,_)=>{try{var picker=new global::Windows.Storage.Pickers.FileOpenPicker();foreach(var ext in extensions)picker.FileTypeFilter.Add(ext);WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.Main!));var file=await picker.PickSingleFileAsync();if(file is null)return;change(file.Path);name.Text=file.Name;}catch(Exception ex){Error(ex);}};
  clear.Click+=(_,_)=>{change("");name.Text=emptyLabel;};Row(title,panel);
 }
}
