using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Text.Json;
using VisionFocus.Utilities;

namespace VisionFocus.Services
{
    /// <summary>
    /// Service for monitoring eye state and triggering alerts
    /// Demonstrates polymorphism through ImageSourceStrategy pattern
    /// </summary>
    public class EyeMonitoringService : IDisposable
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitoringTask;
        private DateTime _eyesClosedStartTime;
        private DateTime _lastAlertTime = DateTime.MinValue;
        private bool _eyesClosed = false;
        private bool _warningTriggered = false;
        private bool _alertTriggered = false;
        private bool _isPaused = false;

        // Polymorphism: Strategy pattern for image source selection
        private ImageSourceStrategyBase _imageSourceStrategy;

        // Settings
        public double AlertThresholdSeconds { get; set; } = 5.0;
        public double WarningThresholdSeconds { get; set; } = 3.0;
        public double AlertRepeatIntervalSeconds { get; set; } = 2.0;
        public double AlertVolume { get; set; } = 0.8;

        private const int MIN_DELAY_BETWEEN_CHECKS_MS = 100; // Minimum wait time between API calls

        // Events
        public event EventHandler<LogEntry>? LogEntryAdded;
        public event EventHandler<EyeState>? EyeStateChanged;
        public event EventHandler? AlertTriggered;
        public event EventHandler? WarningTriggered;

        /// <summary>
        /// Constructor - initializes with camera image strategy by default
        /// </summary>
        public EyeMonitoringService()
        {
            // Default: use real camera feed
            _imageSourceStrategy = new CameraImageStrategy();
            AddLog(LogLevel.Info, "Monitoring service initialized");
        }

        /// <summary>
        /// Set debug mode - switches between camera and debug image strategies
        /// Demonstrates polymorphism: different strategies, same interface
        /// </summary>
        public void SetDebugMode(bool enabled, string debugImageFileName = "Closed.jpg")
        {
            if (enabled)
            {
                // Switch to debug strategy OR update existing debug strategy
                if (_imageSourceStrategy is DebugImageStrategy debugStrategy)
                {
                    // Already in debug mode, just change the image
                    debugStrategy.SetDebugImage(debugImageFileName);
                    AddLog(LogLevel.Info, $"?? Debug image changed to: {debugImageFileName}");
                }
                else
                {
                    // Switch from camera to debug mode
                    _imageSourceStrategy = new DebugImageStrategy(debugImageFileName);
                    AddLog(LogLevel.Info, $"?? Debug mode enabled: {debugImageFileName}");
                }

                Debug.WriteLine($"?? Strategy: {_imageSourceStrategy.GetDescription()}");
            }
            else
            {
                // Switch back to camera strategy
                _imageSourceStrategy = new CameraImageStrategy();
                AddLog(LogLevel.Info, "?? Debug mode disabled");
                Debug.WriteLine($"?? Strategy: {_imageSourceStrategy.GetDescription()}");
            }
        }

        /// <summary>
        /// Start monitoring
        /// </summary>
        public void StartMonitoring()
        {
            // Stop if already running
            if (_monitoringTask != null)
            {
                StopMonitoring();
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cancellationTokenSource.Token));

            AddLog(LogLevel.Success, $"? Monitoring started ({_imageSourceStrategy.GetDescription()})");
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
            _monitoringTask?.Wait(TimeSpan.FromSeconds(2));
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _monitoringTask = null;

