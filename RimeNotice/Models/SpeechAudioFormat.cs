using System;
using System.IO;
using System.Text;
namespace RimeNotice.Models;
public static class SpeechAudioFormat {
 public static byte[] Wave(byte[] pcm,int rate){if(pcm.Length%2!=0||rate<8000||rate>96000)throw new InvalidDataException("PCM 应为单声道 16 位小端音频，请检查采样率。");
  using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(pcm.Length+36);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(pcm.Length);writer.Write(pcm);return memory.ToArray();
 }
}
