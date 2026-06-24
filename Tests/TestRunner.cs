namespace ThreadPilot.Tests
{
    using System;
    using System.Threading.Tasks;

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
