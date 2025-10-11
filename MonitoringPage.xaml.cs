using System.Diagnostics;

namespace VisionFocus
{
    public partial class MonitoringPage : ContentPage
    {
        private bool _isMonitoring = false;
        private CancellationTokenSource? _cancellationTokenSource;

        // Track eyes closed state
        private DateTime? _eyesClosedStartTime = null;
        private double _consecutiveClosedDuration = 0; // Duration in seconds
        private const double ALERT_THRESHOLD = 5.0; // Alert after 5 seconds
        private const double WARNING_THRESHOLD = 3.0; // Warning after 3 seconds

        // Monitoring interval in milliseconds
        private const int MONITORING_INTERVAL_MS = 1000; // Check every 1 second

        public MonitoringPage()
        {
            InitializeComponent();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            // Stop monitoring before going back
            if (_isMonitoring)
            {
                StopMonitoring();
            }
            await Shell.Current.GoToAsync("..");
        }

        private async void OnStartClicked(object sender, EventArgs e)
        {
            try
            {
                // Check if there are saved images
                var imagePaths = ImageHelper.GetAllImagePaths();
                if (imagePaths.Count == 0)
                {
                    await DisplayAlert("Error", "No images found. Please capture an image with the camera first.", "OK");
                    return;
                }

                // Start monitoring
                _isMonitoring = true;
                _cancellationTokenSource = new CancellationTokenSource();

                // Initialize tracking variables
                _eyesClosedStartTime = null;
                _consecutiveClosedDuration = 0;

                // Update UI
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                StatusLabel.Text = "Monitoring...";
                LogContainer.Children.Clear();

                AddLogEntry("🟢 Monitoring started", LogLevel.Success);

                // Start monitoring loop
                _ = Task.Run(() => MonitoringLoopAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to start monitoring: {ex.Message}", "OK");
                AddLogEntry($"❌ Error: {ex.Message}", LogLevel.Error);
            }
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            StopMonitoring();
        }

        private void StopMonitoring()
        {
            if (_isMonitoring)
            {
                _isMonitoring = false;
                _cancellationTokenSource?.Cancel();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    StatusLabel.Text = "Stop monitoring";
                    AddLogEntry("⏹️ Stop monitoring", LogLevel.Info);
                });
            }
        }

        /// <summary>
        /// Monitoring loop: continuously calls API and monitors eye state
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (_isMonitoring && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Get latest image
                    var imagePaths = ImageHelper.GetAllImagePaths();
                    if (imagePaths.Count == 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            AddLogEntry("⚠️ No images found", LogLevel.Warning);
                        });
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    string latestImagePath = imagePaths[0];
                    string fileName = Path.GetFileName(latestImagePath);

