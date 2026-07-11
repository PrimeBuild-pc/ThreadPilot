namespace ThreadPilot.Services
{
    using ThreadPilot.Models;

    internal static class PersistentRuleRuntimeKeys
    {
        public static string GetRuleSignature(PersistentProcessRule rule) =>
            string.Join(
                "|",
                string.IsNullOrWhiteSpace(rule.Id) ? rule.Name : rule.Id,
                rule.UpdatedAt.ToUniversalTime().Ticks);
    }
}
