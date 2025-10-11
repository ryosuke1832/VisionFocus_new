#if WINDOWS
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
#endif

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
#if WINDOWS
        private MediaCapture? _mediaCapture;
        private System.Timers.Timer? _timer;
        private bool _isCapturing = false;
#endif

        public CameraPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Back button handler
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            // Stop camera before going back
            StopCamera();
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Start camera button handler
        /// </summary>
        private async void OnStartClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                StatusLabel.Text = "Initializing camera...";

                // Check camera permission
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Error", "Camera permission is required", "OK");
                    StatusLabel.Text = "Permission error";
                    return;
                }

                // Initialize MediaCapture
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video
                };
                await _mediaCapture.InitializeAsync(settings);

                // Capture every second with timer
                _timer = new System.Timers.Timer(1000);
                _timer.Elapsed += async (s, args) => await CaptureFrameAsync();
                _timer.Start();

                _isCapturing = true;
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                StatusLabel.IsVisible = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to start camera: {ex.Message}", "OK");
                StatusLabel.Text = $"Error: {ex.Message}";
            }
#else
            await DisplayAlert("Error", "This feature is only available on Windows", "OK");
#endif
        }

        /// <summary>
        /// Stop camera button handler
        /// </summary>
        private void OnStopClicked(object sender, EventArgs e)
        {
            StopCamera();
        }

        /// <summary>
        /// Common method to stop camera
        /// </summary>
        private void StopCamera()
        {
#if WINDOWS
            try
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;

                _mediaCapture?.Dispose();
                _mediaCapture = null;

                _isCapturing = false;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    StatusLabel.Text = "Stopped";
                    StatusLabel.IsVisible = true;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Error", $"An error occurred while stopping: {ex.Message}", "OK");
                });
            }
#endif
        }

#if WINDOWS
        /// <summary>
        /// Capture and display one frame from camera
        /// </summary>
        private async Task CaptureFrameAsync()
        {
            if (_mediaCapture == null || !_isCapturing)
                return;

            try
            {
                // Capture to memory stream
                using var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(
                    Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg(),
                    stream);

                // Decode image
                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                // Get pixel data
                var pixelData = await decoder.GetPixelDataAsync();
                var pixels = pixelData.DetachPixelData();

                // Re-encode to JPEG
                using var outputStream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    decoder.DpiX,
                    decoder.DpiY,
                    pixels);
                await encoder.FlushAsync();

                // Convert to byte array
                var bytes = new byte[outputStream.Size];
                outputStream.Seek(0);
                await outputStream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);

                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Capture error: {ex.Message}");
            }
        }
#endif

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Stop when leaving the page
            StopCamera();
        }
    }
}