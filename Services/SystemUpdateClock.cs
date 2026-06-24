/*
 * ThreadPilot - updater clock abstraction.
 */
namespace ThreadPilot.Services
{
    using System;

    public sealed class SystemUpdateClock : IUpdateClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
