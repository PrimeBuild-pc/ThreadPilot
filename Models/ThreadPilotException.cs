/*
 * ThreadPilot - exception hierarchy and error code registry.
 */
namespace ThreadPilot.Models
{
    using System;

    /// <summary>
    /// Defines stable error codes used in diagnostics and incident reporting.
    /// </summary>
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

    /// <summary>
    /// Base exception type for domain-level ThreadPilot failures.
    /// </summary>
    public class ThreadPilotException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadPilotException"/> class.
        /// </summary>
        public ThreadPilotException()
            : this("A ThreadPilot error has occurred.", ErrorCode.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadPilotException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        public ThreadPilotException(string message)
            : this(message, ErrorCode.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadPilotException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Inner exception.</param>
        public ThreadPilotException(string message, Exception innerException)
            : this(message, ErrorCode.Unknown, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadPilotException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="errorCode">Domain error code.</param>
        public ThreadPilotException(string message, ErrorCode errorCode)
            : base(message)
        {
            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadPilotException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="errorCode">Domain error code.</param>
        /// <param name="innerException">Inner exception.</param>
        public ThreadPilotException(string message, ErrorCode errorCode, Exception innerException)
            : base(message, innerException)
        {
            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Gets the domain error code associated with this exception.
        /// </summary>
        public ErrorCode ErrorCode { get; }
    }

    /// <summary>
    /// Exception for process monitoring and process-control failures.
    /// </summary>
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

    /// <summary>
    /// Exception for privilege and elevation failures.
    /// </summary>
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

    /// <summary>
    /// Exception for rule parsing and rule-matching failures.
    /// </summary>
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

    /// <summary>
    /// Exception for performance/resource optimization failures.
    /// </summary>
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

    /// <summary>
    /// Exception for persistence/configuration I/O failures.
    /// </summary>
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
