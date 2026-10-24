using System;
namespace RimeNotice.Models;
public static class ImagePreviewLayout {
 public static MotionBounds? Beside(MotionBounds card,MotionBounds work,double scale,double requestedWidth,ImageSide side){
  scale=double.IsFinite(scale)&&scale>0?scale:1;int gap=(int)Math.Ceiling(12*scale),minimum=(int)Math.Ceiling(240*scale);
  int available=side==ImageSide.Left?card.X-work.X-gap:work.X+work.Width-(card.X+card.Width)-gap;
  if(available<minimum||work.Height<120*scale)return null;
  int width=Math.Min(available,(int)Math.Ceiling(Math.Clamp(requestedWidth,240,800)*scale));
  int height=Math.Min(work.Height-(int)(24*scale),Math.Max(card.Height,(int)(320*scale)));
  int x=side==ImageSide.Left?card.X-gap-width:card.X+card.Width+gap;
  int y=Math.Clamp(card.Y,work.Y,work.Y+work.Height-height);
  return new(x,y,width,height);
 }
}
