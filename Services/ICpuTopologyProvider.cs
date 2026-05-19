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
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    /// <summary>
    /// Provides a topology-aware CPU snapshot without applying runtime affinity changes.
    /// </summary>
    public interface ICpuTopologyProvider
    {
        /// <summary>
        /// Gets a current CPU topology snapshot.
        /// </summary>
        Task<CpuTopologySnapshot> GetTopologySnapshotAsync(CancellationToken cancellationToken = default);
    }
}
