/*
 * ThreadPilot - retry policy service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;

    /// <summary>
    /// Unit tests for <see cref="RetryPolicyService"/> behavior.
    /// </summary>
    public sealed class RetryPolicyServiceTests
    {
        /// <summary>
        /// Ensures the retry loop retries transient failures and eventually returns success.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_RetriesTransientErrors_ThenSucceeds()
        {
            var service = new RetryPolicyService(NullLogger<RetryPolicyService>.Instance);
            var attempts = 0;

            var policy = new RetryPolicy
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.Zero,
                MaxDelay = TimeSpan.Zero,
                BackoffMultiplier = 1,
                ShouldRetry = _ => true,
            };

            var result = await service.ExecuteAsync(
                async () =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw new InvalidOperationException("transient");
                    }

                    return "ok";
                },
                policy);

            Assert.Equal("ok", result);
            Assert.Equal(3, attempts);
        }

        /// <summary>
        /// Ensures non-retriable failures are surfaced immediately.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_DoesNotRetry_WhenPredicateRejectsException()
        {
            var service = new RetryPolicyService(NullLogger<RetryPolicyService>.Instance);
            var attempts = 0;

            var policy = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialDelay = TimeSpan.Zero,
                MaxDelay = TimeSpan.Zero,
                BackoffMultiplier = 1,
                ShouldRetry = _ => false,
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await service.ExecuteAsync<string>(
                    async () =>
                    {
                        attempts++;
                        throw new UnauthorizedAccessException("denied");
                    },
                    policy);
            });

            Assert.Equal(1, attempts);
        }
    }
}
