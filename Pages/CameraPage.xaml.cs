// CameraPage.xaml.cs - Settings対応版

#if WINDOWS
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Microsoft.UI.Xaml;
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

        // Settings
        private SettingsModel? _settings;
        private int _sessionDurationMinutes = 25;
        private string _selectedSubject = "";
        private double _alertThreshold = 5.0;
        private double _warningThreshold = 3.0;

        // Monitoring variables
        private bool _isMonitoring = false;
        private CancellationTokenSource? _monitoringCancellationTokenSource;
        private DateTime? _eyesClosedStartTime = null;
        private double _consecutiveClosedDuration = 0;
        private const int MONITORING_INTERVAL_MS = 1000;
        private const int MONITORING_START_DELAY_MS = 1000;

        // Timer settings
        private int _remainingSeconds = 25 * 60;

        // Timer intervals
        private const int PreviewIntervalMs = 100;
        private const int AutoSaveIntervalMs = 500;

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
            LoadSettings();
        }

        /// <summary>
        /// load settings from file
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                _settings = SettingsService.LoadSettings();

                // load session duration
                _sessionDurationMinutes = _settings.SessionDurationMinutes;
                _remainingSeconds = _sessionDurationMinutes * 60;
                UpdateTimerDisplay();

                // load alert thresholds
                _alertThreshold = _settings.AlertThresholdSeconds;
                _warningThreshold = _settings.WarningThresholdSeconds;

                // load subjects
                SubjectPicker.ItemsSource = _settings.Subjects;
                if (_settings.Subjects.Count > 0)
                {
                    SubjectPicker.SelectedIndex = 0;
                    _selectedSubject = _settings.Subjects[0];
                }

                Debug.WriteLine($"✅ Settings loaded: Duration={_sessionDurationMinutes}min, Subjects={_settings.Subjects.Count}, Alert={_alertThreshold}s, Warning={_warningThreshold}s");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error loading settings: {ex.Message}");
                // use default settings on error
                _sessionDurationMinutes = 25;
                _remainingSeconds = _sessionDurationMinutes * 60;
                _alertThreshold = 5.0;
                _warningThreshold = 3.0;
                UpdateTimerDisplay();
            }
        }

        /// <summary>
        /// event handler for subject picker change
        /// </summary>
        private void OnSubjectChanged(object sender, EventArgs e)
        {
            if (SubjectPicker.SelectedIndex >= 0)
            {
                _selectedSubject = SubjectPicker.Items[SubjectPicker.SelectedIndex];
                Debug.WriteLine($"📚 Subject changed to: {_selectedSubject}");
            }
        }

