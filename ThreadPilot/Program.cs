using System;

namespace ThreadPilot
{
    /// <summary>
    /// Application entry point
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                App app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                // Log the error to a file 
                Console.Error.WriteLine($"Fatal error: {ex}");
            }
        }
    }
}