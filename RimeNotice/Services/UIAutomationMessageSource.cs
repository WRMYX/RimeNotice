using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NativeUIAutomation;
namespace RimeNotice.Services;
public sealed class UIAutomationMessageSource : IMessageSource {
 private Task<SourceSnapshot>? _pending;
 public async Task<SourceSnapshot> PollAsync(CancellationToken token) {
  if(_pending is {IsCompleted:false})return new(false,"兼容接收源等待响应","UI Automation 尚在响应，不启动额外扫描线程。",[]);
  _pending=Task.Run(Scan,token);
  try{return await _pending.WaitAsync(TimeSpan.FromSeconds(3),token);}
  catch(TimeoutException){return new(false,"兼容接收源响应较慢","已限制扫描并发，请稍后重试。",[]);}
 }
 private static void Release(object? obj){try{if(obj is not null&&Marshal.IsComObject(obj))Marshal.FinalReleaseComObject(obj);}catch(Exception){}}
 private static SourceSnapshot Scan() {
  object? instance=null;IUIAutomationElement? root=null;IUIAutomationElementArray? hosts=null;IUIAutomationCondition? hostCondition=null;
  try {
   instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("E22AD333-B25F-460C-83D0-0581107395C9"),true)!);
   var auto=(IUIAutomation)instance!;var timeouts=(IUIAutomation2)instance!;timeouts.ConnectionTimeout=1000;timeouts.TransactionTimeout=1000;
   root=auto.GetRootElement();hostCondition=auto.CreatePropertyCondition(30012,"Windows.UI.Core.CoreWindow");
   hosts=root.FindAll(TreeScope.TreeScope_Children,hostCondition);var results=new List<SourceNotice>();
   for(int h=0;h<Math.Min(hosts.Length,32);h++) {
    IUIAutomationElement? host=null;IUIAutomationCondition? condition=null;IUIAutomationElementArray? toasts=null;
    try {
     host=hosts.GetElement(h);condition=auto.CreatePropertyCondition(30012,"FlexibleToastView");toasts=host.FindAll(TreeScope.TreeScope_Descendants,condition);
     for(int i=0;i<Math.Min(toasts.Length,32);i++) {
      IUIAutomationElement? toast=null;IUIAutomationElementArray? nodes=null;IUIAutomationCondition? any=null;
      try {
       toast=toasts.GetElement(i);any=auto.CreateTrueCondition();nodes=toast.FindAll(TreeScope.TreeScope_Subtree,any);
       var texts=new List<string>();bool qq=false;string? source=null;
       for(int j=0;j<Math.Min(nodes.Length,256);j++) {
        IUIAutomationElement? node=null;
        try{node=nodes.GetElement(j);string name=node.CurrentName?.Trim()??"";
         string automationId=node.CurrentAutomationId??"";
         bool sourceNode=automationId.Contains("AppName",StringComparison.OrdinalIgnoreCase)||automationId.Contains("ApplicationName",StringComparison.OrdinalIgnoreCase)||automationId.Contains("SourceName",StringComparison.OrdinalIgnoreCase)||node.CurrentControlType==50000;
         if(sourceNode&&name is "QQ" or "腾讯QQ"){qq=true;source=name;}
         else if(sourceNode&&!string.IsNullOrWhiteSpace(name)&&automationId.Contains("AppName",StringComparison.OrdinalIgnoreCase))source=name;
         if(node.CurrentControlType==50020&&!string.IsNullOrWhiteSpace(name))texts.Add(name);
        }finally{Release(node);}
       }
       texts=texts.Where(t=>t!=source).Distinct().ToList();if(texts.Count==0)continue;
       source=qq?"QQ":source??"未知应用";
       string id=string.Join(",",toast.GetRuntimeId());
       results.Add(new(id,texts[0],string.Join("\n",texts.Skip(1)),source,"uia:"+source));
      }finally{Release(nodes);Release(any);Release(toast);}
     }
    }finally{Release(toasts);Release(condition);Release(host);}
   }
   ServiceDiagnostics.Set("UIA","已扫描系统通知；未知来源在仅 QQ 模式下被过滤");
   return new(true,"正在通过兼容模式接收","仅识别系统可访问的通知，不读取 QQ 聊天数据库。",results);
  }catch(Exception ex){ServiceDiagnostics.Error("UIA",ex);return new(false,"兼容接收源暂不可用","请查看调试诊断或切换回系统通知模式。",[]);}
  finally{Release(hosts);Release(hostCondition);Release(root);Release(instance);}
 }
}
