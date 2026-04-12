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
using System;
using System.Threading.Tasks;

namespace ThreadPilot.Tests
{
    /// <summary>
    /// Integrated runtime test runner used by --test mode.
    /// </summary>
    public static class TestRunner
    {
        public static async Task RunTests()
        {
            Console.WriteLine("ThreadPilot Integrated Test Runner");
            Console.WriteLine("================================");

            try
            {
                await CpuTopologyServiceTests.TestCpuTopologyDetection();
                Console.WriteLine();

                var processSelectionTest = new ProcessSelectionTest();
                await processSelectionTest.RunAllTests();
                Console.WriteLine();

                var executableBrowseTest = new ExecutableBrowseTest();
                var browsePassed = executableBrowseTest.RunAllTests();
                Console.WriteLine();

                Console.WriteLine(browsePassed
                    ? "Integrated tests completed."
                    : "Integrated tests completed with failures.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test runner failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
