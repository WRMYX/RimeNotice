using System;
using System.IO;
using System.Security.Cryptography;
using RimeNotice.Core;
namespace RimeNotice.Services;
public static class FontImportService {
 public static string Import(string source){
  var file=new FileInfo(source);if(!file.Exists||file.Length>32*1024*1024)throw new InvalidDataException("字体不存在或超过32 MB。");
  string extension=Path.GetExtension(source).ToLowerInvariant();if(extension is not ".ttf" and not ".otf")throw new InvalidDataException("请选择 TTF 或 OTF 字体。");
  byte[] data=File.ReadAllBytes(source);string family=FontMetadata.Family(data),name=Convert.ToHexString(SHA256.HashData(data))+extension;
  string root;bool packaged;try{root=global::Windows.Storage.ApplicationData.Current.LocalFolder.Path;packaged=true;}catch(Exception ex) when(ex is InvalidOperationException or System.Runtime.InteropServices.COMException){root=AppContext.BaseDirectory;packaged=false;}
  string folder=Path.Combine(root,packaged?"Fonts":"UserFonts");Directory.CreateDirectory(folder);string target=Path.Combine(folder,name);if(!File.Exists(target))File.WriteAllBytes(target,data);
  return (packaged?"ms-appdata:///local/Fonts/"+name:"ms-appx:///UserFonts/"+name)+"#"+Uri.EscapeDataString(family);
 }
}
