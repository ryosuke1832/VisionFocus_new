// CameraPage.xaml.cs - Simplified version with timer

#if WINDOWS
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Microsoft.UI.Xaml; // DispatcherTimer
using System.Runtime.InteropServices.WindowsRuntime;
#endif

using System.Diagnostics;

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

                Debug.WriteLine("✅ Camera started with auto-save (every 0.5s) and timer");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Camera initialization failed: {ex.Message}", "OK");
                Debug.WriteLine(ex);
            }
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
                Debug.WriteLine("Session paused");
            }
            else
            {
                PauseButton.Text = "⏸";
                PauseButton.BackgroundColor = Color.FromArgb("#FF9800");
                Debug.WriteLine("Session resumed");
            }
#endif
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            StopCamera();
        }

        private async void OnJudgeClicked(object sender, EventArgs e)
        {
            try
            {
                // Judge the RealtimePic.jpg file
                string realtimePicPath = ImageHelper.GetImagePath(RealtimePicFilename);

                if (!File.Exists(realtimePicPath))
                {
                    await DisplayAlert("Error", $"'{RealtimePicFilename}' not found. Please start the camera first.", "OK");
                    return;
                }

                JudgeButton.IsEnabled = false;
                JudgeButton.Text = "⏳ Processing...";
                ResultContainer.IsVisible = true;
                ResultLabel.Text = "Sending image to Roboflow API...\nPlease wait...";

                string jsonResponse = await RoboflowService.InferImageAsync(realtimePicPath);
                string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                ResultLabel.Text = $"Image: {RealtimePicFilename}\n\n{parsedResult}";
                Debug.WriteLine($"API Response: {jsonResponse}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to process image: {ex.Message}", "OK");
                ResultLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                JudgeButton.IsEnabled = true;
                JudgeButton.Text = "🔍 Judge Latest Image";
            }
        }

        private void StopCamera()
        {
#if WINDOWS
            try
            {
                _isCapturing = false;
                _isPaused = false;

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
    }
}