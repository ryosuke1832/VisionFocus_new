using VisionFocus.Utilities;
using System.Diagnostics;

namespace VisionFocus.Services
{
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

    /// <summary>
    /// Service for monitoring eye state and triggering alerts
    /// Demonstrates polymorphism usage with AlertStrategyBase
    /// </summary>
    public class EyeMonitoringService : IDisposable
    {
        private System.Timers.Timer? _checkTimer;
        private DateTime _eyesClosedStartTime;
        private DateTime _lastAlertTime = DateTime.MinValue;
        private bool _eyesClosed = false;
        private bool _warningTriggered = false;
        private bool _alertTriggered = false;
        private bool _isPaused = false;

        // Polymorphic alert strategy (Demonstrates Polymorphism)
        private AlertStrategyBase? _alertStrategy;

        // Debug mode settings
        private bool _isDebugMode = false;
        private string _debugImageFileName = "Closed.jpg";

        // Settings
        public double AlertThresholdSeconds { get; set; } = 5.0;
        public double WarningThresholdSeconds { get; set; } = 3.0;
        public double AlertRepeatIntervalSeconds { get; set; } = 2.0; // Repeat alert every 2 seconds
        private const int CHECK_INTERVAL_MS = 500;

        // Events
        public event EventHandler<LogEntry>? LogEntryAdded;
        public event EventHandler<EyeState>? EyeStateChanged;
        public event EventHandler? AlertTriggered;
        public event EventHandler? WarningTriggered;

        /// <summary>
        /// Constructor with optional alert strategy (Dependency Injection)
        /// </summary>
        public EyeMonitoringService(AlertStrategyBase? alertStrategy = null)
        {
            // Use provided strategy or create default (Polymorphism)
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
        /// <param name="enabled">Enable or disable debug mode</param>
        /// <param name="debugImageFileName">Debug image file name (Closed.jpg or Open.jpg)</param>
        public void SetDebugMode(bool enabled, string debugImageFileName)
        {
            _isDebugMode = enabled;
            _debugImageFileName = debugImageFileName;

            if (_isDebugMode)
            {
                AddLog(LogLevel.Info, $"?? Debug mode enabled: {debugImageFileName}");
            }
            else
            {
                AddLog(LogLevel.Info, "?? Debug mode disabled");
            }
        }

        /// <summary>
        /// Start monitoring
        /// </summary>
        public void StartMonitoring()
        {
            _checkTimer = new System.Timers.Timer(CHECK_INTERVAL_MS);
            _checkTimer.Elapsed += async (s, e) => await CheckEyeStateAsync();
            _checkTimer.Start();

            AddLog(LogLevel.Success, "? Monitoring started");
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _checkTimer = null;

            AddLog(LogLevel.Info, "Monitoring stopped");
        }

        /// <summary>
        /// Toggle pause
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;
            AddLog(LogLevel.Info, _isPaused ? "? Monitoring paused" : "? Monitoring resumed");
        }

        /// <summary>
        /// Check eye state from image
        /// </summary>
        private async Task CheckEyeStateAsync()
        {
            if (_isPaused) return;

            try
            {
                // Select image file based on debug mode
                string imageFileName = _isDebugMode ? _debugImageFileName : "RealtimePic.jpg";
                string imagePath = ImageHelper.GetImagePath(imageFileName);

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
                bool eyesOpen = ParseEyeState(jsonResponse);

                ProcessEyeState(eyesOpen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Monitoring error: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse Roboflow response
        /// </summary>
        private bool ParseEyeState(string jsonResponse)
        {
            try
            {
                if (jsonResponse.Contains("\"class\":\"Open\"") ||
                    jsonResponse.Contains("\"class\":\"open\""))
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return true; // Default to eyes open on error
            }
        }

        /// <summary>
        /// Process eye state and trigger alerts
        /// Fixed: Continuously plays alert sound while eyes are closed beyond threshold
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
                        AddLog(LogLevel.Warning, $"? Eyes closed for {closedDuration:F1}s");
                    }

                    // Alert threshold - INTERVAL-BASED ALERT (every 2 seconds)
                    if (closedDuration >= AlertThresholdSeconds)
                    {
                        // Check if enough time has passed since last alert
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
                    AddLog(LogLevel.Success, $"???? Eyes opened (was closed for {closedDuration:F1}s)");
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
}