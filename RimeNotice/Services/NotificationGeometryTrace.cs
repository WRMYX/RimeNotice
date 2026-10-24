using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace RimeNotice.Services;

// Reads bounds and native states only; never updates layout, states or visuals.
public sealed class NotificationGeometryTrace
{
    private readonly HashSet<VisualStateGroup> _observed = new();
    public void Record(Window window, FrameworkElement root, FrameworkElement dialog, string key, string phase)
    {
        if (!DiagnosticCapture.Active) return;
        try
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            bool windowOk = GetWindowRect(hwnd, out var outer);
            bool clientOk = GetClientRect(hwnd, out var client);
            var origin = new NativePoint();
            bool originOk = ClientToScreen(hwnd, ref origin);
            int dwmResult = DwmGetWindowAttribute(hwnd, 9, out var frame, Marshal.SizeOf<NativeRect>());
            int ncResult = GetDwmInt(hwnd,1,out int ncEnabled,sizeof(int));
            object Bounds(FrameworkElement element)
            {
                try
                {
                    Rect bounds = element.TransformToVisual(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                    return new { element.Name, element.ActualWidth, element.ActualHeight, desiredWidth=element.DesiredSize.Width,desiredHeight=element.DesiredSize.Height,maxHeight=double.IsFinite(element.MaxHeight)?(double?)element.MaxHeight:null,shadow=element.Shadow?.GetType().Name,rootDip = new { bounds.X, bounds.Y, bounds.Width, bounds.Height } };
                }
                catch (Exception ex) { return new { error = ex.GetType().Name, element.ActualWidth, element.ActualHeight }; }
            }
            FrameworkElement? background = null;
            var parts=new Dictionary<string,object>();
            void Visit(DependencyObject node)
            {
                if (node is FrameworkElement element)
                {
                    if (element.Name == "BackgroundElement") background = element;
                    if(element.Name is "Content" or "Title" or "CommandSpace" or "NoticeContent" or "NoticeFooter" or "MessageViewport" or "MessageBody")parts[element.Name]=Bounds(element);
                    if(element is ScrollViewer scroll&&element.Name=="MessageViewport")parts["messageScroll"]=new{scroll.ViewportHeight,scroll.ExtentHeight,scroll.VerticalOffset};
                    foreach (var group in VisualStateManager.GetVisualStateGroups(element))
                    {
                        if (group.Name != "DialogShowingStates" || !_observed.Add(group)) continue;
                        var owner = element;
                        group.CurrentStateChanging += (_, args) => State("changing", args);
                        group.CurrentStateChanged += (_, args) => State("changed", args);
                        void State(string stage, VisualStateChangedEventArgs args)
                        {
                            if (DiagnosticCapture.Active) DiagnosticCapture.Write("window.native-state", new { key, hwnd = hwnd.ToInt64(), group = group.Name, owner = owner.Name, stage, oldState = args.OldState?.Name, newState = args.NewState?.Name });
                        }
                        DiagnosticCapture.Write("window.native-state-attached", new { key, hwnd = hwnd.ToInt64(), group = group.Name, state = group.CurrentState?.Name, owner = element.Name });
                    }
                }
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Visit(VisualTreeHelper.GetChild(node, i));
            }
            Visit(dialog);
            background ??= dialog; // Whole-window cards have no ContentDialog template border.
            DiagnosticCapture.Write("window.geometry", new { key, phase, hwnd = hwnd.ToInt64(), units = "HWND/client/DWM=physical-px; XAML=root-DIP", windowOk, outer, clientOk, client, originOk, origin, dwmResult, frame,ncResult,ncEnabled,parts,scale = root.XamlRoot?.RasterizationScale, dialog = Bounds(dialog), background = background is null ? null : Bounds(background), backdrop = window.SystemBackdrop?.GetType().Name ?? "Solid", appPosition = new { window.AppWindow.Position.X, window.AppWindow.Position.Y }, appSize = new { window.AppWindow.Size.Width, window.AppWindow.Size.Height } });
        }
        catch (Exception ex) { DiagnosticCapture.Write("window.trace-error", new { key, phase, error = ex.GetType().Name }); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]private static extern int GetDwmInt(IntPtr window,int attribute,out int value,int size);
}
