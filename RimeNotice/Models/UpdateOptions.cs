namespace RimeNotice.Models;
public sealed record UpdateOptions {
 public bool CheckOnStartup { get; init; }
 public string SourceId { get; init; } = "direct";
 public string CustomPrefix { get; init; } = "";
 public UpdateOptions Normalize() => this with {
  SourceId = SourceId is "direct" or "ghproxy" or "ghfast" or "ghproxynet" or "custom" ? SourceId : "direct",
  CustomPrefix = CustomPrefix ?? ""
 };
}
