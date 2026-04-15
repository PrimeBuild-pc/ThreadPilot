# Exception Handling Policy

This policy defines how ThreadPilot handles, classifies, and reports runtime failures.

## Goals

- Prevent silent failures in background tasks.
- Keep UI responsive when recoverable faults occur.
- Preserve actionable diagnostics for post-mortem analysis.

## Exception Hierarchy

Implemented domain exception tree:

- ThreadPilotException
- ProcessManagementException
- PrivilegeException
- RuleEngineException
- ResourceOptimizationException
- PersistenceException

Each domain exception carries an ErrorCode value for structured logging.

## Global Handlers

Global safety net handlers are registered in application startup:

- AppDomain.CurrentDomain.UnhandledException
- Application.DispatcherUnhandledException
- TaskScheduler.UnobservedTaskException

Behavior summary:

- AppDomain unhandled: critical log + blocking error dialog.
- Dispatcher unhandled: error log + user choice to continue or terminate.
- Unobserved task: logged and marked observed to avoid process termination from finalizer escalation.

## Logging and Correlation

Unhandled exceptions are routed to:

- ILogger<App> for immediate diagnostic visibility.
- IEnhancedLoggingService for persisted structured telemetry.

Structured context includes:

- Source handler
- ErrorCode (domain code when available)
- CorrelationId (if available)
- Termination-level hint

## Recovery Guidelines

- Prefer local try/catch + retry policies for known transient operations.
- Use RetryPolicyService with operation-specific predicates.
- Reserve global handlers for truly unhandled paths only.

## Guard Clause Guidelines

- Validate external inputs at entry points.
- Throw ArgumentException/ArgumentNullException early for invalid arguments.
- Wrap domain failures in ThreadPilotException-derived types when crossing service boundaries.
