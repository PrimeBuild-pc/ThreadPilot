namespace ThreadPilot.Services
{
    public enum ProcessPriorityWriteSource
    {
        UnknownInternal,
        PersistentRuleInitialApply,
        PersistentRuleRetry,
        PowerPlanAssociation,
        ProcessProfileLoad,
        ManualUiAction,
        CleanupOrRestore,
    }
}