            AddLog(LogLevel.Info, "? Monitoring stopped");
        }

        /// <summary>
        /// Toggle pause/resume
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;
            AddLog(LogLevel.Info, _isPaused ? "? Monitoring paused" : "? Monitoring resumed");
        }

        /// <summary>
        /// Monitoring loop (continuous API calls)
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_isPaused)
                    {
                        await CheckEyeStateAsync();
                    }

                    // Minimum wait time between API calls
                    await Task.Delay(MIN_DELAY_BETWEEN_CHECKS_MS, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal exit when cancelled
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Monitoring error: {ex.Message}");
                    AddLog(LogLevel.Error, $"? Monitoring error: {ex.Message}");
                    // Continue monitoring even if error occurs
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Check eye state from image
        /// Demonstrates polymorphism: uses strategy pattern to get image path
        /// </summary>
        private async Task CheckEyeStateAsync()
        {
            try
            {
                // POLYMORPHISM IN ACTION: 
                // Call the same method regardless of which strategy is active
                // The actual behavior depends on the concrete strategy type
                string imagePath = await _imageSourceStrategy.GetImagePathAsync();

                AddLog(LogLevel.Info, $"?? Checking: {_imageSourceStrategy.GetDescription()}");
                Debug.WriteLine($"?? Image path: {imagePath}");

                if (!File.Exists(imagePath))
                {
                    AddLog(LogLevel.Error, $"? Image not found: {imagePath}");
                    return;
                }

                // Call Roboflow API
                string jsonResponse = await RoboflowService.InferImageAsync(imagePath);

                // Log API call success
                AddLog(LogLevel.Info, $"? API response received");

                // Output full response in debug mode
                if (_imageSourceStrategy is DebugImageStrategy)
                {
                    AddLog(LogLevel.Info, $"?? [Debug] API response: {jsonResponse}");
                    Debug.WriteLine($"?? Full response: {jsonResponse}");
                }

                bool eyesOpen = ParseEyeState(jsonResponse);

                // Log parse result
                AddLog(LogLevel.Info, $"??? Result: {(eyesOpen ? "Eyes open" : "Eyes closed")}");

                ProcessEyeState(eyesOpen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Eye state check error: {ex.Message}");
                AddLog(LogLevel.Error, $"? Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse Roboflow response
        /// </summary>
        private bool ParseEyeState(string jsonResponse)
        {
            try
            {
                // Detailed log: Output full response
                AddLog(LogLevel.Info, $"?? Full JSON: {jsonResponse}");
                Debug.WriteLine($"?? Full JSON: {jsonResponse}");

                // Parse JSON
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;

                // Check predictions array
                if (root.TryGetProperty("predictions", out JsonElement predictions))
                {
                    int count = predictions.GetArrayLength();
                    AddLog(LogLevel.Info, $"?? Detection count: {count}");
                    Debug.WriteLine($"?? Predictions array length: {count}");

                    if (count == 0)
                    {
                        AddLog(LogLevel.Warning, "?? No predictions found - defaulting to Eyes Open");
                        Debug.WriteLine("?? Empty predictions array");
                        return true; // Default: eyes open
                    }

                    // Check each prediction
                    bool hasOpen = false;
                    bool hasClosed = false;

                    foreach (JsonElement prediction in predictions.EnumerateArray())
                    {
                        if (prediction.TryGetProperty("class", out JsonElement classElement))
                        {
                            string className = classElement.GetString()?.ToLower() ?? "";
                            double confidence = prediction.TryGetProperty("confidence", out JsonElement confElement)
                                ? confElement.GetDouble()
                                : 0.0;

                            // Detailed log: Detected class name and confidence
                            AddLog(LogLevel.Info, $"??? Class: '{className}', Confidence: {confidence:P1}");
                            Debug.WriteLine($"   „¤„Ÿ Class: '{className}', Confidence: {confidence}");

                            // Check class name
                            if (className.Contains("open"))
                            {
                                hasOpen = true;
                                AddLog(LogLevel.Success, $"? 'Open' detected in class: '{className}'");
                            }
                            else if (className.Contains("closed"))
                            {
                                hasClosed = true;
                                AddLog(LogLevel.Warning, $"?? 'Closed' detected in class: '{className}'");
                            }
                            else
                            {
                                AddLog(LogLevel.Info, $"? Unknown class: '{className}'");
                            }
                        }
                    }

                    // Log determination result
                    AddLog(LogLevel.Info, $"?? hasOpen={hasOpen}, hasClosed={hasClosed}");
                    Debug.WriteLine($"?? Final detection: hasOpen={hasOpen}, hasClosed={hasClosed}");

                    // Priority: Closed > Open > Default
                    if (hasClosed)
                    {
                        AddLog(LogLevel.Alert, "?? Final result: Eyes CLOSED");
                        return false;
                    }

                    if (hasOpen)
                    {
                        AddLog(LogLevel.Success, "? Final result: Eyes OPEN");
                        return true;
                    }

                    // If neither is detected
                    AddLog(LogLevel.Warning, "?? Neither open nor closed detected - defaulting to Eyes Open");
                    return true;
                }
                else
                {
                    AddLog(LogLevel.Error, "? No 'predictions' property in response");
                    Debug.WriteLine("? Missing predictions property in JSON");
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"? Parse error: {ex.Message}");
                Debug.WriteLine($"? Parse exception: {ex}");
                return true; // Default to eyes open on error
            }
        }

        /// <summary>
        /// Process eye state and trigger alerts
        /// </summary>
        private void ProcessEyeState(bool eyesOpen)
        {
            if (!eyesOpen)
            {
                if (!_eyesClosed)
                {
                    // Eyes just closed
                    _eyesClosedStartTime = DateTime.Now;
                    _eyesClosed = true;
                    _warningTriggered = false;
                    _alertTriggered = false;

                    EyeStateChanged?.Invoke(this, EyeState.Closed);
                    AddLog(LogLevel.Warning, "??? Eyes closed detected");
                }
                else
                {
                    // Eyes still closed - check duration
                    double closedDuration = (DateTime.Now - _eyesClosedStartTime).TotalSeconds;

                    // Warning threshold
                    if (!_warningTriggered && closedDuration >= WarningThresholdSeconds)
                    {
                        _warningTriggered = true;
                        WarningTriggered?.Invoke(this, EventArgs.Empty);
                        AddLog(LogLevel.Warning, $"?? Eyes closed for {closedDuration:F1}s");
                    }

                    // Alert threshold - INTERVAL-BASED ALERT (every 2 seconds)
                    if (closedDuration >= AlertThresholdSeconds)
                    {
                        // Check time since last alert
                        double timeSinceLastAlert = (DateTime.Now - _lastAlertTime).TotalSeconds;

                        // Trigger on first occurrence or after repeat interval
                        if (!_alertTriggered || timeSinceLastAlert >= AlertRepeatIntervalSeconds)
                        {
                            // Trigger event only once per closed-eye session
                            if (!_alertTriggered)
                            {
                                _alertTriggered = true;
                                AlertTriggered?.Invoke(this, EventArgs.Empty);
                            }

                            // Play alert sound
                            AlertSoundService.PlaySound(AlertVolume);

                            // Update last alert time
                            _lastAlertTime = DateTime.Now;

                            // Log the alert
                            AddLog(LogLevel.Alert, $"?? ALERT! Eyes closed for {closedDuration:F1}s");
                        }
                    }
                }
            }
            else
            {
                if (_eyesClosed)
                {
                    // Eyes opened again - reset all flags
                    double closedDuration = (DateTime.Now - _eyesClosedStartTime).TotalSeconds;
                    _eyesClosed = false;
                    _warningTriggered = false;
                    _alertTriggered = false;
                    _lastAlertTime = DateTime.MinValue; // Reset alert timer

                    EyeStateChanged?.Invoke(this, EyeState.Open);
                    AddLog(LogLevel.Success, $"? Eyes opened (was closed for {closedDuration:F1}s)");
                }
            }
        }

        /// <summary>
        /// Add log entry
        /// </summary>
        private void AddLog(LogLevel level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            LogEntryAdded?.Invoke(this, entry);
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }

    /// <summary>
    /// Log levels
    /// </summary>
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Alert
    }

    /// <summary>
    /// Log entry model
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;

        public string FormattedMessage =>
            $"[{Timestamp:HH:mm:ss}] {Level}: {Message}";
    }

    /// <summary>
    /// Eye state enumeration
    /// </summary>
    public enum EyeState
    {
        Open,
        Closed,
        Unknown
    }
}