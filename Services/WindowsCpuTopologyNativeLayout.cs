/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using ThreadPilot.Models;

    internal static class WindowsCpuTopologyNativeLayout
    {
        public static int GroupAffinitySize => Marshal.SizeOf<GroupAffinity>();

        public static int ProcessorGroupCountOffset => Marshal.OffsetOf<ProcessorRelationship>(nameof(ProcessorRelationship.GroupCount)).ToInt32();

        public static int ProcessorGroupMaskOffset => Marshal.OffsetOf<ProcessorRelationship>(nameof(ProcessorRelationship.GroupMask)).ToInt32();

        public static int CacheReservedOffset => Marshal.OffsetOf<CacheRelationship>(nameof(CacheRelationship.Reserved)).ToInt32();

        public static int CacheGroupCountOffset => Marshal.OffsetOf<CacheRelationship>(nameof(CacheRelationship.GroupCount)).ToInt32();

        public static int CacheGroupMaskOffset => Marshal.OffsetOf<CacheRelationship>(nameof(CacheRelationship.GroupMask)).ToInt32();

        public static int NumaReservedOffset => Marshal.OffsetOf<NumaNodeRelationship>(nameof(NumaNodeRelationship.Reserved)).ToInt32();

        public static int NumaGroupCountOffset => Marshal.OffsetOf<NumaNodeRelationship>(nameof(NumaNodeRelationship.GroupCount)).ToInt32();

        public static int NumaGroupMaskOffset => Marshal.OffsetOf<NumaNodeRelationship>(nameof(NumaNodeRelationship.GroupMask)).ToInt32();

        internal enum ProcessorCacheType
        {
            CacheUnified = 0,
            CacheInstruction = 1,
            CacheData = 2,
            CacheTrace = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GroupAffinity
        {
            public UIntPtr Mask;
            public ushort Group;
            public ushort Reserved0;
            public ushort Reserved1;
            public ushort Reserved2;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ProcessorRelationship
        {
            public byte Flags;
            public byte EfficiencyClass;
            public fixed byte Reserved[20];
            public ushort GroupCount;
            public GroupAffinity GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct CacheRelationship
        {
            public byte Level;
            public byte Associativity;
            public ushort LineSize;
            public uint CacheSize;
            public ProcessorCacheType Type;
            public fixed byte Reserved[18];
            public ushort GroupCount;
            public GroupAffinity GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct NumaNodeRelationship
        {
            public uint NodeNumber;
            public fixed byte Reserved[18];
            public ushort GroupCount;
            public GroupAffinity GroupMask;
        }

        public static IReadOnlyList<ProcessorRef> ReadProcessorRelationshipProcessors(IntPtr relationshipPtr, ushort groupCount)
        {
            return ReadProcessorsFromGroupMasks(relationshipPtr, ProcessorGroupMaskOffset, groupCount).ToList();
        }

        public static bool TryReadL3CacheProcessors(IntPtr relationshipPtr, out IReadOnlyList<ProcessorRef> processors)
        {
            var cache = Marshal.PtrToStructure<CacheRelationship>(relationshipPtr);
            if (cache.Level != 3 || cache.GroupCount == 0)
            {
                processors = [];
                return false;
            }

            processors = ReadProcessorsFromGroupMasks(relationshipPtr, CacheGroupMaskOffset, cache.GroupCount).ToList();
            return processors.Count > 0;
        }

        public static IReadOnlyList<ProcessorRef> ReadNumaNodeProcessors(IntPtr relationshipPtr, out int nodeNumber)
        {
            var numaNode = Marshal.PtrToStructure<NumaNodeRelationship>(relationshipPtr);
            nodeNumber = unchecked((int)numaNode.NodeNumber);
            var groupCount = numaNode.GroupCount == 0
                ? (ushort)1
                : numaNode.GroupCount;

            return ReadProcessorsFromGroupMasks(relationshipPtr, NumaGroupMaskOffset, groupCount).ToList();
        }

        public static IEnumerable<ProcessorRef> CreateFallbackProcessors(int logicalProcessorCount)
        {
            return Enumerable.Range(0, logicalProcessorCount)
                .Select(index => new ProcessorRef((ushort)(index / 64), (byte)(index % 64), index));
        }

        private static IEnumerable<ProcessorRef> ReadProcessorsFromGroupMasks(
            IntPtr relationshipPtr,
            int groupMaskOffset,
            ushort groupCount)
        {
            var firstGroupMaskPtr = IntPtr.Add(relationshipPtr, groupMaskOffset);
            var stride = GroupAffinitySize;
            for (var index = 0; index < groupCount; index++)
            {
                var groupAffinity = Marshal.PtrToStructure<GroupAffinity>(IntPtr.Add(firstGroupMaskPtr, index * stride));
                foreach (var logicalProcessor in ReadProcessorsFromGroupAffinity(groupAffinity))
                {
                    yield return logicalProcessor;
                }
            }
        }

        private static IEnumerable<ProcessorRef> ReadProcessorsFromGroupAffinity(GroupAffinity groupAffinity)
        {
            var mask = groupAffinity.Mask.ToUInt64();
            for (var bit = 0; bit < 64; bit++)
            {
                if ((mask & (1UL << bit)) != 0)
                {
                    yield return CreateProcessorRef(groupAffinity.Group, (byte)bit);
                }
            }
        }

        public static ProcessorRef CreateProcessorRef(ushort group, byte logicalProcessorNumber)
        {
            return new ProcessorRef(group, logicalProcessorNumber, (group * 64) + logicalProcessorNumber);
        }
    }
}
