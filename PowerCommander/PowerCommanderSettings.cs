using System.Collections.Generic;

public class PowerCommanderSettings
{
    public bool LoadService { get; set; } = false;
    public List<string> ShutdownTimes { get; set; }
    public bool ShowNextScheduledTime { get; set; } = true;
    public bool EnableExitOption { get; set; } = true;
    public bool EnableManualShutdown { get; set; } = true;
}
