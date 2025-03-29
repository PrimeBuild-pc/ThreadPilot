using System;

namespace ThreadPilot.Models
{
    public class PowerProfile
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsBuiltIn { get; set; }
    }
}
