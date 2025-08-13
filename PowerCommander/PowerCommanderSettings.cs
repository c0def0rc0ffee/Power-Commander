using System.Collections.Generic;

public class PowerCommanderSettings
{
    public bool LoadService { get; set; } = true;
    public List<string> ShutdownTimes { get; set; } = new List<string> { "21:00" };
    public bool ShowNextScheduledTime { get; set; } = true;
    public bool EnableExitOption { get; set; } = true;
    public bool EnableManualShutdown { get; set; } = true;
    public bool EnableStartupToggle { get; set; } = true;
}
