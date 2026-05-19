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
    /// <summary>
    /// Topology-aware CPU affinity preset generated from a CPU topology snapshot.
    /// </summary>
    public sealed record CpuPreset
    {
        public string PresetId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public CpuSelection Selection { get; init; } = new();

        public string Reason { get; init; } = string.Empty;

        public string? Warning { get; init; }

        public CpuTopologySignature? GeneratedByTopologySignature { get; init; }

        public bool IsUserEditable { get; init; } = true;

        public bool IsGenerated { get; init; } = true;

        public bool ReviewRequired { get; init; }
    }
}