#if WINDOWS
        private async Task InitializeCameraAsync()
        {
            try
            {
                // MediaCaptureの初期化
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                };
                await _mediaCapture.InitializeAsync(settings);

                await ConfigureCameraAsync(_mediaCapture);

                // start preview
                _previewTimer = new DispatcherTimer();
                _previewTimer.Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs);
                _previewTimer.Tick += async (_, __) => await CaptureFrameAsync();
                _previewTimer.Start();

                // start auto-save timer
                _autoSaveTimer = new DispatcherTimer();
                _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(AutoSaveIntervalMs);
                _autoSaveTimer.Tick += async (_, __) => await AutoSaveImageAsync();
                _autoSaveTimer.Start();

                // start countdown timer
                _remainingSeconds = _sessionDurationMinutes * 60;
                UpdateTimerDisplay();
                _countdownTimer = new DispatcherTimer();
                _countdownTimer.Interval = TimeSpan.FromSeconds(1);
                _countdownTimer.Tick += OnCountdownTick;
                _countdownTimer.Start();

                _isCapturing = true;
                _isPaused = false;

                // update UI
                StartButton.IsVisible = false;
                ControlButtons.IsVisible = true;

                AddLogEntry($"✅ Session started: {_selectedSubject} ({_sessionDurationMinutes}min)", LogLevel.Success);
                Debug.WriteLine($"✅ Camera started for {_selectedSubject}");

                // start monitoring after short delay
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
        /// start monitoring
        /// </summary>
        private void StartMonitoring()
        {
            try
            {
                _isMonitoring = true;
                _monitoringCancellationTokenSource = new CancellationTokenSource();
                _eyesClosedStartTime = null;
                _consecutiveClosedDuration = 0;

                AddLogEntry("🟢 Monitoring started", LogLevel.Success);
                _ = Task.Run(() => MonitoringLoopAsync(_monitoringCancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                AddLogEntry($"❌ Monitoring start error: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// stop monitoring
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
        /// loop for monitoring
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (_isMonitoring && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_isPaused)
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    string imagePath = ImageHelper.GetImagePath(RealtimePicFilename);
                    if (!File.Exists(imagePath))
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    var startTime = DateTime.Now;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"📤 Analyzing image...", LogLevel.Info);
                    });

                    string jsonResponse = await RoboflowService.InferImageAsync(imagePath);
                    var endTime = DateTime.Now;
                    var responseTime = (endTime - startTime).TotalMilliseconds;

                    string parsedResult = RoboflowService.ParseResponse(jsonResponse);
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
        /// judge eye state from parsed result
        /// </summary>
        private EyeState DetermineEyeState(string parsedResult)
        {
            if (string.IsNullOrWhiteSpace(parsedResult))
                return EyeState.Unknown;

            if (parsedResult.Contains("No detection found") || parsedResult.Contains("no detection"))
                return EyeState.Unknown;

            if (parsedResult.Contains("eyes_closed", StringComparison.OrdinalIgnoreCase) ||
                parsedResult.Contains("closed", StringComparison.OrdinalIgnoreCase))
                return EyeState.Closed;

            if (parsedResult.Contains("eyes_open", StringComparison.OrdinalIgnoreCase) ||
                parsedResult.Contains("open", StringComparison.OrdinalIgnoreCase))
                return EyeState.Open;

            return EyeState.Unknown;
        }

        /// <summary>
        /// alert processing based on eye state
        /// </summary>
        private void ProcessEyeState(EyeState eyeState, string detectionDetails)
        {
            var now = DateTime.Now;

            switch (eyeState)
            {
                case EyeState.Closed:
                    if (_eyesClosedStartTime == null)
                    {
                        _eyesClosedStartTime = now;
                        _consecutiveClosedDuration = 0;
                        AddLogEntry("👁️ Eyes closed detected", LogLevel.Warning);
                    }
                    else
                    {
                        _consecutiveClosedDuration = (now - _eyesClosedStartTime.Value).TotalSeconds;

                        if (_consecutiveClosedDuration >= _alertThreshold)
                        {
                            AddLogEntry($"🚨 [ALERT] Eyes closed for {_consecutiveClosedDuration:F1}s!", LogLevel.Alert);

                            if (_settings != null)
                            {
                                var soundType = AlertSoundService.GetSoundTypeFromIndex(_settings.AlertSoundType);
                                AlertSoundService.PlaySound(soundType, _settings.AlertVolume);
                            }
                        }
                        else if (_consecutiveClosedDuration >= _warningThreshold)
                        {
                            AddLogEntry($"⚠️ [WARNING] Eyes closed for {_consecutiveClosedDuration:F1}s", LogLevel.Warning);
                        }
                    }
                    break;

                case EyeState.Open:
                    if (_eyesClosedStartTime != null)
                    {
                        var closedDuration = (now - _eyesClosedStartTime.Value).TotalSeconds;

                        if (closedDuration >= _alertThreshold)
                        {
                            AddLogEntry($"✅ Eyes opened (were closed for {closedDuration:F1}s)", LogLevel.Success);
                        }
                        else if (closedDuration >= _warningThreshold)
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
                    AddLogEntry("❓ Could not detect eye state", LogLevel.Warning);
                    _eyesClosedStartTime = null;
                    _consecutiveClosedDuration = 0;
                    break;
            }
        }

        /// <summary>
        /// add log entry to UI
        /// </summary>
        [Obsolete]
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
                    Padding = new MauiThickness(5, 2),
                    TextColor = GetLogColor(level)
                };

                LogContainer.Children.Add(label);

                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(50);
                    await LogScrollView.ScrollToAsync(label, ScrollToPosition.End, true);
                });

                if (LogContainer.Children.Count > 100)
                {
                    LogContainer.Children.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// get log color based on level
        /// </summary>
        private Color GetLogColor(LogLevel level)
        {
            var isDark = MauiApp.Current?.RequestedTheme == AppTheme.Dark;
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
        /// countdown timer tick
        /// </summary>
        private void OnCountdownTick(object? sender, object e)
        {
            if (_isPaused) return;

            _remainingSeconds--;
            UpdateTimerDisplay();

            if (_remainingSeconds <= 0)
            {
                StopCamera();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Session Complete",
                        $"{_sessionDurationMinutes} minutes session for '{_selectedSubject}' has ended",
                        "OK");
                });
            }
        }

        /// <summary>
        /// update timer display
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
        /// setting camera parameters
        /// </summary>
        private async Task ConfigureCameraAsync(MediaCapture mediaCapture)
        {
            var vdc = mediaCapture.VideoDeviceController;

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
                    Debug.WriteLine($"📷 Preview: {target.Width}x{target.Height}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Resolution setting error: {ex.Message}");
            }

            try { vdc.TrySetPowerlineFrequency(PowerlineFrequency.FiftyHertz); } catch { }
            try { if (vdc.ExposureControl.Supported) await vdc.ExposureControl.SetAutoAsync(true); } catch { }
            try { if (vdc.IsoSpeedControl.Supported) await vdc.IsoSpeedControl.SetAutoAsync(); } catch { }
            try { if (vdc.WhiteBalanceControl.Supported) await vdc.WhiteBalanceControl.SetPresetAsync(ColorTemperaturePreset.Auto); } catch { }
        }

        /// <summary>
        /// frame capture
        /// </summary>
        private async Task CaptureFrameAsync()
        {
            if (_mediaCapture == null || !_isCapturing) return;
            if (!await _captureGate.WaitAsync(0)) return;

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

                stream.Seek(0);
                using var netStream = stream.AsStreamForRead();
                using var ms = new MemoryStream();
                await netStream.CopyToAsync(ms);
                var bytes = ms.ToArray();

                _latestJpegBytes = bytes;

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
        /// auto-save latest image to fixed filename
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
            }
            else
            {
                PauseButton.Text = "⏸";
                PauseButton.BackgroundColor = Color.FromArgb("#FF9800");
                AddLogEntry("▶️ Session resumed", LogLevel.Info);
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

                StopMonitoring();

                _previewTimer?.Stop();
                _previewTimer = null;

                _autoSaveTimer?.Stop();
                _autoSaveTimer = null;

                _countdownTimer?.Stop();
                _countdownTimer = null;

                SpinWait.SpinUntil(() => _captureGate.CurrentCount == 1, millisecondsTimeout: 500);

                _mediaCapture?.Dispose();
                _mediaCapture = null;
                _latestJpegBytes = null;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsVisible = true;
                    ControlButtons.IsVisible = false;

                    _remainingSeconds = _sessionDurationMinutes * 60;
                    UpdateTimerDisplay();

                    PauseButton.Text = "⏸";
                    PauseButton.BackgroundColor = Color.FromArgb("#FF9800");

                    AddLogEntry("⏹️ Camera stopped", LogLevel.Info);
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Error", $"Error during stop: {ex.Message}", "OK");
                });
            }
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopCamera();
        }

        private enum EyeState
        {
            Open,
            Closed,
            Unknown
        }

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