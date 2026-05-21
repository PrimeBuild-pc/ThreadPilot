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
namespace ThreadPilot.ViewModels
{
    using System;

    public interface IDiagnosticsViewModelProvider
    {
        bool IsCreated { get; }

        PerformanceViewModel GetOrCreate();
    }

    public sealed class DiagnosticsViewModelProvider : IDiagnosticsViewModelProvider
    {
        private readonly Lazy<PerformanceViewModel> performanceViewModel;

        public DiagnosticsViewModelProvider(Lazy<PerformanceViewModel> performanceViewModel)
        {
            this.performanceViewModel = performanceViewModel ?? throw new ArgumentNullException(nameof(performanceViewModel));
        }

        public bool IsCreated => this.performanceViewModel.IsValueCreated;

        public PerformanceViewModel GetOrCreate() => this.performanceViewModel.Value;
    }
}
