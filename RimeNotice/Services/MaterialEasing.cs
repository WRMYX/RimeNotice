using Microsoft.UI.Xaml.Media.Animation;
namespace RimeNotice.Services;
internal static class MaterialEasing {
 internal static EasingFunctionBase Create(bool entering,double exponent) => new ExponentialEase {
  Exponent=System.Math.Clamp(exponent,0,10), EasingMode=entering?EasingMode.EaseOut:EasingMode.EaseIn
 };
}
