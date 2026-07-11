namespace ThreadPilot.Core.Tests
{
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

    public sealed class BundledPowerPlanAssetTests
    {
        private static readonly string[] NewPlanFiles =
        [
            "0 Synez_Public_Power.pow",
            "arsenza low latency (INTEL FIX THREAD-DIRECTOR).pow",
            "arsenza low latency.pow",
            "AutoOS.pow",
            "BEYOND-PERFORMANCE-AMD+INTEL.pow",
            "Bitsum Highest Performance.pow",
            "cactusOS.pow",
            "FPSHEAVEN2026.pow",
            "GALA's ultimate performance(AMD).pow",
            "Gavot Performance.pow",
            "GTweaks Power Plan V3.pow",
            "imribiy2026.pow",
            "IrisFixed.pow",
            "J o k r O S   P o w e r   P l a n.pow",
            "Jackpot2026.pow",
            "Kizzimo's Extreme Low Latency.pow",
            "KSOS11.pow",
            "melody LowestLatency.pow",
            "Microsoft High performance.pow",
            "Microsoft Ultimate Performance.pow",
            "Mitstas IDLE ENABLED.pow",
            "n1kobg GPU_Booster_Power_Plan.pow",
            "Prodazin Power Plan.pow",
            "Reticle v2.pow",
            "RevisionPowerPlanV2.8.pow",
            "RIP Tweaks Power Plan.pow",
            "Rosca Tweaks v2.pow",
            "Velo's Power Plan.pow",
            "VTRL Optimized.pow",
            "XNRL Pro Plan.pow",
        ];

        [Fact]
        public void BundledPlans_AreValidUniqueRegistryHives()
        {
            var directory = GetPowerPlansDirectory();
            var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);

            Assert.All(files, file => Assert.Equal(".pow", Path.GetExtension(file), ignoreCase: true));
            Assert.Equal(files.Length, files.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                Assert.InRange(info.Length, 4, 10 * 1024 * 1024);
                Assert.Equal("regf", Encoding.ASCII.GetString(File.ReadAllBytes(file), 0, 4));
                Assert.DoesNotContain(Path.GetFileName(file), character => Path.GetInvalidFileNameChars().Contains(character));
                Assert.DoesNotContain("#U", Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task NewAndExistingPlans_ArePresentDiscoverableAndNotBinaryDuplicates()
        {
            var directory = GetPowerPlansDirectory();
            var files = Directory.GetFiles(directory, "*.pow", SearchOption.TopDirectoryOnly);
            var newPaths = NewPlanFiles.Select(file => Path.Combine(directory, file)).ToArray();

            Assert.All(newPaths, path => Assert.True(File.Exists(path), $"Missing bundled power plan: {Path.GetFileName(path)}"));
            Assert.All(["Amit.pow", "Beyond.pow", "L1 Final Version.pow"], file => Assert.True(File.Exists(Path.Combine(directory, file))));

            var newHashes = newPaths.ToDictionary(path => path, GetSha256);
            Assert.Equal(newHashes.Count, newHashes.Values.Distinct(StringComparer.Ordinal).Count());
            Assert.All(
                newHashes,
                entry => Assert.DoesNotContain(files, file => !newPaths.Contains(file, StringComparer.OrdinalIgnoreCase) && GetSha256(file) == entry.Value));

            var logger = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            var runner = new Mock<IProcessRunner>(MockBehavior.Strict);
            var service = new PowerPlanService(
                NullLogger<PowerPlanService>.Instance,
                logger.Object,
                runner.Object,
                () => directory);

            var discovered = await service.GetCustomPowerPlansAsync();
            Assert.All(NewPlanFiles, file => Assert.Contains(discovered, plan => plan.Name == Path.GetFileNameWithoutExtension(file)));
            Assert.Equal(files.Length, discovered.Count);
        }

        [Fact]
        public void Project_ConfiguresAllPowerPlansForBuildAndPublish()
        {
            var project = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "ThreadPilot.csproj"));

            Assert.Contains(@"assets\Powerplans\**\*.pow", project, StringComparison.Ordinal);
            Assert.Contains(@"<TargetPath>Powerplans\%(RecursiveDir)%(Filename)%(Extension)</TargetPath>", project, StringComparison.Ordinal);
            Assert.Contains("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", project, StringComparison.Ordinal);
            Assert.Contains("<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>", project, StringComparison.Ordinal);
        }

        private static string GetPowerPlansDirectory() =>
            Path.Combine(FindRepositoryRoot(), "assets", "Powerplans");

        private static string GetSha256(string path) =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ThreadPilot.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root could not be located.");
        }
    }
}
