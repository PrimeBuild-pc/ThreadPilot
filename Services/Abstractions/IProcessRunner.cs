/*
 * ThreadPilot - process execution seam.
 */
namespace ThreadPilot.Services.Abstractions
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IProcessRunner
    {
        Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout);
    }

    public readonly record struct ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
}
