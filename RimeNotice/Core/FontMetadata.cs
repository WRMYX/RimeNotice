using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
namespace RimeNotice.Core;
public static class FontMetadata {
 public static string Family(byte[] bytes){
  if(bytes.Length<12||bytes.Length>32*1024*1024)throw new InvalidDataException("字体文件大小无效。");
  uint signature=BinaryPrimitives.ReadUInt32BigEndian(bytes);if(signature!=0x00010000&&signature!=0x4f54544f)throw new InvalidDataException("请选择有效的 TTF 或 OTF 字体。");
  int U16(int p){if(p<0||p>bytes.Length-2)throw new InvalidDataException("字体索引越界。");return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p,2));}
  int U32(int p){if(p<0||p>bytes.Length-4)throw new InvalidDataException("字体索引越界。");uint v=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(p,4));if(v>int.MaxValue)throw new InvalidDataException("字体索引过大。");return (int)v;}
  int tables=U16(4);if(tables>4096)throw new InvalidDataException("字体表数量无效。");
  for(int i=0;i<tables;i++){int p=12+i*16;if(U32(p)!=0x6e616d65)continue;int offset=U32(p+8),length=U32(p+12);if(offset>bytes.Length-length||length<6)throw new InvalidDataException("字体名称表无效。");
   int count=U16(offset+2),strings=U16(offset+4);if(count>4096||strings>length||6+count*12>length)throw new InvalidDataException("字体名称记录无效。");
   string? selected=null;int best=-1;
   for(int j=0;j<count;j++){int q=offset+6+j*12,platform=U16(q),language=U16(q+4),id=U16(q+6),size=U16(q+8),at=U16(q+10);if(id!=1&&id!=16)continue;if(size<=0||strings+at>length-size)continue;
    string name=(platform is 0 or 3?Encoding.BigEndianUnicode:Encoding.Latin1).GetString(bytes,offset+strings+at,size).Trim();if(name.Length==0||name.Length>256||name.IndexOfAny(['\0','\r','\n','#'])>=0)continue;
    int score=(id==16?10:0)+(platform==3?4:0)+(language==0x409?2:0);if(score>best){best=score;selected=name;}
   }return selected??throw new InvalidDataException("字体中未找到可用的家族名称。");
  }throw new InvalidDataException("字体缺少名称表。");
 }
}
