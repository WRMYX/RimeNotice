using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace RimeNotice.Services
{
    /// <summary>
    /// Win32 窗口管道辅助（仅窗口行为，不做任何外观绘制）：
    /// 为浮窗加上 WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW，使其置顶显示但不抢走放映焦点。
    /// </summary>
    public static class WindowPlumbing
    {
        internal static void OwnNotification(Window notification, Window mask)
        {
            IntPtr child = WinRT.Interop.WindowNative.GetWindowHandle(notification);
            IntPtr owner = WinRT.Interop.WindowNative.GetWindowHandle(mask);
            Marshal.SetLastPInvokeError(0);
            var previous = SetWindowLongPtrW(child, -8, owner);
            int error = Marshal.GetLastWin32Error();
            if (previous == IntPtr.Zero && error != 0) throw new Win32Exception(error);
        }
        internal static void PreventActivation(Window window)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            SetWindowLongW(hwnd, GWL_EXSTYLE, GetWindowLongW(hwnd, GWL_EXSTYLE) | (int)WS_EX_NOACTIVATE | (int)WS_EX_TOOLWINDOW);
        }
        [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW", SetLastError=true)]
        private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_NOACTIVATE = 0x08000000;
        private const long WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_CAPTION = 0x00C00000;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private static readonly IntPtr HwndTopmost = new(-1);
        private const long WS_EX_TRANSPARENT = 0x00000020;
        private const long WS_EX_LAYERED = 0x00080000;

        /// <summary>整窗透明度，用于只有纯色背景的引导遮罩。</summary>
        internal static void SetNotificationShadow(Window window,bool enabled)
        {
            if(enabled)return;
            IntPtr hwnd=WinRT.Interop.WindowNative.GetWindowHandle(window);
            int disabled=1;
            DwmSetWindowAttribute(hwnd,2,ref disabled,sizeof(int));
        }
        internal static void ConfigureNativeNotificationHost(Window window)
        {
            // Apply after the transparent frame is initialized, not before it can reset DWM policy.
            RemoveResizableFrame(window);UseTransparentNativeBackground(window);
            var hwnd=WinRT.Interop.WindowNative.GetWindowHandle(window);
            int disabled=1,noBorder=unchecked((int)0xFFFFFFFE),noCorners=1,allowTransitions=0;
            int ncHr=DwmSetWindowAttribute(hwnd,2,ref disabled,sizeof(int));
            int borderHr=DwmSetWindowAttribute(hwnd,34,ref noBorder,sizeof(int));
            int cornerHr=DwmSetWindowAttribute(hwnd,33,ref noCorners,sizeof(int));
            int transitionHr=DwmSetWindowAttribute(hwnd,3,ref allowTransitions,sizeof(int));
            int readHr=GetDwmInt(hwnd,1,out int ncEnabled,sizeof(int));
            ServiceDiagnostics.Set("原生父窗口边界",$"非客户区关闭：0x{ncHr:X8}；读取：0x{readHr:X8}，enabled={ncEnabled}；边框：0x{borderHr:X8}；实际阴影待验收");
            if(DiagnosticCapture.Active)DiagnosticCapture.Write("window.host-decoration",new{hwnd=hwnd.ToInt64(),ncHr,borderHr,cornerHr,transitionHr,readHr,ncEnabled});
        }
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]
        private static extern int GetDwmInt(IntPtr hwnd,int attribute,out int value,int size);
        internal static void ResetTrayHost(Window window)
        {
            // Restore layout/rendering while fully transparent before reopening a flyout.
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (SetWindowRgn(hwnd, IntPtr.Zero, true) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        internal static void ConfigureTrayHost(Window window)
        {
            // The flyout has a separate native popup. An empty owner region removes
            // the host surface and its DWM shadow without changing the flyout theme.
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            IntPtr region = CreateRectRgn(0, 0, 0, 0);
            if (region == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (SetWindowRgn(hwnd, region, true) == 0)
            { DeleteObject(region); throw new Win32Exception(Marshal.GetLastWin32Error()); }
            // Windows owns the region after a successful SetWindowRgn.
        }
        internal static void BringToForeground(Window window) => SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
        // Cloaking is used only to prepare the same real window's first frame.
        // It is not an animation and does not disable DWM transitions.
        internal static void CloakNotification(Window window, bool cloak)
        {
            int value = cloak ? 1 : 0;
            int result = DwmSetWindowAttribute(WinRT.Interop.WindowNative.GetWindowHandle(window), 13, ref value, sizeof(int));
            if (result < 0) Marshal.ThrowExceptionForHR(result);
        }
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

        internal static void SetOpacity(Window window, byte opacity)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            int style = GetWindowLongW(hwnd, GWL_EXSTYLE);
            Marshal.SetLastPInvokeError(0);
            int previous = SetWindowLongW(hwnd, GWL_EXSTYLE, (int)((uint)style | WS_EX_LAYERED));
            int error = Marshal.GetLastWin32Error();
            if (previous == 0 && error != 0) throw new Win32Exception(error);
            if (!SetLayeredWindowAttributes(hwnd, 0, opacity, 2 /* LWA_ALPHA */))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        internal static bool HasLayeredStyle(Window window)=>((uint)GetWindowLongW(WinRT.Interop.WindowNative.GetWindowHandle(window),GWL_EXSTYLE)&WS_EX_LAYERED)!=0;
        internal static void RestoreOpacity(Window window,bool originallyLayered){
            SetOpacity(window,255);
            if(!originallyLayered){IntPtr handle=WinRT.Interop.WindowNative.GetWindowHandle(window);int style=GetWindowLongW(handle,GWL_EXSTYLE);SetWindowLongW(handle,GWL_EXSTYLE,(int)((uint)style&~WS_EX_LAYERED));}
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern bool SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_NCLBUTTONDOWN = 0x00A1;
        private static readonly IntPtr HtCaption = new(2);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO info);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("dwmapi.dll")]
        private static extern void DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern void DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
        private const int DWMWCP_ROUND = 2;

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        private const int DWM_BB_ENABLE = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_BLURBEHIND
        {
            public int dwFlags;
            public int fEnable;
            public IntPtr hRgnBlur;
            public int fTransitionOnMaximized;
        }

        // WS_EX_NOACTIVATE 会使部分新建浮窗忽略触摸；通过消息拒绝激活但保留输入。


        private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

        private static readonly SubclassProc TransparentBackgroundProc = HandleTransparentBackground;
        private static readonly UIntPtr TransparentBackgroundId = new(0x52494D46);

        // XAML's transparent island does not clear the HWND's GDI redirection surface.
        // DWM glass needs zeroed pixels here; the default light-theme erase paints white.
        internal static void UseTransparentNativeBackground(Window window)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (!SetWindowSubclass(hwnd, TransparentBackgroundProc, TransparentBackgroundId, UIntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to configure transparent window background.");
            // The HWND can already have received its first erase before XAML Content is attached.
            // Clear that surface too; waiting for another erase leaves the old white pixels behind.
            var hdc = GetDC(hwnd);
            if (hdc != IntPtr.Zero)
            {
                try { if (GetClientRect(hwnd, out var bounds)) FillRect(hdc, ref bounds, GetStockObject(4)); }
                finally { ReleaseDC(hwnd, hdc); }
            }
        }

        private static IntPtr HandleTransparentBackground(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
        {
            if (message == 0x0014 && GetClientRect(hwnd, out var bounds)) // WM_ERASEBKGND
            {
                if (FillRect(unchecked((nint)(nuint)wParam), ref bounds, GetStockObject(4)) != 0)
                    return new IntPtr(1); // BLACK_BRUSH writes the zero-alpha DWM background, not a XAML theme colour.
            }
            if (message == 0x0082) RemoveWindowSubclass(hwnd, TransparentBackgroundProc, id);
            return DefSubclassProc(hwnd, message, wParam, lParam);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT rectangle);
        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr hdc, ref RECT rectangle, IntPtr brush);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr GetStockObject(int objectType);


        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id, UIntPtr data);
        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id);
        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 打开 DWM 的 alpha 合成路径，使 XAML 里的 Transparent 背景真正透视到窗口后面
        /// （WinUI 3 窗口默认无分层支持，透明背景会被渲染成黑色）。
        /// </summary>
        public static void EnableTransparency(Window window, bool enableBlur = true)
        {
            UseTransparentNativeBackground(window);
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            var blur = new DWM_BLURBEHIND
            {
                dwFlags = DWM_BB_ENABLE,
                fEnable = enableBlur ? 1 : 0,
                hRgnBlur = CreateRectRgn(0, 0, 0, 0),
            };
            try { DwmEnableBlurBehindWindow(hwnd, ref blur); }
            finally { if (blur.hRgnBlur != IntPtr.Zero) DeleteObject(blur.hRgnBlur); }
        }

        /// <summary>
        /// 剥掉 WS_THICKFRAME 不可见 resize 边框：OverlappedPresenter 即使
        /// SetBorderAndTitleBar(false,false) 后仍保留它，导致 XAML 内容比窗口
        /// 矩形内缩一圈，四周露出 DWM 玻璃白边。改完需 FRAMECHANGED 重算。
        /// </summary>
        public static void RemoveResizableFrame(Window window)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            int style = GetWindowLongW(hwnd, GWL_STYLE);
            style &= ~(WS_THICKFRAME | WS_CAPTION);
            SetWindowLongW(hwnd, GWL_STYLE, style);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        /// <summary>
        /// 去掉 Windows 11 DWM 给无边框窗口默认绘制的那圈窗口边框
        /// （浮窗矩形与卡片重合，默认边框会看起来像卡片外包了一圈白边）。
        /// 属性 34 仅 Win11+ 支持，低版本返回失败被静默忽略（Win10 无此边框）。
        /// </summary>
        public static void RemoveWindowBorder(Window window)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            int value = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref value, sizeof(int));
        }
    }
}
