#if WINDOWS
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
#endif
using System.Diagnostics;

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
#if WINDOWS
        private MediaCapture? _mediaCapture;
        private MediaFrameReader? _frameReader;           
        private System.Timers.Timer? _timer;
        private bool _isCapturing = false;
        private byte[]? _latestFrameBytes;
        private int _captureBusy = 0;
#endif
#if WINDOWS
        private float _expComp = 0.0f;  
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
                StatusLabel.Text = "Initializing camera...";

                // Permission
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Error", "Camera permission is required", "OK");
                    StatusLabel.Text = "Permission error";
                    return;
                }

                // MediaCapture 
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview 
                };
                await _mediaCapture.InitializeAsync(settings);

                await StartFrameReaderAsync(_mediaCapture);

                await ConfigureCameraAsync(_mediaCapture);

                await Task.Delay(1500);


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
                await DisplayAlert("Error", $"Failed to initialize camera: {ex.Message}", "OK");
                StatusLabel.Text = $"Error: {ex.Message}";
                Debug.WriteLine(ex);
            }
        }

        private async Task StartFrameReaderAsync(MediaCapture mediaCapture)
        {

            MediaFrameSource? colorSource = null;
            foreach (var kv in mediaCapture.FrameSources)
            {
                var src = kv.Value;
                if (src.Info.SourceKind == MediaFrameSourceKind.Color)
                {
                    colorSource = src;
                    break;
                }
            }

            if (colorSource != null)
            {
                _frameReader = await mediaCapture.CreateFrameReaderAsync(colorSource, MediaEncodingSubtypes.Bgra8);
                await _frameReader.StartAsync(); 
            }

        }
        private async Task ConfigureCameraAsync(MediaCapture mediaCapture)
        {
            var v = mediaCapture.VideoDeviceController;

            if (v.ExposureControl.Supported)
                await v.ExposureControl.SetAutoAsync(true);
            if (v.IsoSpeedControl.Supported)
                await v.IsoSpeedControl.SetAutoAsync();


            _expComp = -0.7f; 
            if (v.ExposureCompensationControl.Supported)
                await v.ExposureCompensationControl.SetValueAsync(_expComp);

            v.TrySetPowerlineFrequency(PowerlineFrequency.FiftyHertz);

            if (v.WhiteBalanceControl.Supported)
                await v.WhiteBalanceControl.SetPresetAsync(ColorTemperaturePreset.Auto);

            if (v.FocusControl.Supported)
            {
                var focus = v.FocusControl;
                focus.Configure(new FocusSettings
                {
                    Mode = FocusMode.Continuous,
                    AutoFocusRange = AutoFocusRange.FullRange,
                    DisableDriverFallback = false
                });
                await focus.FocusAsync();
            }


            if (v.BacklightCompensation != null && v.BacklightCompensation.Capabilities.Supported)
                v.BacklightCompensation.TrySetValue(0); 
            if (v.HdrVideoControl.Supported)
                v.HdrVideoControl.Mode = HdrVideoMode.Off;
            if (v.VideoTemporalDenoisingControl.Supported)
                v.VideoTemporalDenoisingControl.Mode = VideoTemporalDenoisingMode.Off;

             if (v.ExposureControl.Supported)
            {
                await v.ExposureControl.SetAutoAsync(false);
                await v.ExposureControl.SetValueAsync(TimeSpan.FromMilliseconds(6)); // ≒1/166s
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
                if (_latestFrameBytes == null || _latestFrameBytes.Length == 0)
                {
                    await DisplayAlert("Error", "No frame available to save", "OK");
                    return;
                }

                string fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filePath = ImageHelper.GetImagePath(fileName);
                await File.WriteAllBytesAsync(filePath, _latestFrameBytes);

                await DisplayAlert("Success", $"Image saved successfully!\n{fileName}", "OK");
                Debug.WriteLine($"Image saved: {filePath}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
            }
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
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;

                if (_frameReader != null)
                {
                    try { _frameReader.StopAsync().AsTask().Wait(500); } catch { }
                    _frameReader.Dispose();
                    _frameReader = null;
                }

                if (_mediaCapture != null)
                {
                    _mediaCapture.Dispose();
                    _mediaCapture = null;
                }

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

        private async Task CaptureFrameAsync()
        {
            if (_mediaCapture == null || !_isCapturing) return;
            if (Interlocked.Exchange(ref _captureBusy, 1) == 1) return; 

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(
                    Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg(),
                    stream);

                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync();
                var pixels = pixelData.DetachPixelData();

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

                var bytes = new byte[(int)outputStream.Size];
                outputStream.Seek(0);
                await outputStream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);

                _latestFrameBytes = bytes;

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
                Interlocked.Exchange(ref _captureBusy, 0);
            }
        }
#endif

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopCamera();
        }
    }
}
