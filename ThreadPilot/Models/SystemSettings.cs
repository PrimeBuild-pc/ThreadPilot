using System;

namespace ThreadPilot.Models
{
    [Serializable]
    public class SystemSettings
    {
        // Core Parking
        public bool CoreParkingEnabled { get; set; } = true;

        // Processor Performance Boost Mode (0=Disabled, 1=Enabled, 2=Aggressive, 3=Efficient Aggressive)
        public int PerformanceBoostMode { get; set; } = 1;

        // System Responsiveness (MMCSS) (0-100, 0 for gaming, 20 for balanced)
        public int SystemResponsiveness { get; set; } = 20;

        // Network Throttling Index (-1 to disable, 10 for default)
        public int NetworkThrottlingIndex { get; set; } = 10;

        // Windows Priority Separation (Windows default is 2, gaming is 38)
        public int PrioritySeparation { get; set; } = 2;

        // Game Mode
        public bool GameModeEnabled { get; set; } = true;

        // GameBar
        public bool GameBarEnabled { get; set; } = true;

        // GameDVR
        public bool GameDVREnabled { get; set; } = true;

        // Hibernation
        public bool HibernationEnabled { get; set; } = true;

        // Visual Effects (0=Let Windows decide, 1=Best performance, 2=Custom, 3=Best appearance)
        public int VisualEffectsLevel { get; set; } = 0;
    }

    [Serializable]
    public class WindowSettings
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
    }
}
