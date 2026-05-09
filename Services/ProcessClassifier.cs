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
    using ThreadPilot.Models;

    public readonly record struct ProcessClassificationContext(
        int? ForegroundProcessId,
        bool AccessDenied = false,
        bool Terminated = false);

    public interface IProcessClassifier
    {
        ProcessClassification Classify(ProcessModel process, ProcessClassificationContext context);
    }

    public sealed class ProcessClassifier : IProcessClassifier
    {
        private readonly ProcessFilterService processFilterService;

        public ProcessClassifier(ProcessFilterService processFilterService)
        {
            this.processFilterService = processFilterService ?? throw new ArgumentNullException(nameof(processFilterService));
        }

        public ProcessClassification Classify(ProcessModel process, ProcessClassificationContext context)
        {
            ArgumentNullException.ThrowIfNull(process);

            if (context.Terminated)
            {
                return ProcessClassification.Terminated;
            }

            if (context.AccessDenied)
            {
                return ProcessClassification.ProtectedOrAccessDenied;
            }

            if (context.ForegroundProcessId == process.ProcessId)
            {
                return ProcessClassification.ForegroundApp;
            }

            if (this.processFilterService.IsSystemProcess(process))
            {
                return ProcessClassification.System;
            }

            if (process.HasVisibleWindow)
            {
                return ProcessClassification.VisibleWindowApp;
            }

            if (!string.IsNullOrWhiteSpace(process.Name))
            {
                return ProcessClassification.BackgroundUser;
            }

            return ProcessClassification.Unknown;
        }
    }
}
