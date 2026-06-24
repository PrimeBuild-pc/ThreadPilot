/*
 * ThreadPilot - exception hierarchy and error code registry.
 */
namespace ThreadPilot.Models
{
    using System;

    public enum ErrorCode
    {
        Unknown = 0,
        ProcessManagement = 1000,
        Privilege = 2000,
        RuleEngine = 3000,
        ResourceOptimization = 4000,
        Persistence = 5000,
        Unhandled = 9000,
    }

    public class ThreadPilotException : Exception
    {
        public ThreadPilotException()
            : this("A ThreadPilot error has occurred.", ErrorCode.Unknown)
        {
        }

        public ThreadPilotException(string message)
            : this(message, ErrorCode.Unknown)
        {
        }

        public ThreadPilotException(string message, Exception innerException)
            : this(message, ErrorCode.Unknown, innerException)
        {
        }

        public ThreadPilotException(string message, ErrorCode errorCode)
            : base(message)
        {
            this.ErrorCode = errorCode;
        }

        public ThreadPilotException(string message, ErrorCode errorCode, Exception innerException)
            : base(message, innerException)
        {
            this.ErrorCode = errorCode;
        }

        public ErrorCode ErrorCode { get; }
    }

    public sealed class ProcessManagementException : ThreadPilotException
    {
        public ProcessManagementException(string message)
            : base(message, ErrorCode.ProcessManagement)
        {
        }

        public ProcessManagementException(string message, Exception innerException)
            : base(message, ErrorCode.ProcessManagement, innerException)
        {
        }
    }

    public sealed class PrivilegeException : ThreadPilotException
    {
        public PrivilegeException(string message)
            : base(message, ErrorCode.Privilege)
        {
        }

        public PrivilegeException(string message, Exception innerException)
            : base(message, ErrorCode.Privilege, innerException)
        {
        }
    }

    public sealed class RuleEngineException : ThreadPilotException
    {
        public RuleEngineException(string message)
            : base(message, ErrorCode.RuleEngine)
        {
        }

        public RuleEngineException(string message, Exception innerException)
            : base(message, ErrorCode.RuleEngine, innerException)
        {
        }
    }

    public sealed class ResourceOptimizationException : ThreadPilotException
    {
        public ResourceOptimizationException(string message)
            : base(message, ErrorCode.ResourceOptimization)
        {
        }

        public ResourceOptimizationException(string message, Exception innerException)
            : base(message, ErrorCode.ResourceOptimization, innerException)
        {
        }
    }

    public sealed class PersistenceException : ThreadPilotException
    {
        public PersistenceException(string message)
            : base(message, ErrorCode.Persistence)
        {
        }

        public PersistenceException(string message, Exception innerException)
            : base(message, ErrorCode.Persistence, innerException)
        {
        }
    }
}
