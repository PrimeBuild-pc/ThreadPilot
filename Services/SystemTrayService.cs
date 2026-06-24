namespace ThreadPilot.Services
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

    public class SystemTrayService : ISystemTrayService
    {
        private readonly ILogger<SystemTrayService> logger;
        private NotifyIcon? notifyIcon;
        private ContextMenuStrip? contextMenu;
        private ToolStripMenuItem? quickApplyMenuItem;
        private ToolStripMenuItem? selectedProcessMenuItem;
        private ToolStripMenuItem? monitoringToggleMenuItem;
        private ToolStripMenuItem? settingsMenuItem;
        private ToolStripMenuItem? powerPlansMenuItem;
        private ToolStripMenuItem? profilesMenuItem;
        private ToolStripMenuItem? performanceMenuItem;
        private ToolStripMenuItem? systemStatusMenuItem;
        private ToolStripMenuItem? openDashboardMenuItem;
        private ToolStripMenuItem? exitMenuItem;
        private ApplicationSettingsModel settings;
        private readonly ILocalizationService? localizationService;
        private bool isMonitoring = true;
        private bool isWmiAvailable = true;
        private TrayIconState currentIconState = TrayIconState.Normal;
        private bool isDarkTheme = true;
        private Font? menuFont;
        private Point lastContextMenuOpenPoint = Point.Empty;
        private bool disposed = false;

        public event EventHandler? QuickApplyRequested;

        public event EventHandler? ShowMainWindowRequested;

        public event EventHandler? ExitRequested;

        public event EventHandler<MonitoringToggleEventArgs>? MonitoringToggleRequested;

        public event EventHandler? SettingsRequested;

        public event EventHandler<PowerPlanChangeRequestedEventArgs>? PowerPlanChangeRequested;

        public event EventHandler<ProfileApplicationRequestedEventArgs>? ProfileApplicationRequested;

        public event EventHandler? PerformanceDashboardRequested;

        public event EventHandler? DashboardRequested;

        public SystemTrayService(ILogger<SystemTrayService> logger, ILocalizationService? localizationService = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.localizationService = localizationService;
            this.settings = new ApplicationSettingsModel(); // Default settings
            if (this.localizationService != null)
            {
                this.localizationService.LanguageChanged += this.OnLanguageChanged;
            }
        }

        public void Initialize()
        {
            try
            {
                this.logger.LogInformation("Initializing system tray service");

                // Check if already initialized to prevent duplicate icons
                if (this.notifyIcon != null)
                {
                    this.logger.LogInformation("System tray service already initialized, skipping duplicate initialization to prevent duplicate icons");
                    return;
                }

                // Create the notify icon
                this.notifyIcon = new NotifyIcon
                {
                    Text = this.Localize("MainWindow_Title", "ThreadPilot - Process & Power Plan Manager"),
                    Visible = false,
                };

                // Load the tray icon (custom path if enabled, otherwise bundled ico.ico)
                this.TryLoadTrayIcon();

                // Create context menu
                this.CreateContextMenu();

                // Set up event handlers
                this.notifyIcon.DoubleClick += this.OnTrayIconDoubleClick;
                this.notifyIcon.MouseUp += this.OnTrayIconMouseUp;

                // Set initial icon state
                this.UpdateTrayIcon(TrayIconState.Normal);

                this.logger.LogInformation("System tray service initialized successfully");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize system tray service");
                throw;
            }
        }

        private void CreateContextMenu()
        {
            this.contextMenu = new ContextMenuStrip();
            this.menuFont = CreatePreferredMenuFont(this.contextMenu.Font.Size);
            this.contextMenu.Font = this.menuFont;

            this.openDashboardMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_OpenDashboard", "Open Dashboard"))
            {
                Font = new Font(this.menuFont, FontStyle.Regular),
            };
            this.openDashboardMenuItem.Click += this.OnDashboardClick;
            this.contextMenu.Items.Add(this.openDashboardMenuItem);

            if (AppNavigationOptions.ShowAdvancedDiagnostics)
            {
                this.performanceMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_OpenDiagnostics", "Open Diagnostics"))
                {
                    Font = new Font(this.menuFont, FontStyle.Regular),
                };
                this.performanceMenuItem.Click += this.OnPerformanceDashboardClick;
                this.contextMenu.Items.Add(this.performanceMenuItem);
            }

            this.monitoringToggleMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_PauseMonitoring", "Pause Automation Monitoring"));
            this.monitoringToggleMenuItem.Click += this.OnMonitoringToggleClick;
            this.contextMenu.Items.Add(this.monitoringToggleMenuItem);

            this.contextMenu.Items.Add(new ToolStripSeparator());

            // System status (disabled, for display only)
            this.systemStatusMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_SystemStatus", "System Status"))
            {
                Enabled = false,
                Font = new Font(this.menuFont, FontStyle.Regular),
            };
            this.contextMenu.Items.Add(this.systemStatusMenuItem);

            // Selected process info (disabled, for display only)
            this.selectedProcessMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_NoProcessSelected", "No process selected"))
            {
                Enabled = false,
                Font = new Font(this.menuFont, FontStyle.Regular),
            };
            this.contextMenu.Items.Add(this.selectedProcessMenuItem);

            // Quick apply command
            this.quickApplyMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_ApplyPendingToSelected", "Apply Pending Settings to Selected Process"))
            {
                Enabled = false,
            };
            this.quickApplyMenuItem.Click += this.OnQuickApplyClick;
            this.contextMenu.Items.Add(this.quickApplyMenuItem);

            this.contextMenu.Items.Add(new ToolStripSeparator());

            // Power Plans submenu
            this.powerPlansMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_PowerPlans", "ðŸ”‹ Power Plans"));
            this.contextMenu.Items.Add(this.powerPlansMenuItem);

            // Profiles submenu
            this.profilesMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_Profiles", "ðŸ“‹ Profiles"));
            this.contextMenu.Items.Add(this.profilesMenuItem);

            this.contextMenu.Items.Add(new ToolStripSeparator());

            // Settings
            this.settingsMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_Settings", "Settings"));
            this.settingsMenuItem.Click += this.OnSettingsClick;
            this.contextMenu.Items.Add(this.settingsMenuItem);

            // Separator
            this.contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            this.exitMenuItem = new ToolStripMenuItem(this.Localize("SystemTray_Exit", "Exit"));
            this.exitMenuItem.Click += this.OnExitClick;
            this.contextMenu.Items.Add(this.exitMenuItem);

            this.ApplyContextMenuTheme();
        }

        public void Show()
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = true;
                this.logger.LogDebug("System tray icon shown");
            }
        }

        public void Hide()
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = false;
                this.logger.LogDebug("System tray icon hidden");
            }
        }

        public void UpdateTooltip(string tooltip)
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip;
            }
        }

        public void ShowBalloonTip(string title, string text, int timeoutMs = 3000)
        {
            if (this.notifyIcon != null && this.notifyIcon.Visible)
            {
                this.notifyIcon.ShowBalloonTip(timeoutMs, title, text, ToolTipIcon.Info);
            }
        }

        public void UpdateContextMenu(string? selectedProcessName = null, bool hasSelection = false)
        {
            if (this.selectedProcessMenuItem == null || this.quickApplyMenuItem == null)
            {
                return;
            }

            if (hasSelection && !string.IsNullOrEmpty(selectedProcessName))
            {
                this.selectedProcessMenuItem.Text = string.Format(
                    this.Localize("SystemTray_SelectedProcessFormat", "Selected: {0}"),
                    selectedProcessName);
                this.quickApplyMenuItem.Enabled = true;
                this.quickApplyMenuItem.Text = string.Format(
                    this.Localize("SystemTray_ApplyPendingToProcessFormat", "Apply Pending Settings to {0}"),
                    selectedProcessName);
            }
            else
            {
                this.selectedProcessMenuItem.Text = this.Localize("SystemTray_NoProcessSelected", "No process selected");
                this.quickApplyMenuItem.Enabled = false;
                this.quickApplyMenuItem.Text = this.Localize("SystemTray_ApplyPendingToSelected", "Apply Pending Settings to Selected Process");
            }
        }

        private void OnTrayIconDoubleClick(object? sender, EventArgs e)
        {
            this.ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnTrayIconMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || this.contextMenu == null)
            {
                return;
            }

            var cursorPosition = Cursor.Position;
            var workingArea = Screen.FromPoint(cursorPosition.IsEmpty ? this.lastContextMenuOpenPoint : cursorPosition).WorkingArea;
            var openPoint = SystemTrayMenuPlacement.ResolveMenuOpenPoint(
                cursorPosition,
                this.lastContextMenuOpenPoint,
                Rectangle.Empty,
                workingArea);
            this.lastContextMenuOpenPoint = openPoint;

            if (this.contextMenu.Visible)
            {
                this.contextMenu.Close(ToolStripDropDownCloseReason.CloseCalled);
            }

            this.contextMenu.Show(openPoint, ToolStripDropDownDirection.Default);
        }

        private void OnQuickApplyClick(object? sender, EventArgs e)
        {
            this.QuickApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnDashboardClick(object? sender, EventArgs e)
        {
            this.DashboardRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            this.ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnMonitoringToggleClick(object? sender, EventArgs e)
        {
            this.isMonitoring = !this.isMonitoring;
            this.MonitoringToggleRequested?.Invoke(this, new MonitoringToggleEventArgs(this.isMonitoring));
            this.UpdateMonitoringStatus(this.isMonitoring, this.isWmiAvailable);
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            this.SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnPerformanceDashboardClick(object? sender, EventArgs e)
        {
            this.PerformanceDashboardRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnPowerPlanClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is PowerPlanModel powerPlan)
            {
                this.PowerPlanChangeRequested?.Invoke(this, new PowerPlanChangeRequestedEventArgs(powerPlan.Guid, powerPlan.Name));
            }
        }

        private void OnProfileClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string profileName)
            {
                this.ProfileApplicationRequested?.Invoke(this, new ProfileApplicationRequestedEventArgs(profileName));
            }
        }

        public void UpdateMonitoringStatus(bool isMonitoring, bool isWmiAvailable = true)
        {
            this.isMonitoring = isMonitoring;
            this.isWmiAvailable = isWmiAvailable;

            if (this.monitoringToggleMenuItem != null)
            {
                this.monitoringToggleMenuItem.Text = isMonitoring
                    ? this.Localize("SystemTray_PauseMonitoring", "Pause Automation Monitoring")
                    : this.Localize("SystemTray_ResumeMonitoring", "Resume Automation Monitoring");
                this.monitoringToggleMenuItem.Enabled = isWmiAvailable;
            }

            // Update tray icon state
            var iconState = !isWmiAvailable ? TrayIconState.Error :
                           isMonitoring ? TrayIconState.Monitoring : TrayIconState.Disabled;
            this.UpdateTrayIcon(iconState);

            // Update tooltip
            var status = !isWmiAvailable
                ? this.Localize("SystemTray_StatusWmiError", "Automation WMI Error")
                : isMonitoring
                    ? this.Localize("SystemTray_StatusActive", "Automation Active")
                    : this.Localize("SystemTray_StatusDisabled", "Automation Disabled");
            this.UpdateTooltip($"ThreadPilot - {status}");
        }

        public void UpdateTrayIcon(TrayIconState state)
        {
            if (this.notifyIcon == null)
            {
                return;
            }

            this.currentIconState = state;

            this.TryLoadTrayIcon(state);
        }

        public void ShowTrayNotification(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000)
        {
            if (this.notifyIcon == null || !this.settings.EnableBalloonNotifications)
            {
                return;
            }

            try
            {
                var balloonIcon = type switch
                {
                    NotificationType.Error => ToolTipIcon.Error,
                    NotificationType.Warning => ToolTipIcon.Warning,
                    NotificationType.Success => ToolTipIcon.Info,
                    _ => ToolTipIcon.Info,
                };

                this.notifyIcon.ShowBalloonTip(timeoutMs, title, message, balloonIcon);
                this.logger.LogDebug("Balloon tip shown: {Title}", title);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error showing balloon tip");
            }
        }

        public void UpdateSettings(ApplicationSettingsModel settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Update tray icon visibility
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = settings.ShowTrayIcon;
                this.TryLoadTrayIcon(this.currentIconState);
            }

            this.ApplyContextMenuTheme();

            this.logger.LogDebug("Tray service settings updated");
        }

        public void ApplyTheme(bool useDarkTheme)
        {
            if (this.isDarkTheme == useDarkTheme)
            {
                return;
            }

            this.isDarkTheme = useDarkTheme;
            this.ApplyContextMenuTheme();
        }

        public void UpdatePowerPlans(IEnumerable<PowerPlanModel> powerPlans, PowerPlanModel? activePlan)
        {
            if (this.powerPlansMenuItem == null)
            {
                return;
            }

            try
            {
                this.powerPlansMenuItem.DropDownItems.Clear();

                foreach (var powerPlan in powerPlans)
                {
                    var menuItem = new ToolStripMenuItem(powerPlan.Name)
                    {
                        Tag = powerPlan,
                        Checked = activePlan?.Guid == powerPlan.Guid,
                    };
                    menuItem.Click += this.OnPowerPlanClick;
                    this.powerPlansMenuItem.DropDownItems.Add(menuItem);
                }

                this.ApplyContextMenuTheme();

                this.logger.LogDebug("Updated power plans in context menu");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to update power plans in context menu");
            }
        }

        public void UpdateProfiles(IEnumerable<string> profileNames)
        {
            if (this.profilesMenuItem == null)
            {
                return;
            }

            try
            {
                this.profilesMenuItem.DropDownItems.Clear();

                if (!profileNames.Any())
                {
                    var noProfilesItem = new ToolStripMenuItem(this.Localize("SystemTray_NoProfilesAvailable", "No profiles available"))
                    {
                        Enabled = false,
                    };
                    this.profilesMenuItem.DropDownItems.Add(noProfilesItem);
                }
                else
                {
                    foreach (var profileName in profileNames)
                    {
                        var menuItem = new ToolStripMenuItem(profileName)
                        {
                            Tag = profileName,
                        };
                        menuItem.Click += this.OnProfileClick;
                        this.profilesMenuItem.DropDownItems.Add(menuItem);
                    }
                }

                this.ApplyContextMenuTheme();

                this.logger.LogDebug("Updated profiles in context menu");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to update profiles in context menu");
            }
        }

        public void UpdateSystemStatus(string currentPowerPlan, double cpuUsage, double memoryUsage)
        {
            if (this.systemStatusMenuItem == null)
            {
                return;
            }

            try
            {
                this.systemStatusMenuItem.Text = string.Format(
                    this.Localize("SystemTray_CpuRamStatusFormat", "ðŸ’» CPU: {0:F1}% | RAM: {1:F1}% | {2}"),
                    cpuUsage,
                    memoryUsage,
                    currentPowerPlan);
                this.logger.LogDebug("Updated system status in context menu");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to update system status in context menu");
            }
        }

        public void UpdateSystemStatus(string currentPowerPlan)
        {
            if (this.systemStatusMenuItem == null)
            {
                return;
            }

            try
            {
                this.systemStatusMenuItem.Text = string.Format(
                    this.Localize("SystemTray_PowerPlanFormat", "Power Plan: {0}"),
                    currentPowerPlan);
                this.logger.LogDebug("Updated non-performance system status in context menu");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to update system status in context menu");
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            try
            {
                this.logger.LogInformation("Disposing system tray service");

                if (this.notifyIcon != null)
                {
                    this.notifyIcon.MouseUp -= this.OnTrayIconMouseUp;
                    this.notifyIcon.Visible = false;
                    this.notifyIcon.Dispose();
                    this.notifyIcon = null;
                }

                if (this.contextMenu != null)
                {
                    this.contextMenu.Dispose();
                    this.contextMenu = null;
                }

                this.menuFont?.Dispose();
                this.menuFont = null;
                if (this.localizationService != null)
                {
                    this.localizationService.LanguageChanged -= this.OnLanguageChanged;
                }

                this.disposed = true;
                this.logger.LogInformation("System tray service disposed");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error disposing system tray service");
            }
        }

        private string? ResolveTrayIconPath()
        {
            if (this.settings.UseCustomTrayIcon && !string.IsNullOrWhiteSpace(this.settings.CustomTrayIconPath) && File.Exists(this.settings.CustomTrayIconPath))
            {
                return this.settings.CustomTrayIconPath;
            }

            var iconCandidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ico.ico"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons", "ico.ico"),
            };

            foreach (var candidate in iconCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void OnLanguageChanged(object? sender, string language)
        {
            this.UpdateLocalizedMenuText();
        }

        private void UpdateLocalizedMenuText()
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Text = this.Localize("MainWindow_Title", "ThreadPilot - Process & Power Plan Manager");
            }

            if (this.openDashboardMenuItem != null)
            {
                this.openDashboardMenuItem.Text = this.Localize("SystemTray_OpenDashboard", "Open Dashboard");
            }

            if (this.performanceMenuItem != null)
            {
                this.performanceMenuItem.Text = this.Localize("SystemTray_OpenDiagnostics", "Open Diagnostics");
            }

            if (this.systemStatusMenuItem != null)
            {
                this.systemStatusMenuItem.Text = this.Localize("SystemTray_SystemStatus", "System Status");
            }

            if (this.powerPlansMenuItem != null)
            {
                this.powerPlansMenuItem.Text = this.Localize("SystemTray_PowerPlans", "ðŸ”‹ Power Plans");
            }

            if (this.profilesMenuItem != null)
            {
                this.profilesMenuItem.Text = this.Localize("SystemTray_Profiles", "ðŸ“‹ Profiles");
            }

            if (this.settingsMenuItem != null)
            {
                this.settingsMenuItem.Text = this.Localize("SystemTray_Settings", "Settings");
            }

            if (this.exitMenuItem != null)
            {
                this.exitMenuItem.Text = this.Localize("SystemTray_Exit", "Exit");
            }

            this.UpdateContextMenu();
            this.UpdateMonitoringStatus(this.isMonitoring, this.isWmiAvailable);
        }

        private string Localize(string key, string fallback)
        {
            if (this.localizationService == null)
            {
                return fallback;
            }

            var localized = this.localizationService.GetString(key);
            return string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.Ordinal)
                ? fallback
                : localized;
        }

        private Icon? TryLoadEmbeddedIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/assets/icons/ico.ico", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    return new Icon(streamInfo.Stream);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to load embedded icon");
            }
            return null;
        }

        private void TryLoadTrayIcon(TrayIconState? stateOverride = null)
        {
            if (this.notifyIcon == null)
            {
                return;
            }

            try
            {
                // Try custom or external bundled icon first
                var iconPath = this.ResolveTrayIconPath();
                if (iconPath != null)
                {
                    this.notifyIcon.Icon = new Icon(iconPath);
                    this.logger.LogDebug("Tray icon set from {IconPath}", iconPath);
                    return;
                }

                // Try embedded resource icon (for single-file publish)
                var embeddedIcon = this.TryLoadEmbeddedIcon();
                if (embeddedIcon != null)
                {
                    this.notifyIcon.Icon = embeddedIcon;
                    this.logger.LogDebug("Tray icon set from embedded resource");
                    return;
                }

                // Fallback to system icons if no custom/bundled/embedded icon is available
                var state = stateOverride ?? this.currentIconState;
                this.notifyIcon.Icon = state switch
                {
                    TrayIconState.Normal => SystemIcons.Application,
                    TrayIconState.Monitoring => SystemIcons.Information,
                    TrayIconState.Error => SystemIcons.Error,
                    TrayIconState.Disabled => SystemIcons.Warning,
                    _ => SystemIcons.Application,
                };
                this.logger.LogDebug("Tray icon set to system icon for state {State}", state);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to load tray icon");
            }
        }

        private void ApplyContextMenuTheme()
        {
            if (this.contextMenu == null)
            {
                return;
            }

            Color backgroundColor;
            Color foregroundColor;
            Color selectionColor;
            Color borderColor;
            Color disabledColor;

            if (this.isDarkTheme)
            {
                // Force stable dark palette for WinForms tray menu even if XAML resources are unavailable.
                backgroundColor = Color.FromArgb(28, 28, 30);
                foregroundColor = Color.FromArgb(232, 232, 232);
                selectionColor = Color.FromArgb(60, 60, 64);
                borderColor = Color.FromArgb(74, 74, 79);
                disabledColor = Color.FromArgb(132, 132, 136);
            }
            else
            {
                backgroundColor = ResolveColorFromResource("SurfaceAltBrush", SystemColors.Menu);
                foregroundColor = ResolveColorFromResource("TextPrimaryBrush", SystemColors.MenuText);
                selectionColor = ResolveColorFromResource("SoftSelectionBackgroundBrush", SystemColors.Highlight);
                borderColor = ResolveColorFromResource("BorderBrush", SystemColors.ControlDark);
                disabledColor = ResolveColorFromResource("TextDisabledBrush", SystemColors.GrayText);
            }

            this.contextMenu.RenderMode = ToolStripRenderMode.Professional;
            this.contextMenu.Renderer = new ToolStripProfessionalRenderer(new TrayMenuColorTable(backgroundColor, selectionColor, borderColor));
            this.contextMenu.BackColor = backgroundColor;
            this.contextMenu.ForeColor = foregroundColor;

            ApplyMenuItemTheme(this.contextMenu.Items, backgroundColor, foregroundColor, disabledColor);
        }

        private static void ApplyMenuItemTheme(ToolStripItemCollection items, Color backColor, Color foreColor, Color disabledColor)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripSeparator)
                {
                    continue;
                }

                item.BackColor = backColor;
                item.ForeColor = item.Enabled ? foreColor : disabledColor;

                if (item is ToolStripMenuItem menuItem)
                {
                    menuItem.DropDown.BackColor = backColor;
                    menuItem.DropDown.ForeColor = foreColor;

                    if (menuItem.DropDownItems.Count > 0)
                    {
                        ApplyMenuItemTheme(menuItem.DropDownItems, backColor, foreColor, disabledColor);
                    }
                }
            }
        }

        private sealed class TrayMenuColorTable : ProfessionalColorTable
        {
            private readonly Color backgroundColor;
            private readonly Color selectionColor;
            private readonly Color borderColor;

            public TrayMenuColorTable(Color backgroundColor, Color selectionColor, Color borderColor)
            {
                this.backgroundColor = backgroundColor;
                this.selectionColor = selectionColor;
                this.borderColor = borderColor;
            }

            public override Color MenuBorder => this.borderColor;

            public override Color ToolStripDropDownBackground => this.backgroundColor;

            public override Color ImageMarginGradientBegin => this.ToolStripDropDownBackground;

            public override Color ImageMarginGradientMiddle => this.ToolStripDropDownBackground;

            public override Color ImageMarginGradientEnd => this.ToolStripDropDownBackground;

            public override Color MenuItemSelected => this.selectionColor;

            public override Color MenuItemSelectedGradientBegin => this.MenuItemSelected;

            public override Color MenuItemSelectedGradientEnd => this.MenuItemSelected;

            public override Color MenuItemBorder => this.borderColor;
        }

        private static Font CreatePreferredMenuFont(float baseSize)
        {
            var size = Math.Max(8.5f, baseSize);

            try
            {
                return new Font("Segoe UI Variable", size, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                return new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        private static Color ResolveColorFromResource(string resourceKey, Color fallback)
        {
            var app = System.Windows.Application.Current;
            if (app == null)
            {
                return fallback;
            }

            MediaSolidColorBrush? brush = null;

            if (app.Dispatcher.CheckAccess())
            {
                brush = app.TryFindResource(resourceKey) as MediaSolidColorBrush;
            }
            else
            {
                app.Dispatcher.Invoke(() =>
                {
                    brush = app.TryFindResource(resourceKey) as MediaSolidColorBrush;
                });
            }

            if (brush != null)
            {
                return Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
            }

            return fallback;
        }
    }
}
