using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RimeNotice.Services;
namespace RimeNotice.Windows;

// Both presentations share the existing reception, content, audio and queue lifecycle.
// The native presentation changes only the container to a real ContentDialog.
public sealed class NativeNotificationWindow : INotificationWindow
{
 private readonly NotificationWindow _window;
 public NativeNotificationWindow(IncomingNotice notice,ElementTheme theme) => _window=new NotificationWindow(notice,theme,true);
 public Task Completion=>_window.Completion;
 public void ShowNotice(Func<IncomingNotice?>? takeNext=null)=>_window.ShowNotice(takeNext);
 public void SetPending(IncomingNotice[] pending,int count)=>_window.SetPending(pending,count);
 public void Close()=>_window.Close();
 internal static ContentDialog CreateDialog(FrameworkElement content,FrameworkElement title) => new()
 {
  Title=title,Content=content,PrimaryButtonText="收到",CloseButtonText="关闭",DefaultButton=ContentDialogButton.None,
  HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,
  MinWidth=0,MinHeight=0,MaxWidth=2000,MaxHeight=2000
 };
 internal static FrameworkElement? FindPart(DependencyObject root,string name)
 {
  if(root is FrameworkElement element&&element.Name==name)return element;
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(FindPart(VisualTreeHelper.GetChild(root,i),name) is { } found)return found;
  return null;
 }
}
