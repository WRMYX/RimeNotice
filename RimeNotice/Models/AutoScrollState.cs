using System;
namespace RimeNotice.Models;
// Pure state, shared by body/gallery viewports. Time is monotonic elapsed seconds.
public sealed class AutoScrollState {
 private double _resumeAt,_last,_endAt=-1,_knownEnd; private bool _interacting;
 public bool FirstPassCompleted {get;private set;}
 public bool Manual {get;private set;}
 public void Reset(double now){_last=now;_resumeAt=now+2;Manual=false;_interacting=false;_endAt=-1;FirstPassCompleted=false;_knownEnd=0;}
 public void User(double now,double wait,bool interacting){Manual=true;_interacting=interacting;_resumeAt=now+wait;_last=now;_endAt=-1;}
 public bool Paused(double now)=>_interacting||Manual&&now<_resumeAt;
 public double Advance(double now,double offset,double end,double speed,bool enabled){
  double dt=Math.Clamp(now-_last,0,.1);_last=now;
  if(end>_knownEnd+2&&offset<end-1)FirstPassCompleted=false;_knownEnd=end;
  if(end<=2){FirstPassCompleted=true;return offset;}
  if(offset>=end-1)FirstPassCompleted=true;
  if(Paused(now))return offset;
  if(Manual){Manual=false;_resumeAt=now+1;}
  if(!enabled||now<_resumeAt)return offset;
  if(offset>=end-1){if(_endAt<0)_endAt=now;return now-_endAt>=2?ResetCycle(now):offset;}
  _endAt=-1;return Math.Min(end,offset+speed*dt);
 }
 private double ResetCycle(double now){_resumeAt=now+2;_endAt=-1;return 0;}
}
