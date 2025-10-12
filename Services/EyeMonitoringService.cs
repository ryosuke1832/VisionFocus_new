using System.Diagnostics;
using VisionFocus.Utilities;

namespace VisionFocus.Services
{
    /// <summary>
    /// Eye state enumeration
    /// </summary>
    public enum EyeState
    {
        Open,      // Eyes are open
        Closed,    // Eyes are closed
        Unknown    // Cannot determine
    }

    /// <summary>
    /// Log level enumeration
    /// </summary>
    public enum LogLevel
    {
        Success,   // Success (green)
        Info,      // Information (default)
        Warning,   // Warning (orange)
        Error,     // Error (red)
        Alert      // Alert (red, critical)
    }

    /// <summary>
    /// Log entry class
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
        public LogLevel Level { get; set; }

        public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {Message}";
    }

    /// <summary>
    /// Service responsible for eye monitoring and alert management
    /// </summary>
    public class EyeMonitoringService : IDisposable
    {
        private const double DEFAULT_ALERT_THRESHOLD = 5.0;  // Alert after 5 seconds
        private const double DEFAULT_WARNING_THRESHOLD = 3.0; // Warning after 3 seconds
        private const int MONITORING_INTERVAL_MS = 1000;      // Check every 1 second
        private const string DEFAULT_IMAGE_FILENAME = "RealtimePic.jpg";

        private bool _isMonitoring = false;
        private bool _isPaused = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private DateTime? _eyesClosedStartTime = null;
        private double _consecutiveClosedDuration = 0;

        // Settings
        public double AlertThresholdSeconds { get; set; } = DEFAULT_ALERT_THRESHOLD;
        public double WarningThresholdSeconds { get; set; } = DEFAULT_WARNING_THRESHOLD;

        /// <summary>
        /// Image filename to use for API calls (default: RealtimePic.jpg)
        /// 実験用に別のファイル名を指定する場合はこのプロパティを変更してください
        /// </summary>
        public string ImageFileName { get; set; } = DEFAULT_IMAGE_FILENAME;

        // Events
        public event EventHandler<LogEntry>? LogEntryAdded;
        public event EventHandler<EyeState>? EyeStateChanged;
        public event EventHandler? AlertTriggered;
        public event EventHandler? WarningTriggered;

        /// <summary>
        /// Start monitoring
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            try
            {
                _isMonitoring = true;
                _isPaused = false;
                _cancellationTokenSource = new CancellationTokenSource();

                // Initialize tracking variables
                _eyesClosedStartTime = null;
                _consecutiveClosedDuration = 0;

                AddLog($"?? Monitoring started (using: {ImageFileName})", LogLevel.Success);

                // Start monitoring loop
                _ = Task.Run(() => MonitoringLoopAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                AddLog($"? Monitoring start error: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _cancellationTokenSource?.Cancel();

            AddLog("?? Monitoring stopped", LogLevel.Info);
        }

        /// <summary>
        /// Pause/Resume monitoring
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                AddLog("?? Monitoring paused", LogLevel.Info);
            }
            else
            {
                AddLog("?? Monitoring resumed", LogLevel.Info);
                // Reset counter during pause
                _eyesClosedStartTime = null;
                _consecutiveClosedDuration = 0;
            }
        }

        /// <summary>
        /// Monitoring loop
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (_isMonitoring && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Skip during pause
                    if (_isPaused)
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    // Get image path using specified filename
                    string imagePath = ImageHelper.GetImagePath(ImageFileName);
                    if (!File.Exists(imagePath))
                    {
                        AddLog($"?? Image not found: {ImageFileName}", LogLevel.Warning);
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    // Record API call start time
                    var startTime = DateTime.Now;

                    AddLog("?? Analyzing image...", LogLevel.Info);

                    // Call Roboflow API
                    string jsonResponse = await RoboflowService.InferImageAsync(imagePath);

                    // Record API call end time
                    var endTime = DateTime.Now;
                    var responseTime = (endTime - startTime).TotalMilliseconds;

                    // Parse response
                    string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                    // Determine eye state
                    EyeState eyeState = DetermineEyeState(parsedResult);

                    AddLog($"? Response received ({responseTime:F0}ms)", LogLevel.Info);
                    ProcessEyeState(eyeState, parsedResult);

                    Debug.WriteLine($"API Response: {jsonResponse}");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AddLog($"? Error: {ex.Message}", LogLevel.Error);
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

            // Fire event
            EyeStateChanged?.Invoke(this, eyeState);

            switch (eyeState)
            {
                case EyeState.Closed:
                    HandleClosedEyes(now);
                    break;

                case EyeState.Open:
                    HandleOpenEyes(now);
                    break;

                case EyeState.Unknown:
                    HandleUnknownState();
                    break;
            }
        }

        /// <summary>
        /// Handle closed eyes case
        /// </summary>
        private void HandleClosedEyes(DateTime now)
        {
            if (_eyesClosedStartTime == null)
            {
                // First detection of closed eyes
                _eyesClosedStartTime = now;
                _consecutiveClosedDuration = 0;
                AddLog("?? Eyes are closed", LogLevel.Warning);
            }
            else
            {
                // Eyes continuously closed
                _consecutiveClosedDuration = (now - _eyesClosedStartTime.Value).TotalSeconds;

                if (_consecutiveClosedDuration >= AlertThresholdSeconds)
                {
                    // Closed for 5+ seconds - ALERT
                    AddLog($"?? [ALERT] Eyes closed for {_consecutiveClosedDuration:F1}s!", LogLevel.Alert);
                    AlertTriggered?.Invoke(this, EventArgs.Empty);

                    // Play sound
                    AlertSoundService.PlaySound(AlertSoundType.Exclamation);
                }
                else if (_consecutiveClosedDuration >= WarningThresholdSeconds)
                {
                    // Closed for 3+ seconds - WARNING
                    AddLog($"?? [WARNING] Eyes closed for {_consecutiveClosedDuration:F1}s", LogLevel.Warning);
                    WarningTriggered?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Handle open eyes case
        /// </summary>
        private void HandleOpenEyes(DateTime now)
        {
            if (_eyesClosedStartTime != null)
            {
                // Eyes opened, reset closed duration
                var closedDuration = (now - _eyesClosedStartTime.Value).TotalSeconds;

                if (closedDuration >= AlertThresholdSeconds)
                {
                    AddLog($"?? Eyes opened (were closed for {closedDuration:F1}s)", LogLevel.Success);
                }
                else if (closedDuration >= WarningThresholdSeconds)
                {
                    AddLog($"?? Eyes opened (were closed for {closedDuration:F1}s)", LogLevel.Info);
                }
                else
                {
                    AddLog("?? Eyes opened", LogLevel.Success);
                }

                _eyesClosedStartTime = null;
                _consecutiveClosedDuration = 0;
            }
            else
            {
                AddLog("?? Eyes are open", LogLevel.Success);
            }
        }

        /// <summary>
        /// Handle unknown state case
        /// </summary>
        private void HandleUnknownState()
        {
            AddLog("? Could not detect eye state", LogLevel.Warning);
            // Reset counter when detection fails
            _eyesClosedStartTime = null;
            _consecutiveClosedDuration = 0;
        }

        /// <summary>
        /// Add log entry
        /// </summary>
        private void AddLog(string message, LogLevel level)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level
            };

            LogEntryAdded?.Invoke(this, entry);
        }

        public void Dispose()
        {
            StopMonitoring();
            _cancellationTokenSource?.Dispose();
        }
    }
}