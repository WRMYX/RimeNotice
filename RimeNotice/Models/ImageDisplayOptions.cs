using System;
namespace RimeNotice.Models;
public enum ImagePlacement { Inline, Outside }
public enum ImageSide { Left, Right }
public sealed record ImageDisplayOptions {
 public ImagePlacement Placement {get;init;}=ImagePlacement.Inline;
 public ImageSide Side {get;init;}=ImageSide.Right;
 public bool AutoScroll {get;init;}=true;
 public double ScrollSpeed {get;init;}=24;
 public double PanelWidth {get;init;}=480;
 public double BatchGapSeconds {get;init;}=2;
 public double ResumeSeconds {get;init;}=3;
 public ImageDisplayOptions Normalize()=>this with {
  Placement=Enum.IsDefined(Placement)?Placement:ImagePlacement.Inline,Side=Enum.IsDefined(Side)?Side:ImageSide.Right,
  ScrollSpeed=NotificationLayout.Finite(ScrollSpeed,24,8,80),PanelWidth=NotificationLayout.Finite(PanelWidth,480,240,800),
  BatchGapSeconds=NotificationLayout.Finite(BatchGapSeconds,2,.5,5),ResumeSeconds=NotificationLayout.Finite(ResumeSeconds,3,1,30)
 };
}
