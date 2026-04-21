/*
 * ThreadPilot - process execution seam.
 */
namespace ThreadPilot.Services.Abstractions
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs external processes with a bounded timeout and captured output.
    /// </summary>
    public interface IProcessRunner
    {
        /// <summary>
        /// Executes a process and returns its exit code with captured output streams.
        /// </summary>
        /// <param name="fileName">Executable path.</param>
        /// <param name="arguments">Argument list passed verbatim to the process.</param>
        /// <param name="timeout">Maximum execution time before the process is treated as timed out.</param>
        /// <returns>The captured process result.</returns>
        Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout);
    }

    /// <summary>
    /// Immutable result returned by <see cref="IProcessRunner"/>.
    /// </summary>
    /// <param name="ExitCode">Process exit code, or a synthetic failure code when launch/timeout fails.</param>
    /// <param name="StandardOutput">Captured standard output.</param>
    /// <param name="StandardError">Captured standard error.</param>
    public readonly record struct ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
}
