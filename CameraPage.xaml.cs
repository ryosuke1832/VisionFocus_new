#if WINDOWS
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;
#endif

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
#if WINDOWS
        private MediaCapture? _mediaCapture;
        private System.Timers.Timer? _timer;
        private bool _isCapturing = false;
        private byte[]? _latestFrameBytes;
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
                CaptureButton.IsEnabled = true;
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
        /// Capture and save image button handler
        /// </summary>
        private async void OnCaptureClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                if (_latestFrameBytes == null || _latestFrameBytes.Length == 0)
                {
                    await DisplayAlert("Error", "No frame available to save", "OK");
                    return;
                }

                // Generate unique filename with date and time
                string fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filePath = ImageHelper.GetImagePath(fileName);

                // Save image to file
                await File.WriteAllBytesAsync(filePath, _latestFrameBytes);

                await DisplayAlert("Success", $"Image saved successfully!\n{fileName}", "OK");
                Debug.WriteLine($"Image saved: {filePath}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
            }
#else
            await DisplayAlert("Error", "This feature is only available on Windows", "OK");
#endif
        }

        /// <summary>
        /// Open folder button handler
        /// </summary>
        private async void OnOpenFolderClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                // Open folder in Windows Explorer
                string folderPath = ImageHelper.ImagesFolderPath;
                if (Directory.Exists(folderPath))
                {
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
            await DisplayAlert("Error", "This feature is only available on Windows", "OK");
#endif
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
                _latestFrameBytes = null;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    CaptureButton.IsEnabled = false;
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

                // Store latest frame for saving
                _latestFrameBytes = bytes;

                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Capture error: {ex.Message}");
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