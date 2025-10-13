#if WINDOWS
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices.WindowsRuntime;
#endif

using System.Diagnostics;
using VisionFocus.Utilities;

namespace VisionFocus.Services
{
    /// <summary>
    /// Service responsible for camera capture and frame management
    /// </summary>
    public class CameraService : ICameraService
    {
#if WINDOWS
        private MediaCapture? _mediaCapture;
        private DispatcherTimer? _previewTimer;
        private DispatcherTimer? _autoSaveTimer;
        private volatile bool _isCapturing = false;
        private readonly SemaphoreSlim _captureGate = new(1, 1);
        private byte[]? _latestJpegBytes;

        // Constants
        private const int PreviewIntervalMs = 100;  // 10fps
        private const int AutoSaveIntervalMs = 500; // 0.5 seconds
        private const string RealtimePicFilename = "RealtimePic.jpg";
        private const uint TargetWidth = 1280;
        private const uint TargetHeight = 720;

        // Events
        public event EventHandler<byte[]>? FrameCaptured;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? CameraStarted;
        public event EventHandler? CameraStopped;
#endif

        /// <summary>
        /// Initialize and start camera
        /// </summary>
        public async Task<bool> StartCameraAsync()
        {
#if WINDOWS
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

                // Optimize camera settings
                await ConfigureCameraAsync(_mediaCapture);

                // Start preview timer (10fps)
                _previewTimer = new DispatcherTimer();
                _previewTimer.Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs);
                _previewTimer.Tick += async (_, __) => await CaptureFrameAsync();
                _previewTimer.Start();

                // Start auto-save timer (0.5 second interval)
                _autoSaveTimer = new DispatcherTimer();
                _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(AutoSaveIntervalMs);
                _autoSaveTimer.Tick += async (_, __) => await AutoSaveImageAsync();
                _autoSaveTimer.Start();

                _isCapturing = true;

                CameraStarted?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine("? Camera started");

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex.Message);
                Debug.WriteLine($"Camera initialization error: {ex}");
                return false;
            }
#else
            return await Task.FromResult(false);
#endif
        }

        /// <summary>
        /// Stop camera
        /// </summary>
        public void StopCamera()
        {
#if WINDOWS
            try
            {
                _isCapturing = false;

                // Stop timers
                _previewTimer?.Stop();
                _previewTimer = null;

                _autoSaveTimer?.Stop();
                _autoSaveTimer = null;

                // Wait for last frame capture to complete
                SpinWait.SpinUntil(() => _captureGate.CurrentCount == 1, millisecondsTimeout: 500);

                // Dispose MediaCapture
                _mediaCapture?.Dispose();
                _mediaCapture = null;

                _latestJpegBytes = null;

                CameraStopped?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine("Camera stopped");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error during stop: {ex.Message}");
            }
#endif
        }

#if WINDOWS
        /// <summary>
        /// Optimize camera settings
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
                    Debug.WriteLine($"?? Preview: {target.Width}x{target.Height}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Resolution setting error: {ex.Message}");
            }

            // Enable auto exposure
            try
            {
                if (vdc.ExposureControl.Supported)
                {
                    await vdc.ExposureControl.SetAutoAsync(true);
                    Debug.WriteLine("? Auto exposure enabled");
                }
            }
            catch { }

            // Set ISO to auto
            try
            {
                if (vdc.IsoSpeedControl.Supported)
                {
                    await vdc.IsoSpeedControl.SetAutoAsync();
                    Debug.WriteLine("? Auto ISO enabled");
                }
            }
            catch { }

            // Set exposure compensation (adjust brightness)
            try
            {
                if (vdc.ExposureCompensationControl.Supported)
                {
                    var min = vdc.ExposureCompensationControl.Min;
                    var max = vdc.ExposureCompensationControl.Max;
                    float targetComp = 0.5f;
                    var clamped = Math.Max(min, Math.Min(max, targetComp));
                    await vdc.ExposureCompensationControl.SetValueAsync(clamped);
                    Debug.WriteLine($"? Exposure compensation: {clamped}");
                }
            }
            catch { }

            // Set white balance to auto
            try
            {
                if (vdc.WhiteBalanceControl.Supported)
                {
                    await vdc.WhiteBalanceControl.SetPresetAsync(ColorTemperaturePreset.Auto);
                    Debug.WriteLine("? Auto white balance enabled");
                }
            }
            catch { }

            // Enable continuous auto focus
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
                    Debug.WriteLine("? Continuous auto focus enabled");
                }
            }
            catch { }

            // Enable backlight compensation
            try
            {
                if (vdc.BacklightCompensation?.Capabilities.Supported == true)
                {
                    vdc.BacklightCompensation.TrySetValue(1);
                    Debug.WriteLine("? Backlight compensation enabled");
                }
            }
            catch { }

            // Adjust brightness and contrast
            try
            {
                if (vdc.Brightness?.Capabilities.Supported == true)
                {
                    var range = vdc.Brightness.Capabilities;
                    double target = (range.Max + range.Min) / 2.0 + range.Step;
                    vdc.Brightness.TrySetValue(target);
                }

                if (vdc.Contrast?.Capabilities.Supported == true)
                {
                    var range = vdc.Contrast.Capabilities;
                    double target = (range.Max + range.Min) / 2.0 + range.Step;
                    vdc.Contrast.TrySetValue(target);
                }
            }
            catch { }
        }

        /// <summary>
        /// Capture frame and fire event
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

                // Fire event
                FrameCaptured?.Invoke(this, bytes);
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
        /// Auto-save latest image
        /// </summary>
        private async Task AutoSaveImageAsync()
        {
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

        public void Dispose()
        {
            StopCamera();
        }
    }
}