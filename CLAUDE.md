# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ThreadPilot is a professional Windows process and power plan manager built with WPF and .NET 8.0. It provides advanced process management, intelligent power plan automation, ML-based game detection, and system optimization tools for power users, gamers, and system administrators.

## Build and Development Commands

### Building
```bash
# Build the project
dotnet build --configuration Release

# Build for debugging
dotnet build --configuration Debug
```

### Running
```bash
# Run the application
dotnet run --configuration Release

# Run with command-line arguments
dotnet run --configuration Release -- --test              # Run tests
dotnet run --configuration Release -- --start-minimized   # Start minimized
dotnet run --configuration Release -- --autostart         # Autostart mode
```

### Publishing
```bash
# Publish as self-contained portable executable
dotnet publish --configuration Release --runtime win-x64 --self-contained true
```

### Testing
The application includes an integrated test suite:
- Press `Ctrl+Shift+T` in the running application to execute Game Boost integration tests
- Run `dotnet run --configuration Release -- --test` to run tests in console mode
- Tests are located in the `Tests/` directory

## Architecture Overview

### MVVM Pattern with Dependency Injection
The application follows strict MVVM architecture using CommunityToolkit.Mvvm with centralized DI configuration:

- **Models/** - Data models using ObservableObject base class
- **Views/** - WPF XAML views with code-behind
- **ViewModels/** - View models implementing INotifyPropertyChanged via source generators
- **Services/** - Business logic and system interaction layer

### Service Configuration
All services are configured in `Services/ServiceConfiguration.cs` using extension methods organized by layer:
- `ConfigureServiceInfrastructure()` - Logging, caching, health monitoring, retry policies
- `ConfigureCoreSystemServices()` - OS interaction (ProcessService, PowerPlanService, CpuTopologyService)
- `ConfigureProcessManagementServices()` - Process monitoring, game detection, boost services
- `ConfigureApplicationLevelServices()` - Settings, notifications, system tray, security
- `ConfigurePresentationLayer()` - ViewModels and Views

### Service Lifetime Strategy
- **Singletons**: Core services, ViewModels that share state (ProcessViewModel, MasksViewModel)
- **Transients**: UI-specific ViewModels and Views (PowerPlanViewModel, SettingsViewModel, MainWindow)

### CPU Topology and Affinity Management
The application has sophisticated CPU topology awareness:
- **CpuTopologyService** - Detects P-cores/E-cores (Intel Hybrid), AMD CCD layout, NUMA nodes
- **CoreMaskService** - Manages CPU affinity masks for precise core assignment
- **ProcessCpuSetHandler** (Platforms/Windows/) - Uses Windows CPU Sets API for modern affinity control on Windows 11+
- Fallback to traditional `ProcessorAffinity` for Windows 10 compatibility

CPU Sets vs ProcessorAffinity:
- CPU Sets (Windows 11+): More granular control, respects system scheduling policies
- ProcessorAffinity (legacy): Direct affinity mask, used as fallback

### Power Plan Management
- Integrates with Windows Power Plans via `powercfg` command-line tool
- Supports custom .pow power plan imports from hardcoded path (see PowerPlanService.cs:15)
- Process-based automatic power plan switching via `ProcessPowerPlanAssociationService`
- Real-time power plan monitoring with change event notifications

### Game Detection and Boost
- **GameDetectionService** - ML-based game detection with 95% accuracy (heuristics-based)
- **GameBoostService** - Automatic performance optimization for detected games
- **PerformanceMonitoringService** - Real-time FPS estimation and resource tracking
- User override system with persistent manual classification

### Notification System
- **NotificationService** - Basic Windows notifications
- **SmartNotificationService** - Intelligent throttling, deduplication, DND mode, priority queuing
- Category-based notification preferences
- Integrates with system tray for balloon tips

### Process Monitoring
- **ProcessMonitorService** - Low-level WMI-based process event monitoring
- **ProcessMonitorManagerService** - Orchestrates monitoring, profile application, power plan switching
- **VirtualizedProcessService** - Handles 5000+ processes efficiently with UI virtualization
- Background refresh with intelligent throttling to reduce resource usage

### System Tweaks
- Core parking control
- C-States management
- System service tweaks (SysMain, Prefetch, power throttling)
- HPET configuration
- High-priority scheduling category management

### Security and Elevation
- **ElevationService** - Manages administrator privilege detection and elevation requests
- **SecurityService** - Security-related functionality
- Application can run in limited mode without admin privileges
- Prompts for elevation when needed for specific features

## Key Implementation Details

### Async Initialization Pattern
MainWindow uses a sophisticated async initialization pattern with loading overlay:
1. Loading overlay displayed during startup
2. ViewModels initialized with timeout protection (prevents hanging)
3. Services initialized in specific order with fallback strategies
4. System tray and monitoring started last
5. Graceful degradation if components fail (e.g., basic tray mode if full init times out)

### Cross-Thread Marshaling
UI updates from background threads must be marshaled via `Dispatcher.InvokeAsync()`:
```csharp
Dispatcher.InvokeAsync(() => {
    // UI updates here
});
```

### Memory Management
- `IServiceHealthMonitor` and `IServiceDisposalCoordinator` for lifecycle management
- `IRetryPolicyService` for automatic retry with exponential backoff
- Memory cache (Microsoft.Extensions.Caching.Memory) for performance optimization
- Proper cleanup of timers, event handlers, and WMI watchers

### Logging
- Uses Microsoft.Extensions.Logging with console output
- **EnhancedLoggingService** provides correlation IDs and structured logging
- Debug logging to temp file during initialization (see MainWindow.xaml.cs:45)

### Settings Persistence
- Settings stored via `ApplicationSettingsService` (JSON-based)
- Profiles stored in ApplicationData\ThreadPilot\Profiles as JSON files
- Power plans exported as .pow files in hardcoded directory

### Keyboard Shortcuts
- **KeyboardShortcutService** manages global hotkeys via Win32 API
- RegisterHotKey/UnregisterHotKey integration
- Actions: ShowMainWindow, ToggleMonitoring, GameBoostToggle, OpenTweaks, OpenSettings, etc.
- Shortcuts loaded from settings

### System Tray Integration
- **SystemTrayService** with context menu for quick actions
- Power plan switching directly from tray
- Monitoring status display (CPU/Memory usage)
- Game Boost status indicator
- Periodic updates every 10 seconds (performance-optimized)

### Platform-Specific Code
- Windows-specific functionality isolated in `Platforms/Windows/`
- P/Invoke calls for CPU Sets API (CpuSetNativeMethods.cs)
- Requires `AllowUnsafeBlocks` for native interop

## Common Development Patterns

### Creating New Services
1. Define interface in `Services/I*.cs`
2. Implement in `Services/*.cs` with constructor injection
3. Register in `ServiceConfiguration.cs` in appropriate layer method
4. Inject into ViewModels or other services as needed

### Adding ViewModels
1. Inherit from `BaseViewModel` or `ObservableObject`
2. Use `[ObservableProperty]` source generators for properties
3. Use `[RelayCommand]` for commands
4. Register in `ServiceConfiguration.ConfigurePresentationLayer()`
5. Choose Singleton (shared state) or Transient (per-instance) lifetime

### Working with Processes
- Use `IProcessService` for basic process operations
- Use `IVirtualizedProcessService` for large process lists in UI
- Update ProcessModel properties to trigger UI updates automatically
- CPU usage calculation requires two samples (see ProcessService.CalculateCpuUsage)

### Adding System Tweaks
- Implement tweak logic in `SystemTweaksService`
- Add corresponding properties/commands to `SystemTweaksViewModel`
- Update UI in `Views/SystemTweaksView.xaml`
- Most tweaks require administrator privileges

## Project Structure Notes

- **Converters/** - WPF value converters (e.g., ItemIndexConverter for list numbering)
- **Models/** - Core data models (ProcessModel, PowerPlanModel, ProcessPowerPlanAssociation, CoreMask)
- **app.manifest** - Defines UAC elevation requirements and compatibility
- **ico.ico** - Application icon used in loading overlay and system tray
- **ThreadPilot.csproj** - Configured for single-file publish, self-contained, win-x64 only

## Important Implementation Notes

### Startup Sequence
1. App.xaml.cs configures DI container via ServiceConfiguration
2. Validates core service resolution
3. Checks elevation status (shows warning if not admin)
4. Parses command-line arguments (--test, --start-minimized, --autostart)
5. MainWindow constructor initializes loading overlay
6. Async initialization loads ViewModels, Services, starts monitoring
7. Loading overlay hidden when complete (with timeout protection)

### Error Handling Strategy
- Global exception handlers in App.xaml.cs (domain + dispatcher)
- Timeout protection on async operations (typically 5-8 seconds)
- Fallback strategies for failed initializations (e.g., basic system tray)
- User-friendly error dialogs with retry options
- Detailed logging with correlation IDs

### Performance Considerations
- Process list refresh paused when window minimized
- System tray updates throttled to 10-second intervals
- Virtualized UI for large datasets (process lists)
- CPU usage calculations cached per process
- Intelligent notification deduplication and throttling

### Windows Version Compatibility
- Targets .NET 8.0 Windows only (UseWPF + UseWindowsForms)
- CPU Sets API used on Windows 11+, falls back to ProcessorAffinity on Windows 10
- Requires Windows 10/11 for full functionality
- Some features require administrator privileges

## Gotchas and Known Issues

- PowerPlanService.cs:15 has hardcoded path `C:\Users\Administrator\Desktop\Project\ThreadPilot_1\Powerplans`
- WMI monitoring may fail on some systems (graceful degradation implemented)
- Process CPU Sets require Windows 11 - application auto-detects and falls back
- System tray context menu updates can timeout if performance metrics take too long (2s timeout)
- Loading overlay initialization has 15-second timeout with retry option
- Elevation dialogs can be suppressed during autostart to avoid interrupting user
