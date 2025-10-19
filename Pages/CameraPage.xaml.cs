using VisionFocus.Services;
using VisionFocus.Core.Models;

namespace VisionFocus
{
    /// <summary>
    /// Camera Page - UI event handling and service coordination with session tracking
    /// </summary>
    public partial class CameraPage : ContentPage
    {
        // Services (Using interface types - demonstrates interface usage)
        private EyeMonitoringService? _monitoringService;
        private ICameraService? _cameraService;
        private ITimerService? _timerService;

        // Session tracking
        private DateTime _sessionStartDate;
        private TimeSpan _sessionStartTime;
        private int _sessionDurationMinutes;
        private Dictionary<int, int> _alertsByMinute = new Dictionary<int, int>();
        private int _currentMinute = 0;
        private int _totalAlertCount = 0;

        // Constants
        private const int MONITORING_START_DELAY_MS = 1000;

        // Selected subject
        private string _selectedSubject = string.Empty;

        // Debug mode settings
        private bool _isDebugMode = false;
        private string _debugImageFileName = "Closed.jpg";

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
        /// Load subjects list from settings
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
        /// Initialize session tracking data structures
        /// </summary>
        private void InitializeSessionTracking(int durationMinutes)
        {
            _sessionStartDate = DateTime.Now.Date;
            _sessionStartTime = DateTime.Now.TimeOfDay;
            _sessionDurationMinutes = durationMinutes;
            _alertsByMinute.Clear();
            _currentMinute = 0;
            _totalAlertCount = 0;

            // Initialize all minutes with 0 alerts
            for (int i = 0; i < durationMinutes; i++)
            {
                _alertsByMinute[i] = 0;
            }

            System.Diagnostics.Debug.WriteLine($"Session tracking initialized: {durationMinutes} minutes");
        }

        /// <summary>
        /// Record alert for current minute
        /// </summary>
        private void RecordAlert()
        {
            if (_alertsByMinute.ContainsKey(_currentMinute))
            {
                _alertsByMinute[_currentMinute]++;
                _totalAlertCount++;
                System.Diagnostics.Debug.WriteLine($"Alert recorded: Minute {_currentMinute}, Total {_totalAlertCount}");
            }
        }

