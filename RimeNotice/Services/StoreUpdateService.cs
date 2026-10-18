using System;
using Windows.ApplicationModel;
namespace RimeNotice.Services;
internal static class StoreUpdateService {
 public static Package? GetInstalledPackage(){try{return Package.Current;}catch(InvalidOperationException){return null;}}
 public static bool SupportsUpdates(Package? p)=>p is not null&&p.SignatureKind==PackageSignatureKind.Store;
}
