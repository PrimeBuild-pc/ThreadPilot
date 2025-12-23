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
using ThreadPilot.Tests;

namespace ThreadPilot
{
    /// <summary>
    /// Simple test runner for CPU topology functionality
    /// </summary>
    public static class TestRunner
    {
        /// <summary>
        /// Main test entry point
        /// </summary>
        public static async Task RunTests()
        {
            Console.WriteLine("ThreadPilot CPU Topology Test Runner");
            Console.WriteLine("====================================");
            
            try
            {
                await CpuTopologyServiceTests.TestCpuTopologyDetection();

                Console.WriteLine();

                // Run Process Selection Test
                var processSelectionTest = new ProcessSelectionTest();
                await processSelectionTest.RunAllTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}

