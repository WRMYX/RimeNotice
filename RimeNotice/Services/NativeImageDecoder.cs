using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
namespace RimeNotice.Services;
public static class NativeImageDecoder {
 public static async Task<bool> CanDecodeAsync(string path,CancellationToken token){
  try{token.ThrowIfCancellationRequested();using var stream=await(await StorageFile.GetFileFromPathAsync(path).AsTask(token)).OpenReadAsync().AsTask(token);var decoder=await BitmapDecoder.CreateAsync(stream).AsTask(token);token.ThrowIfCancellationRequested();if(decoder.PixelWidth==0||decoder.PixelHeight==0)return false;
   var transform=new BitmapTransform{ScaledWidth=Math.Min(64u,decoder.PixelWidth),ScaledHeight=Math.Min(64u,decoder.PixelHeight)};
   await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,transform,ExifOrientationMode.RespectExifOrientation,ColorManagementMode.DoNotColorManage).AsTask(token);token.ThrowIfCancellationRequested();return true;}
  catch(OperationCanceledException){throw;}catch(Exception ex){ServiceDiagnostics.Error("图片解码",ex);return false;}
 }
}
