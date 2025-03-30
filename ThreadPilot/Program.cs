using System;

namespace ThreadPilot
{
    /// <summary>
    /// Application entry point
    /// </summary>
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            // Create and run the application
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}