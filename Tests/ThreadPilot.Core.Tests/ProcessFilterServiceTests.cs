namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessFilterServiceTests
    {
        [Theory]
        [InlineData("svchost")]
        [InlineData("svchost.exe")]
        [InlineData("csrss")]
        [InlineData("csrss.exe")]
        public void FilterAndSort_HidesSystemProcesses_WithOrWithoutExeSuffix(string processName)
        {
            var service = new ProcessFilterService();
            var processes = new[]
            {
                new ProcessModel { Name = processName, CpuUsage = 10 },
                new ProcessModel { Name = "UserApp", CpuUsage = 1 },
            };

            var result = service.FilterAndSort(processes, new ProcessFilterCriteria
            {
                HideSystemProcesses = true,
                SortMode = "Name",
            });

            var remaining = Assert.Single(result);
            Assert.Equal("UserApp", remaining.Name);
        }
    }
}
