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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Tests
{
    /// <summary>
    /// Test runner for validating Game Boost functionality
    /// </summary>
    public static class TestRunner
    {
        /// <summary>
        /// Runs all Game Boost integration tests
        /// </summary>
        public static async Task<bool> RunGameBoostTestsAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<GameBoostIntegrationTest>>();
            
            try
            {
                logger.LogInformation("=== Starting Game Boost Integration Tests ===");

                var integrationTest = new GameBoostIntegrationTest(serviceProvider);
                var result = await integrationTest.RunIntegrationTestAsync();

                if (result)
                {
                    logger.LogInformation("=== All Game Boost Tests PASSED ===");
                }
                else
                {
                    logger.LogError("=== Game Boost Tests FAILED ===");
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Test runner failed with exception");
                return false;
            }
        }

        /// <summary>
        /// Validates that all required services are properly configured
        /// </summary>
        public static bool ValidateServiceConfiguration(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<GameBoostIntegrationTest>>();

            try
            {
                logger.LogInformation("Validating service configuration...");

                // Check all required services
                var requiredServices = new[]
                {
                    typeof(ThreadPilot.Services.IGameBoostService),
                    typeof(ThreadPilot.Services.ISystemTrayService),
                    typeof(ThreadPilot.Services.IProcessMonitorService),
                    typeof(ThreadPilot.Services.IApplicationSettingsService),
                    typeof(ThreadPilot.Services.INotificationService),
                    typeof(ThreadPilot.Services.IPowerPlanService),
                    typeof(ThreadPilot.Services.IProcessService)
                };

                foreach (var serviceType in requiredServices)
                {
                    var service = serviceProvider.GetService(serviceType);
                    if (service == null)
                    {
                        logger.LogError("Required service {ServiceType} is not registered", serviceType.Name);
                        return false;
                    }
                    logger.LogDebug("Service {ServiceType} is properly registered", serviceType.Name);
                }

                logger.LogInformation("Service configuration validation passed");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Service configuration validation failed");
                return false;
            }
        }
    }
}

