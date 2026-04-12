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
namespace ThreadPilot.ViewModels
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Services;

    /// <summary>
    /// Base ViewModel with common functionality for all ViewModels.
    /// </summary>
    public abstract partial class BaseViewModel : ObservableObject, IDisposable
    {
        protected readonly ILogger Logger;
        protected readonly IEnhancedLoggingService? EnhancedLoggingService;
        private bool disposed;
        private CancellationTokenSource? statusLifetimeCts;
        private const int StatusVisibleDurationMs = 1500;
        private const int StatusFadeDurationMs = 500;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private double statusOpacity = 1.0;

        [ObservableProperty]
        private bool hasError;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        protected BaseViewModel(ILogger logger, IEnhancedLoggingService? enhancedLoggingService = null)
        {
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.EnhancedLoggingService = enhancedLoggingService;
        }

        /// <summary>
        /// Set status message and busy state.
        /// </summary>
        protected void SetStatus(string message, bool isBusyState = true)
        {
            this.CancelStatusLifetime();
            this.StatusOpacity = 1.0;
            this.StatusMessage = message;
            this.IsBusy = isBusyState;
            this.ClearError();

            if (!string.IsNullOrWhiteSpace(message) && !isBusyState)
            {
                _ = this.StartStatusLifetimeAsync(message);
            }
        }

        /// <summary>
        /// Clear status and busy state.
        /// </summary>
        protected void ClearStatus()
        {
            this.CancelStatusLifetime();
            this.StatusMessage = string.Empty;
            this.StatusOpacity = 1.0;
            this.IsBusy = false;
        }

        /// <summary>
        /// Set error message and clear busy state.
        /// </summary>
        protected void SetError(string message, Exception? exception = null)
        {
            this.ErrorMessage = message;
            this.HasError = true;
            this.IsBusy = false;

            if (exception != null)
            {
                this.Logger.LogError(exception, "Error in {ViewModelType}: {Message}", this.GetType().Name, message);
            }
            else
            {
                this.Logger.LogWarning("Error in {ViewModelType}: {Message}", this.GetType().Name, message);
            }
        }

        /// <summary>
        /// Clear error state.
        /// </summary>
        protected void ClearError()
        {
            this.ErrorMessage = string.Empty;
            this.HasError = false;
        }

        /// <summary>
        /// Execute an async operation with error handling and status updates.
        /// </summary>
        protected async Task ExecuteAsync(Func<Task> operation, string? statusMessage = null, string? successMessage = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(statusMessage))
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.SetStatus(statusMessage);
                    });
                }

                await operation();

                if (!string.IsNullOrEmpty(successMessage))
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.SetStatus(successMessage, false);
                    });
                }
                else
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.ClearStatus();
                    });
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetError($"Operation failed: {ex.Message}", ex);
                });
            }
        }

        /// <summary>
        /// Execute an async operation with return value and error handling.
        /// </summary>
        protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, string? statusMessage = null, string? successMessage = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(statusMessage))
                {
                    // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.SetStatus(statusMessage);
                    });
                }

                var result = await operation();

                if (!string.IsNullOrEmpty(successMessage))
                {
                    // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.SetStatus(successMessage, false);
                    });
                }
                else
                {
                    // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.ClearStatus();
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetError($"Operation failed: {ex.Message}", ex);
                });
                return default;
            }
        }

        /// <summary>
        /// Log user action for audit purposes.
        /// </summary>
        protected async Task LogUserActionAsync(string action, string details, string? context = null)
        {
            try
            {
                if (this.EnhancedLoggingService != null)
                {
                    await this.EnhancedLoggingService.LogUserActionAsync(action, details, context);
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to log user action: {Action}", action);
            }
        }

        /// <summary>
        /// Initialize the ViewModel - override in derived classes.
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            // Base implementation does nothing
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cleanup resources - override in derived classes.
        /// </summary>
        protected virtual void OnDispose()
        {
            this.CancelStatusLifetime();
            // Base implementation does nothing
        }

        private async Task StartStatusLifetimeAsync(string expectedMessage)
        {
            var cts = new CancellationTokenSource();
            this.statusLifetimeCts = cts;

            try
            {
                await Task.Delay(StatusVisibleDurationMs, cts.Token);

                const int fadeSteps = 5;
                var stepDelay = StatusFadeDurationMs / fadeSteps;

                for (var i = 1; i <= fadeSteps; i++)
                {
                    await Task.Delay(stepDelay, cts.Token);
                    if (this.StatusMessage != expectedMessage)
                    {
                        return;
                    }

                    this.StatusOpacity = 1.0 - ((double)i / fadeSteps);
                }

                if (this.StatusMessage == expectedMessage)
                {
                    this.StatusMessage = string.Empty;
                    this.StatusOpacity = 1.0;
                    this.IsBusy = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when status message is replaced.
            }
        }

        private void CancelStatusLifetime()
        {
            if (this.statusLifetimeCts == null)
            {
                return;
            }

            this.statusLifetimeCts.Cancel();
            this.statusLifetimeCts.Dispose();
            this.statusLifetimeCts = null;
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.OnDispose();
                this.disposed = true;
            }
        }
    }
}
