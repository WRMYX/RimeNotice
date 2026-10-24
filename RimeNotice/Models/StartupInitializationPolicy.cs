namespace RimeNotice.Models;
public static class StartupInitializationPolicy {
 public static bool ShouldEnable(bool fresh,bool enabled,bool blocked,bool existingRunEntry)=>fresh&&!enabled&&!blocked&&!existingRunEntry;
}
