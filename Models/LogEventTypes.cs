namespace ThreadPilot.Models
{
    public static class LogEventTypes
    {
        public static class PowerPlan
        {
            public const string Changed = "PowerPlanChanged";
            public const string ChangeRequested = "PowerPlanChangeRequested";
            public const string ChangeFailed = "PowerPlanChangeFailed";
            public const string Restored = "PowerPlanRestored";
            public const string DefaultSet = "DefaultPowerPlanSet";
            public const string EnumerationFailed = "PowerPlanEnumerationFailed";
        }

        public static class ProcessMonitoring
        {
            public const string Started = "ProcessStarted";
            public const string Stopped = "ProcessStopped";
            public const string MonitoringStarted = "MonitoringStarted";
            public const string MonitoringStopped = "MonitoringStopped";
            public const string WmiEventReceived = "WmiEventReceived";
            public const string WmiConnectionFailed = "WmiConnectionFailed";
            public const string PollingFallback = "PollingFallbackActivated";
            public const string ProcessDetected = "ProcessDetected";
            public const string ProcessLost = "ProcessLost";
            public const string AssociationTriggered = "AssociationTriggered";
        }

        public static class System
        {
            public const string ApplicationStarted = "ApplicationStarted";
            public const string ApplicationShutdown = "ApplicationShutdown";
            public const string ServiceInitialized = "ServiceInitialized";
            public const string ServiceStarted = "ServiceStarted";
            public const string ServiceStopped = "ServiceStopped";
            public const string ConfigurationLoaded = "ConfigurationLoaded";
            public const string ConfigurationSaved = "ConfigurationSaved";
            public const string CpuTopologyDetected = "CpuTopologyDetected";
            public const string SystemTrayInitialized = "SystemTrayInitialized";
            public const string NotificationSent = "NotificationSent";
            public const string ErrorRecovered = "ErrorRecovered";
        }

        public static class Errors
        {
            public const string ServiceFailure = "ServiceFailure";
            public const string ConfigurationError = "ConfigurationError";
            public const string FileSystemError = "FileSystemError";
            public const string PermissionError = "PermissionError";
            public const string NetworkError = "NetworkError";
            public const string WmiError = "WmiError";
            public const string UnhandledException = "UnhandledException";
            public const string ValidationError = "ValidationError";
        }

        public static class Performance
        {
            public const string HighMemoryUsage = "HighMemoryUsage";
            public const string HighCpuUsage = "HighCpuUsage";
            public const string SlowOperation = "SlowOperation";
            public const string LargeLogFile = "LargeLogFile";
            public const string ProcessCountHigh = "ProcessCountHigh";
            public const string Gen2PauseAlert = "Gen2PauseAlert";
        }
    }

    public static class LogProperties
    {
        public const string ProcessName = "ProcessName";
        public const string ProcessId = "ProcessId";
        public const string PowerPlanId = "PowerPlanId";
        public const string PowerPlanName = "PowerPlanName";
        public const string GameName = "GameName";
        public const string UserId = "UserId";
        public const string SessionId = "SessionId";
        public const string CorrelationId = "CorrelationId";
        public const string Duration = "Duration";
        public const string ErrorCode = "ErrorCode";
        public const string StackTrace = "StackTrace";
        public const string MemoryUsage = "MemoryUsage";
        public const string CpuUsage = "CpuUsage";
        public const string ThreadId = "ThreadId";
        public const string Version = "Version";
        public const string Environment = "Environment";
    }
}
