/*
 * ThreadPilot - Windows process memory priority native integration tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System;
    using System.Runtime.Versioning;
    using ThreadPilot.Platforms.Windows;

    [SupportedOSPlatform("windows10.0.22000.0")]
    public sealed class ProcessMemoryPriorityNativeIntegrationTests
    {
        [Fact]
        public void AcquirePrivilegeScope_WithSeIncreaseBasePriorityPrivilege_RestoresOriginalState()
        {
            const string privilegeName = ProcessMemoryPriorityNativeMethods.SeIncreaseBasePriorityPrivilege;
            bool originalEnabled = ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName);

            using (var scope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope(privilegeName))
            {
                Assert.NotNull(scope);
                if (ProcessMemoryPriorityNativeMethods.IsPrivilegePresent(privilegeName))
                {
                    Assert.True(ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName));
                }
            }

            bool restoredEnabled = ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName);
            Assert.Equal(originalEnabled, restoredEnabled);
        }

        [Fact]
        public void AcquirePrivilegeScope_WithPresentPrivilege_EnablesDuringScopeAndRestoresAfterDisposal()
        {
            const string privilegeName = "SeShutdownPrivilege";
            if (!ProcessMemoryPriorityNativeMethods.IsPrivilegePresent(privilegeName))
            {
                return;
            }

            bool originalEnabled = ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName);

            using (var scope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope(privilegeName))
            {
                Assert.NotNull(scope);
                Assert.True(ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName));
            }

            bool restoredEnabled = ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName);
            Assert.Equal(originalEnabled, restoredEnabled);
        }

        [Fact]
        public void AcquirePrivilegeScope_WithOverlappingCallers_KeepsPrivilegeUntilLastScopeDisposes()
        {
            const string privilegeName = "SeShutdownPrivilege";
            if (!ProcessMemoryPriorityNativeMethods.IsPrivilegePresent(privilegeName))
            {
                return;
            }

            bool originalEnabled = ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName);

            using (var outerScope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope(privilegeName))
            {
                Assert.NotNull(outerScope);
                Assert.True(ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName));

                using (var innerScope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope(privilegeName))
                {
                    Assert.NotNull(innerScope);
                    Assert.True(ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName));
                }

                // Inner scope disposed, but outer scope still active:
                // Privilege must remain enabled for overlapping callers.
                Assert.True(ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName));
            }

            // Both scopes disposed: privilege must be restored to original state.
            bool finalEnabled = ProcessMemoryPriorityNativeMethods.IsPrivilegeEnabled(privilegeName);
            Assert.Equal(originalEnabled, finalEnabled);
        }

        [Fact]
        public void AcquirePrivilegeScope_WithNullOrWhitespace_ReturnsSafeDisposableWithoutThrowing()
        {
            using (var nullScope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope(null!))
            {
                Assert.NotNull(nullScope);
            }

            using (var emptyScope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope(string.Empty))
            {
                Assert.NotNull(emptyScope);
            }

            using (var whitespaceScope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope("   "))
            {
                Assert.NotNull(whitespaceScope);
            }
        }

        [Fact]
        public void AcquirePrivilegeScope_WithNonExistentPrivilege_ReturnsSafeDisposableWithoutThrowing()
        {
            using var scope = ProcessMemoryPriorityNativeMethods.AcquirePrivilegeScope("SeNonExistentPrivilegeThatDoesNotExist");
            Assert.NotNull(scope);
        }

        [Fact]
        public void ProcessMemoryPriorityNativeApi_Instance_ProvidesWorkingPrivilegeScopeAndMethods()
        {
            var api = ProcessMemoryPriorityNativeApi.Instance;
            Assert.NotNull(api);
            Assert.True(api.IsSupported);

            using var scope = api.AcquirePrivilegeScope(ProcessMemoryPriorityNativeMethods.SeIncreaseBasePriorityPrivilege);
            Assert.NotNull(scope);
        }

        [Fact]
        public void ProcessMemoryPriorityNativeApi_DefaultInterfaceMethods_ReturnSafeDefaults()
        {
            IProcessMemoryPriorityNativeApi defaultApi = new MinimalTestNativeApi();

            int priority = 0;
            int queryResult = defaultApi.QueryIoPriority(null!, ref priority, out uint returnLength);
            Assert.Equal(unchecked((int)0xC0000002), queryResult);
            Assert.Equal(0u, returnLength);

            int setResult = defaultApi.SetIoPriority(null!, ref priority);
            Assert.Equal(unchecked((int)0xC0000002), setResult);

            var scope = defaultApi.AcquirePrivilegeScope("TestPrivilege");
            Assert.Null(scope);
        }

        private sealed class MinimalTestNativeApi : IProcessMemoryPriorityNativeApi
        {
            public bool IsSupported => true;

            public Microsoft.Win32.SafeHandles.SafeProcessHandle OpenProcess(
                ProcessAccessFlags access,
                bool inheritHandle,
                uint processId) => new(IntPtr.Zero, ownsHandle: false);

            public bool GetProcessInformation(
                Microsoft.Win32.SafeHandles.SafeProcessHandle process,
                ProcessInformationClass processInformationClass,
                ref MemoryPriorityInformation processInformation,
                uint processInformationSize) => false;

            public bool SetProcessInformation(
                Microsoft.Win32.SafeHandles.SafeProcessHandle process,
                ProcessInformationClass processInformationClass,
                ref MemoryPriorityInformation processInformation,
                uint processInformationSize) => false;

            public int GetLastWin32Error() => 0;
        }
    }
}
