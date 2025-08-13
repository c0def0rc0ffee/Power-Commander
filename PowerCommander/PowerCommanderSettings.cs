using System.Collections.Generic;

namespace PowerCommander
{
    public class PowerCommanderSettings
    {
        public bool LoadService { get; set; } = true;
        public List<string> ShutdownTimes { get; set; } = new List<string>();
        public bool ShowNextScheduledTime { get; set; } = true;
        public bool EnableExitOption { get; set; } = true;
        public bool EnableManualShutdown { get; set; } = true;
        public bool EnableStartupToggle { get; set; } = true;
    }
}

