// CameraPage.xaml.cs - Integrated version with monitoring functionality

#if WINDOWS
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Microsoft.UI.Xaml; // DispatcherTimer
using System.Runtime.InteropServices.WindowsRuntime;
using MauiApp = Microsoft.Maui.Controls.Application;
using MauiThickness = Microsoft.Maui.Thickness;
#endif

using System.Diagnostics;
using VisionFocus.Services;
using VisionFocus.Utilities;

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
#if WINDOWS
        private MediaCapture? _mediaCapture;
        private DispatcherTimer? _previewTimer;
        private DispatcherTimer? _autoSaveTimer;
        private DispatcherTimer? _countdownTimer;
        private volatile bool _isCapturing = false;
        private volatile bool _isPaused = false;
        private readonly SemaphoreSlim _captureGate = new(1, 1);
        private byte[]? _latestJpegBytes;

        // Monitoring variables
        private bool _isMonitoring = false;
        private CancellationTokenSource? _monitoringCancellationTokenSource;
        private DateTime? _eyesClosedStartTime = null;
        private double _consecutiveClosedDuration = 0;
        private const double ALERT_THRESHOLD = 5.0; // Alert after 5 seconds
        private const double WARNING_THRESHOLD = 3.0; // Warning after 3 seconds
        private const int MONITORING_INTERVAL_MS = 1000; // Check every 1 second
        private const int MONITORING_START_DELAY_MS = 1000; // Start monitoring after 1 second

        // Timer settings
        private int _remainingSeconds = 25 * 60; // 25 minutes in seconds
        private const int SessionDurationMinutes = 25;

        // Timer intervals
        private const int PreviewIntervalMs = 100;  // Preview update: 100ms ≈ 10fps
        private const int AutoSaveIntervalMs = 500; // Auto-save: 500ms = 0.5 seconds

        // Fixed filename for auto-save
        private const string RealtimePicFilename = "RealtimePic.jpg";

        // Target resolution
        private const uint TargetWidth = 1280;
        private const uint TargetHeight = 720;
#endif

        public CameraPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
        }

