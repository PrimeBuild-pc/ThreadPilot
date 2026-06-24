namespace ThreadPilot.Services
{
    public interface ISelfResourceManagementService
    {
        void ApplyLowImpactMode(bool limitAffinity);

        void RestoreForegroundMode();
    }
}
