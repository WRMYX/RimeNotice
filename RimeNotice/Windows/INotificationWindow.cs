using System;
using System.Threading.Tasks;
using RimeNotice.Services;
namespace RimeNotice.Windows;

public interface INotificationWindow
{
 Task Completion { get; }
 void ShowNotice(Func<IncomingNotice?>? takeNext=null);
 void SetPending(IncomingNotice[] pending,int count);
 void Close();
}
