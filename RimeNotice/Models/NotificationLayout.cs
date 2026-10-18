using System;
namespace RimeNotice.Models;
public readonly record struct CardSize(double Width,double Height);
public static class NotificationLayout {
 public static double Finite(double v,double fallback,double min,double max) => Math.Clamp(double.IsFinite(v)?v:fallback,min,max);
 public static CardSize Fit(AppearanceOptions options,double workWidth,double workHeight) {
  double width=Math.Min(Finite(options.Width,480,360,560),Math.Max(240,workWidth-32));
  double height=Math.Min(Finite(options.Height,420,220,800),Math.Max(160,workHeight-32));
  return new(width,height);
 }
 // The native template owns padding and buttons; only its measured viewport is used here.
 public static double ContentHeight(double viewport,double insets,double titleHeight) => Math.Max(0,viewport-Math.Max(0,insets)-Math.Max(0,titleHeight));
 public static CardSize MeasureCard(AppearanceOptions options,double workWidth,double workHeight,double chrome,double content) {
  var limit=Fit(options,workWidth,workHeight);return limit with{Height=CompactHeight(limit.Height,chrome,content)};
 }
 public static double CompactHeight(double limit,double chrome,double content) => Math.Min(limit,Math.Max(Math.Min(180,limit),Math.Max(0,chrome)+Math.Max(0,content)));
 public static bool StackHeader(double contentWidth) => contentWidth < 300;
 public static int Remaining(int seconds,TimeSpan elapsed) => Math.Max(0,(int)Math.Ceiling(seconds-Math.Max(0,elapsed.TotalSeconds)));
 public static bool AcceptSource(string source,ReceptionOptions options) =>
  source.Equals("QQ",StringComparison.OrdinalIgnoreCase) || source.Equals("腾讯QQ",StringComparison.OrdinalIgnoreCase);
}