                    // Record API call start time
                    var startTime = DateTime.Now;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"📤 Sending to API... ({fileName})", LogLevel.Info);
                    });

                    // Call Roboflow API
                    string jsonResponse = await RoboflowService.InferImageAsync(latestImagePath);

                    // Record API call end time
                    var endTime = DateTime.Now;
                    var responseTime = (endTime - startTime).TotalMilliseconds;

                    // Parse response
                    string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                    // Determine eye state
                    EyeState eyeState = DetermineEyeState(parsedResult);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"📥 API response received ({responseTime:F0}ms)", LogLevel.Info);
                        ProcessEyeState(eyeState, parsedResult);
                    });

                    Debug.WriteLine($"API Response: {jsonResponse}");
                }
                catch (OperationCanceledException)
                {
                    // Cancelled - normal exit
                    break;
                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"❌ Error: {ex.Message}", LogLevel.Error);
                    });
                    Debug.WriteLine($"Monitoring error: {ex}");
                }

                // Wait before next check
                try
                {
                    await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Determine eye state from API response
        /// </summary>
        private EyeState DetermineEyeState(string parsedResult)
        {
            if (string.IsNullOrWhiteSpace(parsedResult))
                return EyeState.Unknown;

            // Check for "No detection found"
            if (parsedResult.Contains("No detection found") ||
                parsedResult.Contains("no detection"))
            {
                return EyeState.Unknown;
            }

            // Check if eyes are closed
            if (parsedResult.Contains("eyes_closed", StringComparison.OrdinalIgnoreCase) ||
                parsedResult.Contains("closed", StringComparison.OrdinalIgnoreCase))
            {
                return EyeState.Closed;
            }

            // Check if eyes are open
            if (parsedResult.Contains("eyes_open", StringComparison.OrdinalIgnoreCase) ||
                parsedResult.Contains("open", StringComparison.OrdinalIgnoreCase))
            {
                return EyeState.Open;
            }

            return EyeState.Unknown;
        }

        /// <summary>
        /// Process eye state and trigger alerts if needed
        /// </summary>
        private void ProcessEyeState(EyeState eyeState, string detectionDetails)
        {
            var now = DateTime.Now;

            switch (eyeState)
            {
                case EyeState.Closed:
                    // Eyes are closed
                    if (_eyesClosedStartTime == null)
                    {
                        // First detection of closed eyes
                        _eyesClosedStartTime = now;
                        _consecutiveClosedDuration = 0;
                        AddLogEntry("👁️ Eyes closed detected", LogLevel.Warning);
                    }
                    else
                    {
                        // Eyes continuously closed
                        _consecutiveClosedDuration = (now - _eyesClosedStartTime.Value).TotalSeconds;

                        if (_consecutiveClosedDuration >= ALERT_THRESHOLD)
                        {
                            // Closed for 5+ seconds - ALERT
                            AddLogEntry($"🚨 [ALERT] Eyes closed for {_consecutiveClosedDuration:F1} seconds!", LogLevel.Alert);
                            StatusLabel.Text = $"🚨 ALERT! Eyes closed for {_consecutiveClosedDuration:F1}s";
                        }
                        else if (_consecutiveClosedDuration >= WARNING_THRESHOLD)
                        {
                            // Closed for 3+ seconds - WARNING
                            AddLogEntry($"⚠️ [WARNING] Eyes closed for {_consecutiveClosedDuration:F1} seconds", LogLevel.Warning);
                            StatusLabel.Text = $"⚠️ Eyes closed for {_consecutiveClosedDuration:F1}s";
                        }
                        else
                        {
                            // Normal tracking
                            StatusLabel.Text = $"Eyes closed for {_consecutiveClosedDuration:F1}s";
                        }
                    }
                    break;

                case EyeState.Open:
                    // Eyes are open
                    if (_eyesClosedStartTime != null)
                    {
                        // Eyes opened, reset closed duration
                        var closedDuration = (now - _eyesClosedStartTime.Value).TotalSeconds;

                        if (closedDuration >= ALERT_THRESHOLD)
                        {
                            AddLogEntry($"✅ Eyes opened (were closed for {closedDuration:F1}s)", LogLevel.Success);
                        }
                        else if (closedDuration >= WARNING_THRESHOLD)
                        {
                            AddLogEntry($"👁️ Eyes opened (were closed for {closedDuration:F1}s)", LogLevel.Info);
                        }
                        else
                        {
                            AddLogEntry("👁️ Eyes opened", LogLevel.Success);
                        }

                        _eyesClosedStartTime = null;
                        _consecutiveClosedDuration = 0;
                    }
                    StatusLabel.Text = "✅ Eyes are open";
                    break;

                case EyeState.Unknown:
                    // Could not detect
                    AddLogEntry("❓ Could not detect eye state", LogLevel.Warning);
                    StatusLabel.Text = "❓ Detection failed";
                    // Reset counter when detection fails
                    _eyesClosedStartTime = null;
                    _consecutiveClosedDuration = 0;
                    break;
            }
        }

        /// <summary>
        /// Add a log entry to the display
        /// </summary>
        private void AddLogEntry(string message, LogLevel level)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logText = $"[{timestamp}] {message}";

            var label = new Label
            {
                Text = logText,
                FontSize = 14,
                Padding = new Thickness(5),
                TextColor = GetLogColor(level)
            };

            LogContainer.Children.Add(label);

            // auto-scroll to bottom    
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                await LogScrollView.ScrollToAsync(label, ScrollToPosition.End, true);
            });

            // if log exceeds 100 entries, remove oldest
            if (LogContainer.Children.Count > 100)
            {
                LogContainer.Children.RemoveAt(0);
            }
        }

        /// <summary>
        /// get color based on log level
        /// </summary>
        private Color GetLogColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Success => Colors.Green,
                LogLevel.Info => Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
                LogLevel.Warning => Colors.Orange,
                LogLevel.Error => Colors.Red,
                LogLevel.Alert => Colors.Red,
                _ => Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
            };
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopMonitoring();
        }

        /// <summary>
        /// eye state enumeration
        /// </summary>
        private enum EyeState
        {
            Open,      
            Closed,    
            Unknown    
        }

        /// <summary>
        /// Log level for color coding
        /// </summary>
        private enum LogLevel
        {
            Success,   
            Info,     
            Warning,  
            Error,     
            Alert    
        }
    }
}