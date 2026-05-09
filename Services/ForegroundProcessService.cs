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
    using Microsoft.Extensions.Logging;

    public readonly record struct ForegroundWindowSnapshot(
        IntPtr WindowHandle,
        int ProcessId,
        bool IsVisible,
        bool IsCloaked);

    public interface IForegroundWindowProvider
    {
        bool TryGetForegroundWindow(out ForegroundWindowSnapshot snapshot);
    }

    public interface IForegroundProcessService
    {
        int? TryGetForegroundProcessId();
    }

    public sealed class ForegroundProcessService : IForegroundProcessService
    {
        private readonly IForegroundWindowProvider foregroundWindowProvider;
        private readonly ILogger<ForegroundProcessService> logger;

        public ForegroundProcessService(
            IForegroundWindowProvider foregroundWindowProvider,
            ILogger<ForegroundProcessService> logger)
        {
            this.foregroundWindowProvider = foregroundWindowProvider ?? throw new ArgumentNullException(nameof(foregroundWindowProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int? TryGetForegroundProcessId()
        {
            try
            {
                if (!this.foregroundWindowProvider.TryGetForegroundWindow(out var snapshot))
                {
                    return null;
                }

                if (snapshot.ProcessId <= 0 || !snapshot.IsVisible || snapshot.IsCloaked)
                {
                    return null;
                }

                return snapshot.ProcessId;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Foreground process detection failed");
                return null;
            }
        }
    }
}