        /// <summary>
        /// Save session data to CSV files
        /// </summary>
        private void SaveSessionData()
        {
            try
            {
                // Create session summary
                var summary = new SessionSummary
                {
                    Date = _sessionStartDate,
                    StartTime = _sessionStartTime,
                    Subject = _selectedSubject,
                    SessionDurationMinutes = _sessionDurationMinutes,
                    TotalAlertCount = _totalAlertCount
                };

                // Create session detail
                var detail = new SessionDetail
                {
                    Date = _sessionStartDate,
                    StartTime = _sessionStartTime,
                    Subject = _selectedSubject,
                    MinuteData = _alertsByMinute
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => new MinuteAlertData
                        {
                            MinuteIndex = kvp.Key,
                            AlertCount = kvp.Value
                        })
                        .ToList()
                };

                // Save both summary and detail
                SessionDataService.SaveSessionSummary(summary);
                SessionDataService.SaveSessionDetail(detail);

                System.Diagnostics.Debug.WriteLine("Session data saved successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session data: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Error", $"Failed to save session data: {ex.Message}", "OK");
                });
            }
        }

        /// <summary>
        /// Initialize camera and services
        /// </summary>
        private async Task InitializeServicesAsync()
        {
            try
            {
                var settings = SettingsService.LoadSettings();

                // Initialize session tracking
                InitializeSessionTracking(settings.SessionDurationMinutes);

                // Initialize camera service (using interface type)
                _cameraService = new CameraService();
                _cameraService.FrameCaptured += OnFrameCaptured;
                _cameraService.ErrorOccurred += OnCameraError;
                _cameraService.CameraStarted += OnCameraStarted;
                _cameraService.CameraStopped += OnCameraStopped;

                // Initialize monitoring service with alert volume
                _monitoringService = new EyeMonitoringService();
                _monitoringService.AlertThresholdSeconds = settings.AlertThresholdSeconds;
                _monitoringService.WarningThresholdSeconds = settings.WarningThresholdSeconds;
                _monitoringService.AlertVolume = settings.AlertVolume;
                _monitoringService.LogEntryAdded += OnLogEntryAdded;
                _monitoringService.EyeStateChanged += OnEyeStateChanged;
                _monitoringService.AlertTriggered += OnAlertTriggered;
                _monitoringService.WarningTriggered += OnWarningTriggered;

                // Initialize timer service (using interface type)
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

                // Set debug mode if enabled
                if (_isDebugMode)
                {
                    _monitoringService.SetDebugMode(_isDebugMode, _debugImageFileName);
                }

                // Update UI
                StartButton.IsVisible = false;
                ControlButtons.IsVisible = true;

                // Start monitoring after delay
                await Task.Delay(MONITORING_START_DELAY_MS);
                _monitoringService.StartMonitoring();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Initialization error: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Stop all services and optionally save session data
        /// </summary>
        /// <param name="saveData">Whether to save session data</param>
        private void StopAllServices(bool saveData = true)
        {
            // Stop monitoring
            _monitoringService?.StopMonitoring();

            // Stop camera
            _cameraService?.StopCamera();

            // Stop timer
            _timerService?.Stop();

            // Save session data if requested
            if (saveData && _totalAlertCount >= 0)
            {
                SaveSessionData();
            }

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
        /// Handle captured frame from camera
        /// </summary>
        private void OnFrameCaptured(object? sender, byte[] imageBytes)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
            });
        }

        /// <summary>
        /// Handle camera errors
        /// </summary>
        private void OnCameraError(object? sender, string errorMessage)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Camera Error", errorMessage, "OK");
            });
        }

        /// <summary>
        /// Handle camera started event
        /// </summary>
        private void OnCameraStarted(object? sender, EventArgs e)
        {
            // Additional processing if needed
        }

        /// <summary>
        /// Handle camera stopped event
        /// </summary>
        private void OnCameraStopped(object? sender, EventArgs e)
        {
            // Additional processing if needed
        }

        #endregion

        #region Event Handlers - Monitoring Service

        /// <summary>
        /// Handle new log entry from monitoring service
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

                Dispatcher.Dispatch(async () =>
                {
                    await Task.Delay(50);
                    await LogScrollView.ScrollToAsync(label, ScrollToPosition.End, true);
                });

                // Limit log entries to prevent memory issues
                if (LogContainer.Children.Count > 100)
                {
                    LogContainer.Children.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// Handle eye state change
        /// </summary>
        private void OnEyeStateChanged(object? sender, EyeState eyeState)
        {
            // Additional processing if needed
        }

        /// <summary>
        /// Handle alert triggered event
        /// </summary>
        private void OnAlertTriggered(object? sender, EventArgs e)
        {
            // Record alert for current minute
            RecordAlert();
        }

        /// <summary>
        /// Handle warning triggered event
        /// </summary>
        private void OnWarningTriggered(object? sender, EventArgs e)
        {
            // Additional processing if needed
        }

        #endregion

        #region Event Handlers - Timer Service

        /// <summary>
        /// Handle timer update event
        /// </summary>
        private void OnTimeUpdated(object? sender, int remainingSeconds)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_timerService != null)
                {
                    TimerLabel.Text = _timerService.FormattedTime;

                    // Calculate current minute (elapsed time)
                    int totalSeconds = _sessionDurationMinutes * 60;
                    int elapsedSeconds = totalSeconds - remainingSeconds;
                    int newMinute = elapsedSeconds / 60;

                    // Update current minute if it changed
                    if (newMinute != _currentMinute && newMinute < _sessionDurationMinutes)
                    {
                        _currentMinute = newMinute;
                        System.Diagnostics.Debug.WriteLine($"Minute changed to: {_currentMinute}");
                    }
                }
            });
        }

        /// <summary>
        /// Handle session completed event
        /// </summary>
        private void OnSessionCompleted(object? sender, EventArgs e)
        {
            StopAllServices(saveData: true);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Session Complete",
                    $"Session ended!\nTotal alerts: {_totalAlertCount}",
                    "OK");
            });
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Handle back button click
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            bool isRunning = ControlButtons.IsVisible;

            if (isRunning)
            {
                bool answer = await DisplayAlert(
                    "Confirm",
                    "Session is running. Stop and discard data?",
                    "Yes",
                    "No"
                );

                if (!answer) return;

                StopAllServices(saveData: false);
            }

            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Handle start button click
        /// </summary>
        private async void OnStartClicked(object sender, EventArgs e)
        {
            await InitializeServicesAsync();
        }

        /// <summary>
        /// Handle pause/resume button click
        /// </summary>
        private void OnPauseClicked(object sender, EventArgs e)
        {
            _timerService?.TogglePause();
            _monitoringService?.TogglePause();

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
        /// Handle stop button click
        /// </summary>
        private async void OnStopClicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert(
                "Confirm",
                "Stop session and save data?",
                "Yes",
                "No"
            );

            if (answer)
            {
                StopAllServices(saveData: true);
                await DisplayAlert("Saved",
                    $"Session data saved!\nTotal alerts: {_totalAlertCount}",
                    "OK");
            }
        }

        /// <summary>
        /// Handle subject selection change
        /// </summary>
        private void OnSubjectChanged(object sender, EventArgs e)
        {
            if (SubjectPicker.SelectedIndex >= 0)
            {
                _selectedSubject = SubjectPicker.SelectedItem?.ToString() ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"Selected subject: {_selectedSubject}");
            }
        }

        /// <summary>
        /// Handle debug mode toggle
        /// </summary>
        private void OnDebugModeToggled(object sender, ToggledEventArgs e)
        {
            _isDebugMode = e.Value;
            EyeStateToggleContainer.IsVisible = e.Value;

            System.Diagnostics.Debug.WriteLine($"🔧 Debug mode toggle: {(_isDebugMode ? "ON" : "OFF")}");
            System.Diagnostics.Debug.WriteLine($"📁 Current debug file: {_debugImageFileName}");

            if (_monitoringService != null)
            {
                _monitoringService.SetDebugMode(_isDebugMode, _debugImageFileName);
                System.Diagnostics.Debug.WriteLine($"✅ Applied to MonitoringService: SetDebugMode({_isDebugMode}, {_debugImageFileName})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ MonitoringService not yet initialized");
            }
        }

        /// <summary>
        /// Handle eye state radio button change in debug mode
        /// </summary>
        private void OnEyeStateRadioChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;

            var radioButton = sender as RadioButton;
            if (radioButton == ClosedRadioButton)
            {
                _debugImageFileName = "Closed.jpg";
                System.Diagnostics.Debug.WriteLine("Debug image: Closed.jpg");
            }
            else if (radioButton == OpenRadioButton)
            {
                _debugImageFileName = "Open.jpg";
                System.Diagnostics.Debug.WriteLine("Debug image: Open.jpg");
            }

            // Update monitoring service if running
            if (_monitoringService != null && _isDebugMode)
            {
                _monitoringService.SetDebugMode(_isDebugMode, _debugImageFileName);
            }
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Get color for log level
        /// </summary>
        /// <param name="level">Log level</param>
        /// <returns>Color for the log level</returns>
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

        /// <summary>
        /// Handle page disappearing
        /// </summary>
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            bool isRunning = ControlButtons.IsVisible;
            if (isRunning)
            {
                StopAllServices(saveData: false);
            }

            // Dispose services
            _cameraService?.Dispose();
            _monitoringService?.Dispose();
            _timerService?.Dispose();
        }

        #endregion
    }
}