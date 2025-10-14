using VisionFocus.Utilities;
using System.Diagnostics;

namespace VisionFocus.Services
{
    /// <summary>
    /// Service for monitoring eye state and triggering alerts
    /// Demonstrates polymorphism with AlertStrategyBase
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

        // Polymorphic alert strategy
        private AlertStrategyBase? _alertStrategy;

        // Debug mode settings
        private bool _isDebugMode = false;
        private string _debugImageFileName = "Closed.jpg";

        // Settings
        public double AlertThresholdSeconds { get; set; } = 5.0;
        public double WarningThresholdSeconds { get; set; } = 3.0;
        public double AlertRepeatIntervalSeconds { get; set; } = 2.0;
        private const int MIN_DELAY_BETWEEN_CHECKS_MS = 100; // Minimum wait time between API calls

        // Events
        public event EventHandler<LogEntry>? LogEntryAdded;
        public event EventHandler<EyeState>? EyeStateChanged;
        public event EventHandler? AlertTriggered;
        public event EventHandler? WarningTriggered;

        /// <summary>
        /// Constructor with optional alert strategy
        /// </summary>
        public EyeMonitoringService(AlertStrategyBase? alertStrategy = null)
        {
            _alertStrategy = alertStrategy ?? AlertSoundService.CreateAlertStrategy(AlertSoundType.Beep);
        }

        /// <summary>
        /// Set alert strategy (Strategy Pattern - Polymorphism)
        /// </summary>
        public void SetAlertStrategy(AlertStrategyBase strategy)
        {
            _alertStrategy = strategy;
            AddLog(LogLevel.Info, $"Alert strategy changed to: {strategy.GetDescription()}");
        }

        /// <summary>
        /// Set alert strategy by type
        /// </summary>
        public void SetAlertStrategy(AlertSoundType soundType)
        {
            _alertStrategy = AlertSoundService.CreateAlertStrategy(soundType);
            AddLog(LogLevel.Info, $"Alert strategy changed to: {soundType}");
        }

        /// <summary>
        /// Set debug mode
        /// </summary>
        public void SetDebugMode(bool enabled, string debugImageFileName)
        {
            _isDebugMode = enabled;
            _debugImageFileName = debugImageFileName;

            if (_isDebugMode)
            {
                AddLog(LogLevel.Info, $"?? Debug mode enabled: {debugImageFileName}");
                Debug.WriteLine($"?? Debug mode: ON, File: {debugImageFileName}");
            }
            else
            {
                AddLog(LogLevel.Info, "?? Debug mode disabled");
                Debug.WriteLine("?? Debug mode: OFF");
            }
        }

        /// <summary>
        /// Start monitoring (loop-based)
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

            AddLog(LogLevel.Success, "? Monitoring started");
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

            AddLog(LogLevel.Info, "Monitoring stopped");
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
                    // Continue monitoring even if error occurs
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Check eye state from image
        /// </summary>
        private async Task CheckEyeStateAsync()
        {
            try
            {
                // Select image file based on debug mode
                string imageFileName = _isDebugMode ? _debugImageFileName : "RealtimePic.jpg";
                string imagePath = ImageHelper.GetImagePath(imageFileName);

                // ?? Debug: Log the image file name being sent
                if (_isDebugMode)
                {
                    AddLog(LogLevel.Info, $"?? Sending image: {imageFileName}");
                    Debug.WriteLine($"?? Full path: {imagePath}");
                }

                if (!File.Exists(imagePath))
                {
                    if (_isDebugMode)
                    {
                        AddLog(LogLevel.Error, $"Debug image not found: {imageFileName}");
                    }
                    return;
                }

                // Call Roboflow API
                string jsonResponse = await RoboflowService.InferImageAsync(imagePath);

                // ?? Debug: Log entire API response
                if (_isDebugMode)
                {
                    AddLog(LogLevel.Info, $"?? API response: {jsonResponse}");
                    Debug.WriteLine($"?? Full response: {jsonResponse}");
                }

                bool eyesOpen = ParseEyeState(jsonResponse);

                // ?? Debug: Log parsing result
                if (_isDebugMode)
                {
                    AddLog(LogLevel.Info, $"?? Parsed result: {(eyesOpen ? "Eyes open" : "Eyes closed")}");
                }

                ProcessEyeState(eyesOpen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Eye state check error: {ex.Message}");
                AddLog(LogLevel.Error, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse Roboflow response
        /// </summary>
        private bool ParseEyeState(string jsonResponse)
        {
            try
            {
                // Convert to lowercase for substring search
                string lowerResponse = jsonResponse.ToLower();

                // Look for classes containing "open eye" or "open"
                bool hasOpen = lowerResponse.Contains("\"class\":\"open") ||
                               lowerResponse.Contains("class\":\"open");

                // Look for classes containing "closed eye" or "closed"
                bool hasClosed = lowerResponse.Contains("\"class\":\"closed") ||
                                 lowerResponse.Contains("class\":\"closed");

                // ?? Debug: Log detected classes in detail
                if (_isDebugMode)
                {
                    Debug.WriteLine($"?? 'open' detected: {hasOpen}, 'closed' detected: {hasClosed}");

                    // Extract and display class name
                    int classIndex = lowerResponse.IndexOf("\"class\":\"");
                    if (classIndex >= 0)
                    {
                        int startIndex = classIndex + 9; // Length of "class":"
                        int endIndex = lowerResponse.IndexOf("\"", startIndex);
                        if (endIndex > startIndex)
                        {
                            string className = jsonResponse.Substring(startIndex, endIndex - startIndex);
                            Debug.WriteLine($"   Detected class name: '{className}'");
                        }
                    }
                }

                // If open is detected, eyes are open
                if (hasOpen)
                {
                    return true;
                }

                // If closed is detected, eyes are closed
                if (hasClosed)
                {
                    return false;
                }

                // Default to eyes open if neither detected
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Parse error: {ex.Message}");
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
                    AddLog(LogLevel.Warning, "?? Eyes closed detected");
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
                        AddLog(LogLevel.Warning, $"? Eyes closed for {closedDuration:F1}s");
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

                            // POLYMORPHIC CALL - Play alert at intervals
                            _alertStrategy?.Play();

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
                    AddLog(LogLevel.Success, $"??? Eyes opened (was closed for {closedDuration:F1}s)");
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