// CameraPage.xaml.cs - Windows optimized version with no redundant re-encoding

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
        private DispatcherTimer? _timer;
        private volatile bool _isCapturing = false;
        private readonly SemaphoreSlim _captureGate = new(1, 1);
        private byte[]? _latestJpegBytes;

        // Exposure compensation value (for brightness adjustment)
        private float _expComp = 0.5f;

        // Timer frame interval (ms): 100ms ≈ 10fps
        private const int PreviewIntervalMs = 100;

        // Target resolution (will select closest if exact match not available)
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
#if WINDOWS
            if (_mediaCapture == null)
                await InitializeCameraAsync();
#endif
        }

#if WINDOWS
        private async Task InitializeCameraAsync()
        {
            try
            {
                StatusLabel.IsVisible = true;
                StatusLabel.Text = "Initializing camera...";

                // Initialize MediaCapture
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                };
                await _mediaCapture.InitializeAsync(settings);

                await ConfigureCameraAsync(_mediaCapture);

                // Start timer preview (10fps)
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs);
                _timer.Tick += async (_, __) => await CaptureFrameAsync();
                _timer.Start();

                _isCapturing = true;

                // Update UI
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                CaptureButton.IsEnabled = true;
                StatusLabel.IsVisible = false;

                Debug.WriteLine("✅ Live preview started (DispatcherTimer 10fps)");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Camera initialization failed: {ex.Message}", "OK");
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.IsVisible = true;
                Debug.WriteLine(ex);
            }
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

            // [IMPORTANT] Set exposure compensation to 0 or +0.5 (to brighten image)
            try
            {
                if (vdc.ExposureCompensationControl.Supported)
                {
                    var min = vdc.ExposureCompensationControl.Min;
                    var max = vdc.ExposureCompensationControl.Max;
                    var step = vdc.ExposureCompensationControl.Step;

                    // Set to +0.5 to brighten (clamp within range)
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

            // Enable backlight compensation (for backlighting situations)
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

            // Adjust brightness and contrast (if device supports)
            try
            {
                if (vdc.Brightness != null && vdc.Brightness.Capabilities.Supported)
                {
                    var brightnessRange = vdc.Brightness.Capabilities;
                    // Set slightly brighter than center
                    double targetBrightness = (brightnessRange.Max + brightnessRange.Min) / 2.0 + brightnessRange.Step;
                    vdc.Brightness.TrySetValue(targetBrightness);
                    Debug.WriteLine($"✅ Brightness adjusted: {targetBrightness}");
                }

                if (vdc.Contrast != null && vdc.Contrast.Capabilities.Supported)
                {
                    var contrastRange = vdc.Contrast.Capabilities;
                    // Set contrast slightly higher than standard
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
        /// Capture one JPEG frame via timer and display in Image control.
        /// Note: Ideally CaptureElement/FrameReader would be used for preview,
        ///       but to maintain existing XAML (Image), we use JPEG direct output + FromStream.
        ///       No decode->re-encode is performed here.
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

                // Convert directly to byte array (no decode/re-encode)
                stream.Seek(0);
                using var netStream = stream.AsStreamForRead();
                using var ms = new MemoryStream();
                await netStream.CopyToAsync(ms);
                var bytes = ms.ToArray();

                _latestJpegBytes = bytes;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Creating short-lived Stream every time is hard on GC, but maintains minimal changes
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

        private void OnStopClicked(object sender, EventArgs e)
        {
            StopCamera();
        }

        private async void OnCaptureClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                if (_latestJpegBytes == null || _latestJpegBytes.Length == 0)
                {
                    await DisplayAlert("Error", "No frame available to save", "OK");
                    return;
                }

                string fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filePath = ImageHelper.GetImagePath(fileName);
                await File.WriteAllBytesAsync(filePath, _latestJpegBytes);

                await DisplayAlert("Success", $"Image saved successfully!\n{fileName}", "OK");
                Debug.WriteLine($"Image saved: {filePath}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
            }
#else
            await DisplayAlert("Not Supported", "This feature only works on Windows.", "OK");
#endif
        }

        private async void OnOpenFolderClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                string folderPath = ImageHelper.ImagesFolderPath;
                if (Directory.Exists(folderPath))
                {
                    // Open folder in Explorer
                    Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    await DisplayAlert("Error", "Folder does not exist", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to open folder: {ex.Message}", "OK");
            }
#else
            await DisplayAlert("Not Supported", "This feature only works on Windows.", "OK");
#endif
        }

        private async void OnJudgeClicked(object sender, EventArgs e)
        {
            try
            {
                var imagePaths = ImageHelper.GetAllImagePaths();
                if (imagePaths.Count == 0)
                {
                    await DisplayAlert("Error", "No images found. Please capture an image first.", "OK");
                    return;
                }

                string latestImagePath = imagePaths[0];
                string fileName = Path.GetFileName(latestImagePath);

                JudgeButton.IsEnabled = false;
                JudgeButton.Text = "⏳ Processing...";
                ResultContainer.IsVisible = true;
                ResultLabel.Text = "Sending image to Roboflow API...\nPlease wait...";

                string jsonResponse = await RoboflowService.InferImageAsync(latestImagePath);
                string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                ResultLabel.Text = $"Image: {fileName}\n\n{parsedResult}";
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

                // Stop timer
                _timer?.Stop();
                _timer = null;

                // Wait for last frame to complete (max 500ms)
                SpinWait.SpinUntil(() =>
                    _captureGate.CurrentCount == 1, millisecondsTimeout: 500);

                // Dispose MediaCapture
                _mediaCapture?.Dispose();
                _mediaCapture = null;

                _latestJpegBytes = null;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    CaptureButton.IsEnabled = false;
                    StatusLabel.Text = "Stopped";
                    StatusLabel.IsVisible = true;

                    // Uncomment to clear screen to black
                    // CameraPreview.Source = null;
                });
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