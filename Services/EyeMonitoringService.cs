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
        private bool _eyesClosed = false;
        private bool _warningTriggered = false;
        private bool _alertTriggered = false;
        private bool _isPaused = false;

        // Polymorphic alert strategy (Demonstrates Polymorphism)
        private AlertStrategyBase? _alertStrategy;

        // Settings
        public double AlertThresholdSeconds { get; set; } = 5.0;
        public double WarningThresholdSeconds { get; set; } = 3.0;
        private const int CHECK_INTERVAL_MS = 500;

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
            // Use provided strategy or create default (Polymorphism)
            _alertStrategy = alertStrategy ?? AlertSoundService.CreateAlertStrategy(AlertSoundType.Beep);
        }

        /// <summary>
        /// Set alert strategy
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
        /// Start monitoring
        /// </summary>
        public void StartMonitoring()
        {
            _checkTimer = new System.Timers.Timer(CHECK_INTERVAL_MS);
            _checkTimer.Elapsed += async (s, e) => await CheckEyeStateAsync();
            _checkTimer.Start();

            AddLog(LogLevel.Success, "??? Monitoring started");
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
            AddLog(LogLevel.Info, _isPaused ? "?? Monitoring paused" : "?? Monitoring resumed");
        }

        /// <summary>
        /// Check eye state from image
        /// </summary>
        private async Task CheckEyeStateAsync()
        {
            if (_isPaused) return;

            try
            {
                string imagePath = ImageHelper.GetImagePath("RealtimePic.jpg");
                if (!File.Exists(imagePath)) return;

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
                    AddLog(LogLevel.Warning, "???? Eyes closed detected");
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

                    // Alert threshold - USES POLYMORPHISM
                    if (!_alertTriggered && closedDuration >= AlertThresholdSeconds)
                    {
                        _alertTriggered = true;
                        AlertTriggered?.Invoke(this, EventArgs.Empty);

                        // Polymorphic call - runtime determines which Play() method to execute
                        _alertStrategy?.Play();

                        AddLog(LogLevel.Alert, $"?? ALERT! Eyes closed for {closedDuration:F1}s");
                    }
                }
            }
            else
            {
                if (_eyesClosed)
                {
                    // Eyes opened again
                    double closedDuration = (DateTime.Now - _eyesClosedStartTime).TotalSeconds;
                    _eyesClosed = false;

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