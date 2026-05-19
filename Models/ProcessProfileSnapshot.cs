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
namespace ThreadPilot.Models
{
    using System.Diagnostics;

    public sealed class ProcessProfileSnapshot
    {
        public int ProfileSchemaVersion { get; set; } = CpuAffinityProfileSchemaVersions.Legacy;

        public string ProcessName { get; set; } = string.Empty;

        public ProcessPriorityClass Priority { get; set; }

        public long ProcessorAffinity { get; set; }

        public CpuSelection? CpuSelection { get; set; }

        public CpuSelectionMigrationMetadata? CpuSelectionMigration { get; set; }
    }
}
