using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
namespace RimeNotice.Models;
public sealed class NotificationRevisionTracker {
 private readonly Dictionary<string,string> _known=new();
 public bool Observe(string key,string sender,string body){
  string digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sender+"\0"+body)));
  bool changed=!_known.TryGetValue(key,out var old)||old!=digest;_known[key]=digest;return changed;
 }
 public void Retain(HashSet<string> ids){foreach(var key in new List<string>(_known.Keys))if(!ids.Contains(key))_known.Remove(key);}
 public void Clear()=>_known.Clear();
}
