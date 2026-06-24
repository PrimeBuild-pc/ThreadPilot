namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessClassifierTests
    {
        [Fact]
        public void Classify_ReturnsForegroundAppForForegroundPid()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = "Game",
                HasVisibleWindow = true,
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(10));

            Assert.Equal(ProcessClassification.ForegroundApp, result);
        }

        [Fact]
        public void Classify_ReturnsVisibleWindowAppForVisibleNonForegroundProcess()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = "Editor",
                HasVisibleWindow = true,
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(20));

            Assert.Equal(ProcessClassification.VisibleWindowApp, result);
        }

        [Theory]
        [InlineData("svchost")]
        [InlineData("svchost.exe")]
        public void Classify_ReturnsSystemForNormalizedSystemProcessNames(string processName)
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = processName,
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(null));

            Assert.Equal(ProcessClassification.System, result);
        }

        [Fact]
        public void Classify_ReturnsProtectedOrAccessDeniedWhenAccessWasDenied()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = "ProtectedProcess",
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(null, AccessDenied: true));

            Assert.Equal(ProcessClassification.ProtectedOrAccessDenied, result);
        }

        [Fact]
        public void Classify_ReturnsTerminatedWhenProcessTerminated()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = "ClosedProcess",
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(null, Terminated: true));

            Assert.Equal(ProcessClassification.Terminated, result);
        }

        [Fact]
        public void Classify_ReturnsBackgroundUserForNonSystemProcessWithoutWindow()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = "Worker",
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(null));

            Assert.Equal(ProcessClassification.BackgroundUser, result);
        }

        [Fact]
        public void Classify_TerminatedTakesPrecedenceOverForegroundAndSystem()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = "svchost",
                HasVisibleWindow = true,
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(10, Terminated: true));

            Assert.Equal(ProcessClassification.Terminated, result);
        }

        [Fact]
        public void Classify_AccessDeniedTakesPrecedenceOverForegroundAndWindow()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = "ProtectedWindow",
                HasVisibleWindow = true,
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(10, AccessDenied: true));

            Assert.Equal(ProcessClassification.ProtectedOrAccessDenied, result);
        }

        [Fact]
        public void Classify_ReturnsUnknownWhenNameIsMissing()
        {
            var classifier = new ProcessClassifier(new ProcessFilterService());
            var process = new ProcessModel
            {
                ProcessId = 10,
                Name = string.Empty,
            };

            var result = classifier.Classify(process, new ProcessClassificationContext(null));

            Assert.Equal(ProcessClassification.Unknown, result);
        }
    }
}
