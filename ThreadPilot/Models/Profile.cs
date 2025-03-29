using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ThreadPilot.Models
{
    [Serializable]
    public class Profile
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public List<ProcessSetting> ProcessSettings { get; set; } = new List<ProcessSetting>();
        public SystemSettings SystemOptimizations { get; set; }
        public string PowerProfileGuid { get; set; }
        public string PowerProfileName { get; set; }

        public Profile()
        {
            Created = DateTime.Now;
            LastModified = DateTime.Now;
        }

        public Profile(string name)
        {
            Name = name;
            Created = DateTime.Now;
            LastModified = DateTime.Now;
        }
    }

    [Serializable]
    public class ProcessSetting
    {
        public string ProcessName { get; set; }
        public long AffinityMask { get; set; }
        public ProcessPriorityClass Priority { get; set; }

        public ProcessSetting()
        {
            Priority = ProcessPriorityClass.Normal;
        }

        public ProcessSetting(string processName)
        {
            ProcessName = processName;
            Priority = ProcessPriorityClass.Normal;
        }
    }
}
