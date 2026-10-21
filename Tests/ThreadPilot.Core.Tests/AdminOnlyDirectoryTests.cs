namespace ThreadPilot.Core.Tests
{
    using System.Security.AccessControl;
    using System.Security.Principal;
    using ThreadPilot.Services;

    public sealed class AdminOnlyDirectoryTests
    {
        [Fact]
        public void Create_GrantsOnlyAdministratorsAndSystem_WithoutInheritingTheParentAcl()
        {
            var path = Path.Combine(Path.GetTempPath(), $"threadpilot-acl-{Guid.NewGuid():N}");
            try
            {
                AdminOnlyDirectory.Create(path);

                var security = new DirectoryInfo(path).GetAccessControl();
                Assert.True(security.AreAccessRulesProtected);

                var allowed = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>()
                    .Select(rule => (SecurityIdentifier)rule.IdentityReference)
                    .ToHashSet();
                var expected = new HashSet<SecurityIdentifier>
                {
                    new(WellKnownSidType.LocalSystemSid, null),
                    new(WellKnownSidType.BuiltinAdministratorsSid, null),
                };
                using var identity = WindowsIdentity.GetCurrent();
                if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    Assert.Equal(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), security.GetOwner(typeof(SecurityIdentifier)));
                }
                else
                {
                    expected.Add(identity.User!);
                }

                Assert.Equal(expected, allowed);
            }
            finally
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
        }

        [Fact]
        public void Create_RefusesAnExistingFolder()
        {
            var path = Directory.CreateTempSubdirectory("threadpilot-acl-").FullName;
            try
            {
                Assert.Throws<IOException>(() => AdminOnlyDirectory.Create(path));
            }
            finally
            {
                Directory.Delete(path, recursive: true);
            }
        }

        [Fact]
        public void UpdateFolders_AreCreatedDirectlyUnderTheRoot_AndOnlyThoseAreTrusted()
        {
            var root = Directory.CreateTempSubdirectory("threadpilot-updates-").FullName;
            try
            {
                var provider = new UpdateTempDirectoryProvider(root);

                var created = provider.CreateUpdateTempDirectory(new SemanticVersion(1, 8, 0));

                Assert.Equal(root, Path.GetDirectoryName(created));
                Assert.True(provider.IsSafeUpdateTempPath(Path.Combine(created, "ThreadPilot_v1.8.0_Setup.exe")));
                Assert.False(provider.IsSafeUpdateTempPath(Path.Combine(root, "other", "ThreadPilot_v1.8.0_Setup.exe")));
                Assert.False(provider.IsSafeUpdateTempPath(Path.Combine(created, "..", "..", "Setup.exe")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
