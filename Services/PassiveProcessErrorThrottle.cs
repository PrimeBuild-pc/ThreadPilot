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
    using System.Collections.Concurrent;

    public enum PassiveProcessErrorKind
    {
        AccessDenied,
        Terminated,
        Unknown,
    }

    public interface IPassiveProcessErrorThrottle
    {
        bool ShouldLog(int processId, PassiveProcessErrorKind errorKind);
    }

    public sealed class PassiveProcessErrorThrottle : IPassiveProcessErrorThrottle
    {
        private readonly ConcurrentDictionary<(int ProcessId, PassiveProcessErrorKind ErrorKind), DateTimeOffset> lastLogByError = new();
        private readonly Func<DateTimeOffset> nowProvider;
        private readonly TimeSpan ttl;

        public PassiveProcessErrorThrottle()
            : this(TimeSpan.FromMinutes(5), () => DateTimeOffset.UtcNow)
        {
        }

        public PassiveProcessErrorThrottle(TimeSpan ttl, Func<DateTimeOffset> nowProvider)
        {
            if (ttl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be greater than zero.");
            }

            this.ttl = ttl;
            this.nowProvider = nowProvider ?? throw new ArgumentNullException(nameof(nowProvider));
        }

        public bool ShouldLog(int processId, PassiveProcessErrorKind errorKind)
        {
            var now = this.nowProvider();
            var key = (processId, errorKind);

            if (this.lastLogByError.TryGetValue(key, out var lastLog) && now - lastLog < this.ttl)
            {
                return false;
            }

            this.lastLogByError[key] = now;
            return true;
        }
    }
}
