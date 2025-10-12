using VisionFocus.Services;

namespace VisionFocus
{
    /// <summary>
    /// Camera Page - UI event handling and service coordination only
    /// </summary>
    public partial class CameraPage : ContentPage
    {
        // Services
        private CameraService? _cameraService;
        private EyeMonitoringService? _monitoringService;
        private SessionTimerService? _timerService;

        // Constants
        private const int MONITORING_START_DELAY_MS = 1000; // Delay before monitoring starts

        // Selected subject
        private string _selectedSubject = string.Empty;

        public CameraPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadSubjects();
        }

        /// <summary>
        /// Load subjects list
        /// </summary>
        private void LoadSubjects()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                SubjectPicker.ItemsSource = settings.Subjects;

                if (settings.Subjects.Count > 0)
                {
                    SubjectPicker.SelectedIndex = 0;
                    _selectedSubject = settings.Subjects[0];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Subject loading error: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize camera and services
        /// </summary>
        private async Task InitializeServicesAsync()
        {
            try
            {
                // Initialize camera service
                _cameraService = new CameraService();
                _cameraService.FrameCaptured += OnFrameCaptured;
                _cameraService.ErrorOccurred += OnCameraError;
                _cameraService.CameraStarted += OnCameraStarted;
                _cameraService.CameraStopped += OnCameraStopped;

                // Initialize monitoring service
                _monitoringService = new EyeMonitoringService();

                // Load thresholds from settings
                var settings = SettingsService.LoadSettings();
                _monitoringService.AlertThresholdSeconds = settings.AlertThresholdSeconds;
                _monitoringService.WarningThresholdSeconds = settings.WarningThresholdSeconds;

                _monitoringService.LogEntryAdded += OnLogEntryAdded;
                _monitoringService.EyeStateChanged += OnEyeStateChanged;
                _monitoringService.AlertTriggered += OnAlertTriggered;
                _monitoringService.WarningTriggered += OnWarningTriggered;

                // Initialize timer service
                _timerService = new SessionTimerService();
                _timerService.TimeUpdated += OnTimeUpdated;
                _timerService.SessionCompleted += OnSessionCompleted;

                // Start camera
                bool cameraStarted = await _cameraService.StartCameraAsync();
                if (!cameraStarted)
                {
                    await DisplayAlert("Error", "Camera initialization failed", "OK");
                    return;
                }

                // Start timer
                _timerService.Start(settings.SessionDurationMinutes);

                // Update UI
                StartButton.IsVisible = false;
                ControlButtons.IsVisible = true;

                // Start monitoring after 1 second
                await Task.Delay(MONITORING_START_DELAY_MS);
                _monitoringService.StartMonitoring();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Initialization error: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Stop all services
        /// </summary>
        private void StopAllServices()
        {
            // Stop monitoring
            _monitoringService?.StopMonitoring();

            // Stop camera
            _cameraService?.StopCamera();

            // Stop timer
            _timerService?.Stop();

            // Update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StartButton.IsVisible = true;
                ControlButtons.IsVisible = false;

                // Reset pause button
                PauseButton.Text = "⏸";
                PauseButton.BackgroundColor = Color.FromArgb("#FF9800");
            });
        }

        #region Event Handlers - Camera Service

        /// <summary>
        /// Frame captured event
        /// </summary>
        private void OnFrameCaptured(object? sender, byte[] imageBytes)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
            });
        }

        /// <summary>
        /// Camera error event
        /// </summary>
        private void OnCameraError(object? sender, string errorMessage)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Camera Error", errorMessage, "OK");
            });
        }

        /// <summary>
        /// Camera started event
        /// </summary>
        private void OnCameraStarted(object? sender, EventArgs e)
        {
            // Additional processing if needed
        }

        /// <summary>
        /// Camera stopped event
        /// </summary>
        private void OnCameraStopped(object? sender, EventArgs e)
        {
            // Additional processing if needed
        }

        #endregion

        #region Event Handlers - Monitoring Service

        /// <summary>
        /// Log entry added event
        /// </summary>
        private void OnLogEntryAdded(object? sender, LogEntry entry)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var label = new Label
                {
                    Text = entry.FormattedMessage,
                    FontSize = 12,
                    Padding = new Thickness(5, 2),
                    TextColor = GetLogColor(entry.Level)
                };

                LogContainer.Children.Add(label);

                // Auto-scroll
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(50);
                    await LogScrollView.ScrollToAsync(label, ScrollToPosition.End, true);
                });

                // Remove oldest log if exceeds 100 entries
                if (LogContainer.Children.Count > 100)
                {
                    LogContainer.Children.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// Eye state changed event
        /// </summary>
        private void OnEyeStateChanged(object? sender, EyeState eyeState)
        {
            // Additional processing if needed (e.g., UI changes based on state)
        }

        /// <summary>
        /// Alert triggered event
        /// </summary>
        private void OnAlertTriggered(object? sender, EventArgs e)
        {
            // Additional processing if needed (e.g., turn screen red)
        }

        /// <summary>
        /// Warning triggered event
        /// </summary>
        private void OnWarningTriggered(object? sender, EventArgs e)
        {
            // Additional processing if needed (e.g., turn screen yellow)
        }

        #endregion

        #region Event Handlers - Timer Service

        /// <summary>
        /// Time updated event
        /// </summary>
        private void OnTimeUpdated(object? sender, int remainingSeconds)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_timerService != null)
                {
                    TimerLabel.Text = _timerService.FormattedTime;
                }
            });
        }

        /// <summary>
        /// Session completed event
        /// </summary>
        private void OnSessionCompleted(object? sender, EventArgs e)
        {
            StopAllServices();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Session Complete", "The session has ended", "OK");
            });
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Back button click
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            StopAllServices();
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Start button click
        /// </summary>
        private async void OnStartClicked(object sender, EventArgs e)
        {
            await InitializeServicesAsync();
        }

        /// <summary>
        /// Pause button click
        /// </summary>
        private void OnPauseClicked(object sender, EventArgs e)
        {
            // Toggle pause for timer and monitoring
            _timerService?.TogglePause();
            _monitoringService?.TogglePause();

            // Toggle button display
            bool isPaused = _timerService?.IsPaused ?? false;

            if (isPaused)
            {
                PauseButton.Text = "▶";
                PauseButton.BackgroundColor = Color.FromArgb("#4CAF50");
            }
            else
            {
                PauseButton.Text = "⏸";
                PauseButton.BackgroundColor = Color.FromArgb("#FF9800");
            }
        }

        /// <summary>
        /// Stop button click
        /// </summary>
        private void OnStopClicked(object sender, EventArgs e)
        {
            StopAllServices();
        }

        /// <summary>
        /// Subject changed event
        /// </summary>
        private void OnSubjectChanged(object sender, EventArgs e)
        {
            if (SubjectPicker.SelectedIndex >= 0)
            {
                _selectedSubject = SubjectPicker.SelectedItem?.ToString() ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"Selected subject: {_selectedSubject}");

                // If needed, log the selected subject
                // Example: Save subject information when starting a session
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get color based on log level
        /// </summary>
        private Color GetLogColor(LogLevel level)
        {
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

            return level switch
            {
                LogLevel.Success => Colors.Green,
                LogLevel.Info => isDark ? Colors.White : Colors.Black,
                LogLevel.Warning => Colors.Orange,
                LogLevel.Error => Colors.Red,
                LogLevel.Alert => Colors.Red,
                _ => isDark ? Colors.White : Colors.Black
            };
        }

        #endregion

        #region Lifecycle

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopAllServices();

            // Dispose resources
            _cameraService?.Dispose();
            _monitoringService?.Dispose();
            _timerService?.Dispose();
        }

        #endregion
    }
}