/*
 * ThreadPilot - process power plan association tests.
 */
namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;

    public sealed class ProcessPowerPlanAssociationTests
    {
        [Fact]
        public void MatchesProcess_WithWildcardPath_MatchesOnlyTargetPath()
        {
            var association = new ProcessPowerPlanAssociation
            {
                IsEnabled = true,
                MatchByPath = true,
                ExecutableName = "node",
                ExecutablePath = @"*Raycast*\backend\node.exe",
            };

            Assert.True(association.MatchesProcess(new ProcessModel
            {
                Name = "node.exe",
                ExecutablePath = @"C:\Program Files\WindowsApps\Raycast.Raycast_2.5.3.0_x64__publisher\Raycast\backend\NODE.EXE",
            }));
            Assert.False(association.MatchesProcess(new ProcessModel
            {
                Name = "node.exe",
                ExecutablePath = @"C:\nvm4w\nodejs\node.exe",
            }));
        }
    }
}
