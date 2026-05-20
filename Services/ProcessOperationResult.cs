/*
 * ThreadPilot - process operation result model.
 */
namespace ThreadPilot.Services
{
    public sealed record ProcessOperationResult
    {
        public bool Success { get; init; }

        public string ErrorCode { get; init; } = string.Empty;

        public string UserMessage { get; init; } = string.Empty;

        public string TechnicalMessage { get; init; } = string.Empty;

        public bool IsAccessDenied { get; init; }

        public bool IsAntiCheatLikely { get; init; }

        public bool IsProcessExited { get; init; }

        public static ProcessOperationResult Succeeded(string userMessage, string technicalMessage) =>
            new()
            {
                Success = true,
                UserMessage = userMessage,
                TechnicalMessage = technicalMessage,
            };

        public static ProcessOperationResult Failed(
            string errorCode,
            string userMessage,
            string technicalMessage,
            bool isAccessDenied = false,
            bool isAntiCheatLikely = false,
            bool isProcessExited = false) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                UserMessage = userMessage,
                TechnicalMessage = technicalMessage,
                IsAccessDenied = isAccessDenied,
                IsAntiCheatLikely = isAntiCheatLikely,
                IsProcessExited = isProcessExited,
            };
    }
}
