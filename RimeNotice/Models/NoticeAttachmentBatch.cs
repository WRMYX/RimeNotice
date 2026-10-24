using System;
using RimeNotice.Services;
namespace RimeNotice.Models;
public sealed record NoticeAttachmentBatch(string BatchId,NoticeAttachment[] Items,string[] SourceKeys,bool Inferred,bool IsWaiting=false) {
 public int Revision {get;init;}
 public static NoticeAttachmentBatch Empty(string key)=>new(Guid.NewGuid().ToString("N"),[],[key],false);
}
