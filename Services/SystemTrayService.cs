using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing system tray icon and context menu
    /// </summary>
    public class SystemTrayService : ISystemTrayService
    {
        private readonly ILogger<SystemTrayService> _logger;
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private ToolStripMenuItem? _quickApplyMenuItem;
        private ToolStripMenuItem? _selectedProcessMenuItem;
        private ToolStripMenuItem? _monitoringToggleMenuItem;
        private ToolStripMenuItem? _settingsMenuItem;
        private ToolStripMenuItem? _powerPlansMenuItem;
        private ToolStripMenuItem? _profilesMenuItem;
        private ToolStripMenuItem? _performanceMenuItem;
        private ToolStripMenuItem? _systemStatusMenuItem;
        private ApplicationSettingsModel _settings;
        private bool _isMonitoring = true;
        private bool _isWmiAvailable = true;
        private TrayIconState _currentIconState = TrayIconState.Normal;
        private bool _disposed = false;

        public event EventHandler? QuickApplyRequested;
        public event EventHandler? ShowMainWindowRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler<MonitoringToggleEventArgs>? MonitoringToggleRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler<PowerPlanChangeRequestedEventArgs>? PowerPlanChangeRequested;
        public event EventHandler<ProfileApplicationRequestedEventArgs>? ProfileApplicationRequested;
        public event EventHandler? PerformanceDashboardRequested;

        public SystemTrayService(ILogger<SystemTrayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = new ApplicationSettingsModel(); // Default settings
        }

        public void Initialize()
        {
            try
            {
                _logger.LogInformation("Initializing system tray service");

                // Check if already initialized to prevent duplicate icons
                if (_notifyIcon != null)
                {
                    _logger.LogInformation("System tray service already initialized, skipping duplicate initialization to prevent duplicate icons");
                    return;
                }

                // Create the notify icon
                _notifyIcon = new NotifyIcon
                {
                    Text = "ThreadPilot - Process & Power Plan Manager",
                    Visible = false
                };

                // Load the tray icon (custom path if enabled, otherwise bundled ico.ico)
                TryLoadTrayIcon();

                // Create context menu
                CreateContextMenu();

                // Set up event handlers
                _notifyIcon.DoubleClick += OnTrayIconDoubleClick;
                _notifyIcon.ContextMenuStrip = _contextMenu;

                // Set initial icon state
                UpdateTrayIcon(TrayIconState.Normal);

                _logger.LogInformation("System tray service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize system tray service");
                throw;
            }
        }

        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            // System status (disabled, for display only)
            _systemStatusMenuItem = new ToolStripMenuItem("System Status")
            {
                Enabled = false,
                Font = new Font(_contextMenu.Font, FontStyle.Bold)
            };
            _contextMenu.Items.Add(_systemStatusMenuItem);

            // Selected process info (disabled, for display only)
            _selectedProcessMenuItem = new ToolStripMenuItem("No process selected")
            {
                Enabled = false,
                Font = new Font(_contextMenu.Font, FontStyle.Italic)
            };
            _contextMenu.Items.Add(_selectedProcessMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Quick apply command
            _quickApplyMenuItem = new ToolStripMenuItem("⚡ Quick Apply Affinity & Power Plan")
            {
                Enabled = false
            };
            _quickApplyMenuItem.Click += OnQuickApplyClick;
            _contextMenu.Items.Add(_quickApplyMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Power Plans submenu
            _powerPlansMenuItem = new ToolStripMenuItem("🔋 Power Plans");
            _contextMenu.Items.Add(_powerPlansMenuItem);

            // Profiles submenu
            _profilesMenuItem = new ToolStripMenuItem("📋 Profiles");
            _contextMenu.Items.Add(_profilesMenuItem);

            // Performance Dashboard
            _performanceMenuItem = new ToolStripMenuItem("📊 Performance Dashboard");
            _performanceMenuItem.Click += OnPerformanceDashboardClick;
            _contextMenu.Items.Add(_performanceMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Monitoring toggle
            _monitoringToggleMenuItem = new ToolStripMenuItem("🔍 Disable Process Monitoring");
            _monitoringToggleMenuItem.Click += OnMonitoringToggleClick;
            _contextMenu.Items.Add(_monitoringToggleMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Settings
            _settingsMenuItem = new ToolStripMenuItem("⚙️ Settings...");
            _settingsMenuItem.Click += OnSettingsClick;
            _contextMenu.Items.Add(_settingsMenuItem);

            // Show main window
            var showMenuItem = new ToolStripMenuItem("🪟 Show ThreadPilot");
            showMenuItem.Click += OnShowMainWindowClick;
            _contextMenu.Items.Add(showMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitMenuItem = new ToolStripMenuItem("❌ Exit");
            exitMenuItem.Click += OnExitClick;
            _contextMenu.Items.Add(exitMenuItem);
        }

        public void Show()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
                _logger.LogDebug("System tray icon shown");
            }
        }

        public void Hide()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _logger.LogDebug("System tray icon hidden");
            }
        }

        public void UpdateTooltip(string tooltip)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip;
            }
        }

        public void ShowBalloonTip(string title, string text, int timeoutMs = 3000)
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(timeoutMs, title, text, ToolTipIcon.Info);
            }
        }

        public void UpdateContextMenu(string? selectedProcessName = null, bool hasSelection = false)
        {
            if (_selectedProcessMenuItem == null || _quickApplyMenuItem == null) return;

            if (hasSelection && !string.IsNullOrEmpty(selectedProcessName))
            {
                _selectedProcessMenuItem.Text = $"Selected: {selectedProcessName}";
                _quickApplyMenuItem.Enabled = true;
                _quickApplyMenuItem.Text = $"Quick Apply to {selectedProcessName}";
            }
            else
            {
                _selectedProcessMenuItem.Text = "No process selected";
                _quickApplyMenuItem.Enabled = false;
                _quickApplyMenuItem.Text = "Quick Apply Affinity & Power Plan";
            }
        }

        private void OnTrayIconDoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnQuickApplyClick(object? sender, EventArgs e)
        {
            QuickApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnShowMainWindowClick(object? sender, EventArgs e)
        {
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnMonitoringToggleClick(object? sender, EventArgs e)
        {
            _isMonitoring = !_isMonitoring;
            MonitoringToggleRequested?.Invoke(this, new MonitoringToggleEventArgs(_isMonitoring));
            UpdateMonitoringStatus(_isMonitoring, _isWmiAvailable);
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnPerformanceDashboardClick(object? sender, EventArgs e)
        {
            PerformanceDashboardRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnPowerPlanClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is PowerPlanModel powerPlan)
            {
                PowerPlanChangeRequested?.Invoke(this, new PowerPlanChangeRequestedEventArgs(powerPlan.Guid, powerPlan.Name));
            }
        }

        private void OnProfileClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string profileName)
            {
                ProfileApplicationRequested?.Invoke(this, new ProfileApplicationRequestedEventArgs(profileName));
            }
        }

        public void UpdateMonitoringStatus(bool isMonitoring, bool isWmiAvailable = true)
        {
            _isMonitoring = isMonitoring;
            _isWmiAvailable = isWmiAvailable;

            if (_monitoringToggleMenuItem != null)
            {
                _monitoringToggleMenuItem.Text = isMonitoring ? "Disable Process Monitoring" : "Enable Process Monitoring";
                _monitoringToggleMenuItem.Enabled = isWmiAvailable;
            }

            // Update tray icon state
            var iconState = !isWmiAvailable ? TrayIconState.Error :
                           isMonitoring ? TrayIconState.Monitoring : TrayIconState.Disabled;
            UpdateTrayIcon(iconState);

            // Update tooltip
            var status = !isWmiAvailable ? "WMI Error" :
                        isMonitoring ? "Monitoring Active" : "Monitoring Disabled";
            UpdateTooltip($"ThreadPilot - {status}");
        }

        public void UpdateTrayIcon(TrayIconState state)
        {
            if (_notifyIcon == null) return;

            _currentIconState = state;

            TryLoadTrayIcon(state);
        }

        public void ShowTrayNotification(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000)
        {
            if (_notifyIcon == null || !_settings.EnableBalloonNotifications) return;

            try
            {
                var balloonIcon = type switch
                {
                    NotificationType.Error => ToolTipIcon.Error,
                    NotificationType.Warning => ToolTipIcon.Warning,
                    NotificationType.Success => ToolTipIcon.Info,
                    _ => ToolTipIcon.Info
                };

                _notifyIcon.ShowBalloonTip(timeoutMs, title, message, balloonIcon);
                _logger.LogDebug("Balloon tip shown: {Title}", title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing balloon tip");
            }
        }

        public void UpdateSettings(ApplicationSettingsModel settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Update tray icon visibility
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = settings.ShowTrayIcon;
                TryLoadTrayIcon(_currentIconState);
            }

            _logger.LogDebug("Tray service settings updated");
        }

        public void UpdatePowerPlans(IEnumerable<PowerPlanModel> powerPlans, PowerPlanModel? activePlan)
        {
            if (_powerPlansMenuItem == null) return;

            try
            {
                _powerPlansMenuItem.DropDownItems.Clear();

                foreach (var powerPlan in powerPlans)
                {
                    var menuItem = new ToolStripMenuItem(powerPlan.Name)
                    {
                        Tag = powerPlan,
                        Checked = activePlan?.Guid == powerPlan.Guid
                    };
                    menuItem.Click += OnPowerPlanClick;
                    _powerPlansMenuItem.DropDownItems.Add(menuItem);
                }

                _logger.LogDebug("Updated power plans in context menu");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update power plans in context menu");
            }
        }

        public void UpdateProfiles(IEnumerable<string> profileNames)
        {
            if (_profilesMenuItem == null) return;

            try
            {
                _profilesMenuItem.DropDownItems.Clear();

                if (!profileNames.Any())
                {
                    var noProfilesItem = new ToolStripMenuItem("No profiles available")
                    {
                        Enabled = false
                    };
                    _profilesMenuItem.DropDownItems.Add(noProfilesItem);
                }
                else
                {
                    foreach (var profileName in profileNames)
                    {
                        var menuItem = new ToolStripMenuItem(profileName)
                        {
                            Tag = profileName
                        };
                        menuItem.Click += OnProfileClick;
                        _profilesMenuItem.DropDownItems.Add(menuItem);
                    }
                }

                _logger.LogDebug("Updated profiles in context menu");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update profiles in context menu");
            }
        }

        public void UpdateSystemStatus(string currentPowerPlan, double cpuUsage, double memoryUsage)
        {
            if (_systemStatusMenuItem == null) return;

            try
            {
                _systemStatusMenuItem.Text = $"💻 CPU: {cpuUsage:F1}% | RAM: {memoryUsage:F1}% | {currentPowerPlan}";
                _logger.LogDebug("Updated system status in context menu");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update system status in context menu");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _logger.LogInformation("Disposing system tray service");

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }

                if (_contextMenu != null)
                {
                    _contextMenu.Dispose();
                    _contextMenu = null;
                }

                _disposed = true;
                _logger.LogInformation("System tray service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing system tray service");
            }
        }

        private string? ResolveTrayIconPath()
        {
            if (_settings.UseCustomTrayIcon && !string.IsNullOrWhiteSpace(_settings.CustomTrayIconPath) && File.Exists(_settings.CustomTrayIconPath))
            {
                return _settings.CustomTrayIconPath;
            }

            var bundledIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ico.ico");
            return File.Exists(bundledIcon) ? bundledIcon : null;
        }

        private Icon? TryLoadEmbeddedIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/ico.ico", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    return new Icon(streamInfo.Stream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load embedded icon");
            }
            return null;
        }

        private void TryLoadTrayIcon(TrayIconState? stateOverride = null)
        {
            if (_notifyIcon == null) return;

            try
            {
                // Try custom or external bundled icon first
                var iconPath = ResolveTrayIconPath();
                if (iconPath != null)
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                    _logger.LogDebug("Tray icon set from {IconPath}", iconPath);
                    return;
                }

                // Try embedded resource icon (for single-file publish)
                var embeddedIcon = TryLoadEmbeddedIcon();
                if (embeddedIcon != null)
                {
                    _notifyIcon.Icon = embeddedIcon;
                    _logger.LogDebug("Tray icon set from embedded resource");
                    return;
                }

                // Fallback to system icons if no custom/bundled/embedded icon is available
                var state = stateOverride ?? _currentIconState;
                _notifyIcon.Icon = state switch
                {
                    TrayIconState.Normal => SystemIcons.Application,
                    TrayIconState.Monitoring => SystemIcons.Information,
                    TrayIconState.Error => SystemIcons.Error,
                    TrayIconState.Disabled => SystemIcons.Warning,
                    _ => SystemIcons.Application
                };
                _logger.LogDebug("Tray icon set to system icon for state {State}", state);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load tray icon");
            }
        }
    }
}
