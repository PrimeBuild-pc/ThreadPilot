/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public class RetryPolicy
    {
        public int MaxAttempts { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);
        public double BackoffMultiplier { get; set; } = 2.0;
        public Func<Exception, bool>? ShouldRetry { get; set; }
    }

    /// <summary>
    /// Service for implementing retry policies with exponential backoff
    /// </summary>
    public interface IRetryPolicyService
    {
        /// <summary>
        /// Execute an operation with retry policy
        /// </summary>
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, RetryPolicy? policy = null);

        /// <summary>
        /// Execute an operation with retry policy (no return value)
        /// </summary>
        Task ExecuteAsync(Func<Task> operation, RetryPolicy? policy = null);

        /// <summary>
        /// Create a default retry policy for process operations
        /// </summary>
        RetryPolicy CreateProcessOperationPolicy();

        /// <summary>
        /// Create a default retry policy for WMI operations
        /// </summary>
        RetryPolicy CreateWmiOperationPolicy();

        /// <summary>
        /// Create a default retry policy for file operations
        /// </summary>
        RetryPolicy CreateFileOperationPolicy();
    }
}

