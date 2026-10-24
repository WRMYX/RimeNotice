using System;
namespace RimeNotice.Models;

public enum MaterialEntrance { None=0, Fade=1, Top=3, Bottom=4, Left=5, Right=6 }
public enum MaterialExit { None=0, Fade=1, Top=3, Bottom=4, Left=5, Right=6 }
public sealed record MaterialMotionOptions {
 public MaterialEntrance Entrance {get;init;} = MaterialEntrance.Fade;
 public MaterialExit Exit {get;init;} = MaterialExit.Fade;
 public int EntranceMilliseconds {get;init;} = 260;
 public int ExitMilliseconds {get;init;} = 200;
 public double EntranceExponent {get;init;} = 3;
 public double ExitExponent {get;init;} = 3;
 public MaterialMotionOptions Normalize()=>this with {
  Entrance=Enum.IsDefined(Entrance)?Entrance:MaterialEntrance.Fade,Exit=Enum.IsDefined(Exit)?Exit:MaterialExit.Fade,
  EntranceMilliseconds=Math.Clamp(EntranceMilliseconds,80,1500),ExitMilliseconds=Math.Clamp(ExitMilliseconds,80,1500),
  EntranceExponent=NotificationLayout.Finite(EntranceExponent,3,0,10),ExitExponent=NotificationLayout.Finite(ExitExponent,3,0,10)
 };
}
public static class NoticeColor {
 public static string Normalize(string? value){
  if(string.IsNullOrWhiteSpace(value))return "";
  var text=value.Trim();if(text.Length==7&&text[0]=='#')text="#FF"+text[1..];
  return text.Length==9&&text[0]=='#'&&uint.TryParse(text.AsSpan(1),System.Globalization.NumberStyles.HexNumber,null,out var color)?$"#FF{color&0xffffff:X6}":"";
 }
}
