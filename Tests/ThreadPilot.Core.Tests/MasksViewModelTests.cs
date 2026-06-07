namespace ThreadPilot.Core.Tests
{
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using Moq;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class MasksViewModelTests
    {
        [Fact]
        public void MasksView_SubtitleClarifiesPerProcessUse()
        {
            var document = LoadMasksViewXaml();
            var serialized = document.ToString(SaveOptions.DisableFormatting);
            var locale = LoadEnglishLocale();

            Assert.Contains("MasksView_Subtitle", serialized, StringComparison.Ordinal);
            Assert.Contains("per-process use", locale, StringComparison.Ordinal);
        }

        [Fact]
        public void MasksView_ContainsEditingOnlyClarification()
        {
            var document = LoadMasksViewXaml();
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("does not change CPU affinity", serialized, StringComparison.Ordinal);
            Assert.Contains("until you apply it to a process", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void MasksView_DefaultPresetTooltipWarnsNoAutoApply()
        {
            var document = LoadMasksViewXaml();
            var serialized = document.ToString(SaveOptions.DisableFormatting);
            var locale = LoadEnglishLocale();

            Assert.Contains("MasksView_DefaultPresetTip", serialized, StringComparison.Ordinal);
            Assert.Contains("does not apply CPU affinity automatically", locale, StringComparison.Ordinal);
            Assert.Contains("Pre-selected when ThreadPilot", locale, StringComparison.Ordinal);
        }

        [Fact]
        public void MasksView_NoGlobalAffinityControls()
        {
            var document = LoadMasksViewXaml();
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.DoesNotContain("apply globally", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("global affinity", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("disable SMT", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HyperThreading", serialized, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MasksView_ToggleCpuTextClarifiesNoRunningProcessImpact()
        {
            var document = LoadMasksViewXaml();
            var serialized = document.ToString(SaveOptions.DisableFormatting);
            var locale = LoadEnglishLocale();

            Assert.Contains("MasksView_SelectCpusTip", serialized, StringComparison.Ordinal);
            Assert.Contains("do not affect running processes", locale, StringComparison.Ordinal);
        }

        [Fact]
        public void MasksView_DeleteWarningRefersToProcessesAndRules_NotGlobal()
        {
            var document = LoadMasksViewXaml();
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.DoesNotContain("all processes", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("system-wide", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("globally", serialized, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MasksViewModel_ExposesOnlyCrudCommands()
        {
            var commandNames = typeof(MasksViewModel)
                .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(p => p.PropertyType.Name.Contains("ommand", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Name)
                .ToList();

            Assert.Contains("CreateMaskCommand", commandNames);
            Assert.Contains("DeleteMaskCommand", commandNames);
            Assert.Contains("DuplicateMaskCommand", commandNames);
        }

        [Fact]
        public void MasksViewModel_HasNoAffinityApplyDependencies()
        {
            var constructorDependencies = typeof(MasksViewModel)
                .GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType.FullName ?? p.ParameterType.Name)
                .ToList();

            Assert.DoesNotContain("AffinityApplyService", constructorDependencies, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("ProcessAffinityApplyCoordinator", constructorDependencies, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("IProcessService", constructorDependencies, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void MasksViewModel_HasNoAffinityApplyMethods()
        {
            var methods = typeof(MasksViewModel)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(m => m.Name)
                .ToList();

            Assert.DoesNotContain(methods, m => Regex.IsMatch(m, "Apply.*Affinity", RegexOptions.IgnoreCase));
            Assert.DoesNotContain(methods, m => Regex.IsMatch(m, "Set.*Affinity", RegexOptions.IgnoreCase));
            Assert.DoesNotContain(methods, m => Regex.IsMatch(m, "Apply.*Cpu.*Selection", RegexOptions.IgnoreCase));
        }

        [Fact]
        public void MasksView_AllCoresProtectedDefaultInText()
        {
            var document = LoadMasksViewXaml();
            var serialized = document.ToString(SaveOptions.DisableFormatting);
            var locale = LoadEnglishLocale();

            Assert.Contains("MasksView_SelectCpusTip", serialized, StringComparison.Ordinal);
            Assert.Contains("All Cores is the protected default preset", locale, StringComparison.Ordinal);
        }

        private static XDocument LoadMasksViewXaml()
        {
            var repoRoot = GetRepositoryRoot();
            var path = Path.Combine(repoRoot, "Views", "MasksView.xaml");
            return XDocument.Load(path, LoadOptions.PreserveWhitespace);
        }

        private static string LoadEnglishLocale()
        {
            var repoRoot = GetRepositoryRoot();
            return File.ReadAllText(Path.Combine(repoRoot, "Locales", "en-US.xaml"));
        }

        private static string GetRepositoryRoot()
        {
            var currentDir = AppContext.BaseDirectory;
            var dir = new DirectoryInfo(currentDir);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ThreadPilot_1.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new InvalidOperationException("Could not find repository root from " + currentDir);
            }

            return dir.FullName;
        }
    }
}
