using System;
using System.Collections.Generic;
namespace RimeNotice.Models;
public static class SpeechText {
 public static IEnumerable<string> Chunks(string text,int maximum=800){
  if(maximum<2)throw new ArgumentOutOfRangeException(nameof(maximum));
  for(int start=0;start<text.Length;){int length=Math.Min(maximum,text.Length-start);if(start+length<text.Length&&char.IsHighSurrogate(text[start+length-1]))length--;
   if(start+length<text.Length){for(int i=start+length-1;i>start+length/2;i--)if("。！？；\n.!?;".Contains(text[i])){length=i-start+1;break;}}
   yield return text.Substring(start,length);start+=length;
  }
 }
}