#if WINDOWS
        private async Task InitializeCameraAsync()
        {
            try
            {
                // Initialize MediaCapture
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                };
                await _mediaCapture.InitializeAsync(settings);

                await ConfigureCameraAsync(_mediaCapture);

                // Start preview timer (10fps for display)
                _previewTimer = new DispatcherTimer();
                _previewTimer.Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs);
                _previewTimer.Tick += async (_, __) => await CaptureFrameAsync();
                _previewTimer.Start();

                // Start auto-save timer (0.5 seconds interval)
                _autoSaveTimer = new DispatcherTimer();
                _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(AutoSaveIntervalMs);
                _autoSaveTimer.Tick += async (_, __) => await AutoSaveImageAsync();
                _autoSaveTimer.Start();

                // Start countdown timer (1 second interval)
                _remainingSeconds = SessionDurationMinutes * 60;
                UpdateTimerDisplay();
                _countdownTimer = new DispatcherTimer();
                _countdownTimer.Interval = TimeSpan.FromSeconds(1);
                _countdownTimer.Tick += OnCountdownTick;
                _countdownTimer.Start();

                _isCapturing = true;
                _isPaused = false;

                // Update UI
                StartButton.IsVisible = false;
                ControlButtons.IsVisible = true;

                AddLogEntry("✅ Camera started", LogLevel.Success);
                Debug.WriteLine("✅ Camera started with auto-save (every 0.5s) and timer");

                // Start monitoring after 1 second delay
                await Task.Delay(MONITORING_START_DELAY_MS);
                StartMonitoring();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Camera initialization failed: {ex.Message}", "OK");
                AddLogEntry($"❌ Camera initialization failed: {ex.Message}", LogLevel.Error);
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Start monitoring functionality
        /// </summary>
        private void StartMonitoring()
        {
            try
            {
                _isMonitoring = true;
                _monitoringCancellationTokenSource = new CancellationTokenSource();

                // Initialize tracking variables
                _eyesClosedStartTime = null;
                _consecutiveClosedDuration = 0;

                AddLogEntry("🟢 Monitoring started", LogLevel.Success);

                // Start monitoring loop
                _ = Task.Run(() => MonitoringLoopAsync(_monitoringCancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                AddLogEntry($"❌ Monitoring start error: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Stop monitoring functionality
        /// </summary>
        private void StopMonitoring()
        {
            if (_isMonitoring)
            {
                _isMonitoring = false;
                _monitoringCancellationTokenSource?.Cancel();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddLogEntry("⏹️ Monitoring stopped", LogLevel.Info);
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
                    // Skip monitoring when paused
                    if (_isPaused)
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    // Get latest image
                    string imagePath = ImageHelper.GetImagePath(RealtimePicFilename);
                    if (!File.Exists(imagePath))
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    // Record API call start time
                    var startTime = DateTime.Now;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"📤 Analyzing image...", LogLevel.Info);
                    });

                    // Call Roboflow API
                    string jsonResponse = await RoboflowService.InferImageAsync(imagePath);

                    // Record API call end time
                    var endTime = DateTime.Now;
                    var responseTime = (endTime - startTime).TotalMilliseconds;

                    // Parse response
                    string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                    // Determine eye state
                    EyeState eyeState = DetermineEyeState(parsedResult);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"📥 Response received ({responseTime:F0}ms)", LogLevel.Info);
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
                            AddLogEntry($"🚨 [ALERT] Eyes closed for {_consecutiveClosedDuration:F1}s!", LogLevel.Alert);
                        }
                        else if (_consecutiveClosedDuration >= WARNING_THRESHOLD)
                        {
                            // Closed for 3+ seconds - WARNING
                            AddLogEntry($"⚠️ [WARNING] Eyes closed for {_consecutiveClosedDuration:F1}s", LogLevel.Warning);
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
                    else
                    {
                        AddLogEntry("✅ Eyes are open", LogLevel.Success);
                    }
                    break;

                case EyeState.Unknown:
                    // Could not detect
                    AddLogEntry("❓ Could not detect eye state", LogLevel.Warning);
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

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var label = new Label
                {
                    Text = logText,
                    FontSize = 12,
#if WINDOWS
                    Padding = new MauiThickness(5, 2),
#else
                    Padding = new Thickness(5, 2),
#endif
                    TextColor = GetLogColor(level)
                };

                LogContainer.Children.Add(label);

                // Auto-scroll to bottom
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(50);
                    await LogScrollView.ScrollToAsync(label, ScrollToPosition.End, true);
                });

                // If log exceeds 100 entries, remove oldest
                if (LogContainer.Children.Count > 100)
                {
                    LogContainer.Children.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// Get color based on log level
        /// </summary>
        private Color GetLogColor(LogLevel level)
        {
#if WINDOWS
            var isDark = MauiApp.Current?.RequestedTheme == AppTheme.Dark;
#else
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
#endif
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

        /// <summary>
        /// Countdown timer tick event
        /// </summary>
        private void OnCountdownTick(object? sender, object e)
        {
            if (_isPaused) return;

            _remainingSeconds--;
            UpdateTimerDisplay();

            if (_remainingSeconds <= 0)
            {
                // Session time is up
                StopCamera();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Session Complete", "25 minutes session has ended", "OK");
                });
            }
        }

        /// <summary>
        /// Update timer display
        /// </summary>
        private void UpdateTimerDisplay()
        {
            int minutes = _remainingSeconds / 60;
            int seconds = _remainingSeconds % 60;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TimerLabel.Text = $"{minutes:D2}:{seconds:D2}";
            });
        }

        /// <summary>
        /// Configure camera settings for optimal brightness and quality
        /// </summary>
        private async Task ConfigureCameraAsync(MediaCapture mediaCapture)
        {
            var vdc = mediaCapture.VideoDeviceController;

            // Resolution setting (prioritize 1280x720)
            try
            {
                var all = vdc.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview)
                             .OfType<VideoEncodingProperties>()
                             .ToList();

                var target = all.FirstOrDefault(p => p.Width == TargetWidth && p.Height == TargetHeight)
                          ?? all.OrderBy(p => (long)p.Width * p.Height).FirstOrDefault();

                if (target != null)
                {
                    await vdc.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, target);
                    Debug.WriteLine($"📷 Preview Properties: {target.Subtype} {target.Width}x{target.Height} @{target.FrameRate.Numerator}/{target.FrameRate.Denominator}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set resolution: {ex.Message}");
            }

            // Power line frequency (flicker reduction)
            try
            {
                vdc.TrySetPowerlineFrequency(PowerlineFrequency.FiftyHertz);
            }
            catch { /* ignore */ }

            // Enable auto exposure
            try
            {
                if (vdc.ExposureControl.Supported)
                {
                    await vdc.ExposureControl.SetAutoAsync(true);
                    Debug.WriteLine("✅ Auto exposure enabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto exposure setting error: {ex.Message}");
            }

            // Set ISO speed to auto
            try
            {
                if (vdc.IsoSpeedControl.Supported)
                {
                    await vdc.IsoSpeedControl.SetAutoAsync();
                    Debug.WriteLine("✅ Auto ISO enabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ISO setting error: {ex.Message}");
            }

            // Set exposure compensation to brighten image
            try
            {
                if (vdc.ExposureCompensationControl.Supported)
                {
                    var min = vdc.ExposureCompensationControl.Min;
                    var max = vdc.ExposureCompensationControl.Max;
                    var step = vdc.ExposureCompensationControl.Step;

                    float targetComp = 0.5f; // 0 = standard, +0.5~+1.0 = brighter
                    var clamped = Math.Max(min, Math.Min(max, targetComp));
                    await vdc.ExposureCompensationControl.SetValueAsync(clamped);

                    Debug.WriteLine($"✅ Exposure compensation: {clamped} (range {min}..{max}, step {step})");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exposure compensation error: {ex.Message}");
            }

            // Set white balance to auto
            try
            {
                if (vdc.WhiteBalanceControl.Supported)
                {
                    await vdc.WhiteBalanceControl.SetPresetAsync(ColorTemperaturePreset.Auto);
                    Debug.WriteLine("✅ Auto white balance enabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"White balance setting error: {ex.Message}");
            }

            // Set focus to continuous auto focus
            try
            {
                if (vdc.FocusControl.Supported)
                {
                    var focus = vdc.FocusControl;
                    focus.Configure(new FocusSettings
                    {
                        Mode = FocusMode.Continuous,
                        AutoFocusRange = AutoFocusRange.FullRange,
                        DisableDriverFallback = false
                    });
                    await focus.FocusAsync();
                    Debug.WriteLine("✅ Continuous auto focus enabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Focus setting error: {ex.Message}");
            }

            // Enable backlight compensation
            try
            {
                if (vdc.BacklightCompensation != null && vdc.BacklightCompensation.Capabilities.Supported)
                {
                    vdc.BacklightCompensation.TrySetValue(1); // Enable
                    Debug.WriteLine("✅ Backlight compensation enabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Backlight compensation error: {ex.Message}");
            }

            // Turn off HDR and noise reduction (prioritize brightness)
            try
            {
                if (vdc.HdrVideoControl.Supported)
                    vdc.HdrVideoControl.Mode = HdrVideoMode.Off;

                if (vdc.VideoTemporalDenoisingControl.Supported)
                    vdc.VideoTemporalDenoisingControl.Mode = VideoTemporalDenoisingMode.Off;
            }
            catch { /* ignore */ }

            // Adjust brightness and contrast
            try
            {
                if (vdc.Brightness != null && vdc.Brightness.Capabilities.Supported)
                {
                    var brightnessRange = vdc.Brightness.Capabilities;
                    double targetBrightness = (brightnessRange.Max + brightnessRange.Min) / 2.0 + brightnessRange.Step;
                    vdc.Brightness.TrySetValue(targetBrightness);
                    Debug.WriteLine($"✅ Brightness adjusted: {targetBrightness}");
                }

                if (vdc.Contrast != null && vdc.Contrast.Capabilities.Supported)
                {
                    var contrastRange = vdc.Contrast.Capabilities;
                    double targetContrast = (contrastRange.Max + contrastRange.Min) / 2.0 + contrastRange.Step;
                    vdc.Contrast.TrySetValue(targetContrast);
                    Debug.WriteLine($"✅ Contrast adjusted: {targetContrast}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Brightness/Contrast adjustment error: {ex.Message}");
            }
        }

        /// <summary>
        /// Capture one JPEG frame and update preview display
        /// </summary>
        private async Task CaptureFrameAsync()
        {
            if (_mediaCapture == null || !_isCapturing) return;

            if (!await _captureGate.WaitAsync(0))
                return;

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

                // Convert to byte array
                stream.Seek(0);
                using var netStream = stream.AsStreamForRead();
                using var ms = new MemoryStream();
                await netStream.CopyToAsync(ms);
                var bytes = ms.ToArray();

                _latestJpegBytes = bytes;

                // Update preview display
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Capture error: {ex.Message}");
            }
            finally
            {
                _captureGate.Release();
            }
        }

        /// <summary>
        /// Auto-save the latest captured image as RealtimePic.jpg
        /// </summary>
        private async Task AutoSaveImageAsync()
        {
            if (_isPaused) return;

            try
            {
                if (_latestJpegBytes == null || _latestJpegBytes.Length == 0)
                    return;

                string filePath = ImageHelper.GetImagePath(RealtimePicFilename);
                await File.WriteAllBytesAsync(filePath, _latestJpegBytes);

                Debug.WriteLine($"Auto-saved: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto-save error: {ex.Message}");
            }
        }
#endif

        private async void OnBackClicked(object sender, EventArgs e)
        {
            StopCamera();
            await Shell.Current.GoToAsync("..");
        }

        private async void OnStartClicked(object sender, EventArgs e)
        {
#if WINDOWS
            if (_mediaCapture == null)
                await InitializeCameraAsync();
#endif
        }

        private void OnPauseClicked(object sender, EventArgs e)
        {
#if WINDOWS
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                PauseButton.Text = "▶";
                PauseButton.BackgroundColor = Color.FromArgb("#4CAF50");
                AddLogEntry("⏸️ Session paused", LogLevel.Info);
                Debug.WriteLine("Session paused");
            }
            else
            {
                PauseButton.Text = "⏸";
                PauseButton.BackgroundColor = Color.FromArgb("#FF9800");
                AddLogEntry("▶️ Session resumed", LogLevel.Info);
                Debug.WriteLine("Session resumed");
            }
#endif
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            StopCamera();
        }

        private void StopCamera()
        {
#if WINDOWS
            try
            {
                _isCapturing = false;
                _isPaused = false;

                // Stop monitoring
                StopMonitoring();

                // Stop all timers
                _previewTimer?.Stop();
                _previewTimer = null;

                _autoSaveTimer?.Stop();
                _autoSaveTimer = null;

                _countdownTimer?.Stop();
                _countdownTimer = null;

                // Wait for last frame to complete (max 500ms)
                SpinWait.SpinUntil(() =>
                    _captureGate.CurrentCount == 1, millisecondsTimeout: 500);

                // Dispose MediaCapture
                _mediaCapture?.Dispose();
                _mediaCapture = null;

                _latestJpegBytes = null;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsVisible = true;
                    ControlButtons.IsVisible = false;

                    // Reset timer display
                    _remainingSeconds = SessionDurationMinutes * 60;
                    UpdateTimerDisplay();

                    // Reset pause button
                    PauseButton.Text = "⏸";
                    PauseButton.BackgroundColor = Color.FromArgb("#FF9800");

                    AddLogEntry("⏹️ Camera stopped", LogLevel.Info);
                });

                Debug.WriteLine("Camera stopped");
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Error", $"Error during stop: {ex.Message}", "OK");
                });
            }
#else
            // No action needed for other platforms
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopCamera();
        }

        /// <summary>
        /// Eye state enumeration
        /// </summary>
        private enum EyeState
        {
            Open,      // Eyes are open
            Closed,    // Eyes are closed
            Unknown    // Cannot determine
        }

        /// <summary>
        /// Log level for color coding
        /// </summary>
        private enum LogLevel
        {
            Success,   // Green
            Info,      // Default color
            Warning,   // Orange
            Error,     // Red
            Alert      // Red (critical)
        }
    }
}