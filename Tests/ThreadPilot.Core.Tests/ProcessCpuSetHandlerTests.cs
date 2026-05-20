namespace ThreadPilot.Core.Tests
{
    using System;
    using Microsoft.Win32.SafeHandles;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;
    using ThreadPilot.Services;

    public sealed class ProcessCpuSetHandlerTests
    {
        [Fact]
        public void CpuSetMapping_KeepsSameLogicalProcessorIndexInDifferentGroupsDistinct()
        {
            var group0Cpu0 = new ProcessorRef(0, 0, 0);
            var group1Cpu0 = new ProcessorRef(1, 0, 64);
            var mapping = CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
            {
                [group0Cpu0] = 100,
                [group1Cpu0] = 200,
            });

            Assert.True(mapping.TryGetCpuSetId(group0Cpu0, out var group0CpuSetId));
            Assert.True(mapping.TryGetCpuSetId(group1Cpu0, out var group1CpuSetId));
            Assert.Equal(100U, group0CpuSetId);
            Assert.Equal(200U, group1CpuSetId);
            Assert.True(mapping.TryGetProcessorRef(100, out var group0Processor));
            Assert.True(mapping.TryGetProcessorRef(200, out var group1Processor));
            Assert.Equal(group0Cpu0, group0Processor);
            Assert.Equal(group1Cpu0, group1Processor);
        }

        [Fact]
        public void CpuSetMapping_Cpu64DoesNotSelectCpu0()
        {
            var group0Cpu0 = new ProcessorRef(0, 0, 0);
            var group1Cpu0 = new ProcessorRef(1, 0, 64);
            var topology = CpuTopologySnapshot.Create(
                [group0Cpu0, group1Cpu0],
                cpuSetIds: new Dictionary<ProcessorRef, uint>
                {
                    [group0Cpu0] = 100,
                    [group1Cpu0] = 200,
                });
            var selection = CpuSelection.FromProcessors([group1Cpu0], topology);
            var mapping = CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
            {
                [group0Cpu0] = 100,
                [group1Cpu0] = 200,
            });

            var cpuSetIds = mapping.ResolveCpuSetIds(selection);

            Assert.Equal([200U], cpuSetIds);
        }

        [Fact]
        public void CpuSetMapping_ResolveSelection_UsesExplicitCpuSetIds()
        {
            var mapping = CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
            {
                [new ProcessorRef(0, 0, 0)] = 100,
            });
            var selection = new CpuSelection
            {
                CpuSetIds = [300, 100, 300],
                LogicalProcessors = [new ProcessorRef(0, 0, 0)],
            };

            var cpuSetIds = mapping.ResolveCpuSetIds(selection);

            Assert.Equal([100U, 300U], cpuSetIds);
        }

        [Fact]
        public void CpuSetMapping_ResolveSelection_MapsProcessorRefsWhenCpuSetIdsAreMissing()
        {
            var cpu1 = new ProcessorRef(0, 1, 1);
            var mapping = CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
            {
                [new ProcessorRef(0, 0, 0)] = 100,
                [cpu1] = 101,
            });
            var selection = new CpuSelection
            {
                LogicalProcessors = [cpu1],
            };

            var cpuSetIds = mapping.ResolveCpuSetIds(selection);

            Assert.Equal([101U], cpuSetIds);
        }

        [Fact]
        public void CpuSetMapping_ResolveSelection_ReturnsEmptyWhenNoMappingExists()
        {
            var mapping = CpuSetMapping.Empty;
            var selection = new CpuSelection
            {
                LogicalProcessors = [new ProcessorRef(0, 1, 1)],
            };

            var cpuSetIds = mapping.ResolveCpuSetIds(selection);

            Assert.Empty(cpuSetIds);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelection_WithClearSelection_ClearsCpuSets()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(nativeApi, CpuSetMapping.Empty);

            var result = handler.ApplyCpuSelection(new CpuSelection(), clearSelection: true);

            Assert.True(result);
            Assert.Null(nativeApi.LastAppliedCpuSetIds);
            Assert.Equal(0U, nativeApi.LastAppliedCpuSetCount);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelection_WithClearSelectionAndNullSelection_ClearsCpuSets()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(nativeApi, CpuSetMapping.Empty);

            var result = handler.ApplyCpuSelection(null!, clearSelection: true);

            Assert.True(result);
            Assert.Null(nativeApi.LastAppliedCpuSetIds);
            Assert.Equal(0U, nativeApi.LastAppliedCpuSetCount);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelection_WithNullSelectionAndClearFalse_ThrowsArgumentNullException()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(nativeApi, CpuSetMapping.Empty);

            Assert.Throws<ArgumentNullException>(() =>
                handler.ApplyCpuSelection(null!, clearSelection: false));
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelection_WithExplicitCpuSetIds_AppliesThoseIds()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(nativeApi, CpuSetMapping.Empty);
            var selection = new CpuSelection
            {
                CpuSetIds = [400, 200, 400],
            };

            var result = handler.ApplyCpuSelection(selection);

            Assert.True(result);
            Assert.Equal([200U, 400U], nativeApi.LastAppliedCpuSetIds!);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelection_WithoutCpuSetIds_ResolvesProcessorRefs()
        {
            var cpu64 = new ProcessorRef(1, 0, 64);
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(
                nativeApi,
                CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
                {
                    [new ProcessorRef(0, 0, 0)] = 100,
                    [cpu64] = 200,
                }));
            var selection = new CpuSelection
            {
                LogicalProcessors = [cpu64],
            };

            var result = handler.ApplyCpuSelection(selection);

            Assert.True(result);
            Assert.Equal([200U], nativeApi.LastAppliedCpuSetIds!);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelection_WithoutResolvableCpuSets_ReturnsFalse()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(nativeApi, CpuSetMapping.Empty);
            var selection = new CpuSelection
            {
                LogicalProcessors = [new ProcessorRef(1, 0, 64)],
            };

            var result = handler.ApplyCpuSelection(selection);

            Assert.False(result);
            Assert.False(nativeApi.WasSetProcessDefaultCpuSetsCalled);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelectionDetailed_WithoutResolvableCpuSets_ReturnsInvalidTopology()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(nativeApi, CpuSetMapping.Empty);
            var selection = new CpuSelection
            {
                LogicalProcessors = [new ProcessorRef(1, 0, 64)],
            };

            var result = handler.ApplyCpuSelectionDetailed(selection);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.InvalidTopology, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.InvalidTopology, result.UserMessage);
            Assert.False(nativeApi.WasSetProcessDefaultCpuSetsCalled);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSelectionDetailed_WhenNativeAccessDenied_ReturnsAccessDenied()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi
            {
                SetProcessDefaultCpuSetsResult = false,
                LastWin32Error = 5,
            };
            using var handler = CreateHandler(nativeApi, CpuSetMapping.Empty);
            var selection = new CpuSelection
            {
                CpuSetIds = [400],
            };

            var result = handler.ApplyCpuSelectionDetailed(selection);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.AccessDenied, result.ErrorCode);
            Assert.Equal(5, result.Win32ErrorCode);
            Assert.True(result.IsAccessDenied);
            Assert.Equal(ProcessOperationUserMessages.AccessDenied, result.UserMessage);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSetMask_LegacySingleGroupMappingIsPreserved()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(
                nativeApi,
                CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
                {
                    [new ProcessorRef(0, 0, 0)] = 100,
                    [new ProcessorRef(0, 1, 1)] = 101,
                    [new ProcessorRef(1, 0, 64)] = 200,
                }));

            var result = handler.ApplyCpuSetMask(0b11);

            Assert.True(result);
            Assert.Equal([100U, 101U], nativeApi.LastAppliedCpuSetIds!);
        }

        [Fact]
        public void ProcessCpuSetHandler_ApplyCpuSetMask_LegacyCpu0BitDoesNotRepresentGroup1Cpu0()
        {
            var nativeApi = new FakeProcessCpuSetNativeApi();
            using var handler = CreateHandler(
                nativeApi,
                CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
                {
                    [new ProcessorRef(0, 0, 0)] = 100,
                    [new ProcessorRef(1, 0, 64)] = 200,
                }));

            var result = handler.ApplyCpuSetMask(0b1);

            Assert.True(result);
            Assert.Equal([100U], nativeApi.LastAppliedCpuSetIds!);
        }

        private static ProcessCpuSetHandler CreateHandler(
            FakeProcessCpuSetNativeApi nativeApi,
            CpuSetMapping mapping)
        {
            return new ProcessCpuSetHandler(1234, "test.exe", nativeApi, mapping);
        }

        private sealed class FakeProcessCpuSetNativeApi : IProcessCpuSetNativeApi
        {
            public bool WasSetProcessDefaultCpuSetsCalled { get; private set; }

            public uint[]? LastAppliedCpuSetIds { get; private set; }

            public uint LastAppliedCpuSetCount { get; private set; }

            public int LastWin32Error { get; set; }

            public bool SetProcessDefaultCpuSetsResult { get; init; } = true;

            public SafeProcessHandle OpenProcess(ProcessAccessFlags access, bool inheritHandle, uint processId)
            {
                return new SafeProcessHandle(new IntPtr(1), ownsHandle: false);
            }

            public bool SetProcessDefaultCpuSets(SafeProcessHandle process, uint[]? cpuSetIds, uint cpuSetIdCount)
            {
                this.WasSetProcessDefaultCpuSetsCalled = true;
                this.LastAppliedCpuSetIds = cpuSetIds;
                this.LastAppliedCpuSetCount = cpuSetIdCount;
                return this.SetProcessDefaultCpuSetsResult;
            }

            public bool GetProcessTimes(
                SafeProcessHandle process,
                out FILETIME creationTime,
                out FILETIME exitTime,
                out FILETIME kernelTime,
                out FILETIME userTime)
            {
                creationTime = default;
                exitTime = default;
                kernelTime = default;
                userTime = default;
                return false;
            }

            public bool GetSystemCpuSetInformation(
                IntPtr information,
                uint bufferLength,
                ref uint returnedLength,
                SafeProcessHandle process,
                uint flags)
            {
                returnedLength = 0;
                return false;
            }

            public int GetLastWin32Error()
            {
                return this.LastWin32Error;
            }
        }
    }
}
