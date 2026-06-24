namespace ThreadPilot.Services
{
    using System.Diagnostics;

    public interface ISecurityService
    {
        bool ValidateElevatedOperation(string operation);

        Task AuditElevatedAction(string action, string target, bool success);

        bool ValidateProcessOperation(string processName, string operation);

        bool ValidatePowerPlanOperation(string powerPlanId, string operation);

        string[] GetAllowedElevatedOperations();

        bool IsProtected(Process process);
    }
}

