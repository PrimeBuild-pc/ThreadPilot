namespace ThreadPilot.Platforms.Windows
{
    using ThreadPilot.Services;

    public sealed record CpuSetApplyResult
    {
        public bool Success { get; init; }

        public string ErrorCode { get; init; } = AffinityApplyErrorCodes.None;

        public int Win32ErrorCode { get; init; }

        public string UserMessage { get; init; } = string.Empty;

        public string TechnicalMessage { get; init; } = string.Empty;

        public bool IsAccessDenied { get; init; }

        public bool IsAntiCheatLikely { get; init; }

        public static CpuSetApplyResult Succeeded(string technicalMessage) =>
            new()
            {
                Success = true,
                TechnicalMessage = technicalMessage,
            };

        public static CpuSetApplyResult Failed(
            string errorCode,
            string userMessage,
            string technicalMessage,
            int win32ErrorCode = 0,
            bool isAccessDenied = false,
            bool isAntiCheatLikely = false) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                UserMessage = userMessage,
                TechnicalMessage = technicalMessage,
                Win32ErrorCode = win32ErrorCode,
                IsAccessDenied = isAccessDenied,
                IsAntiCheatLikely = isAntiCheatLikely,
            };
    }
}
