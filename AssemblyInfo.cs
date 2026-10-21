using System.Runtime.CompilerServices;
using System.Windows;

[assembly: InternalsVisibleTo("ThreadPilot.Core.Tests")]

// The app runs elevated: never resolve a P/Invoke DLL (powrprof, dwmapi...) from the app folder.
[assembly: System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            // where theme specific resource dictionaries are located
                                                // (used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly) // where the generic resource dictionary is located
                                               // (used if a resource is not found in the page,
                                               // app, or any theme specific resource dictionaries)
]

