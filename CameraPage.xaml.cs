// CameraPage.xaml.cs - 置き換え版（Windows最適化、無駄な再エンコード排除）

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

        // 露出補正の目安（白飛び対策）
        private float _expComp = -1.0f;

        // タイマーフレーム間隔（ms）: 100ms ≒ 10fps
        private const int PreviewIntervalMs = 100;

        // 目標解像度（存在しなければ最も近いものを選ぶ）
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
                StatusLabel.Text = "カメラを初期化中...";

                // MediaCapture 初期化
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                };
                await _mediaCapture.InitializeAsync(settings);

                await ConfigureCameraAsync(_mediaCapture);

                // タイマープレビュー開始（10fps）
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs);
                _timer.Tick += async (_, __) => await CaptureFrameAsync();
                _timer.Start();

                _isCapturing = true;

                // UI
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                CaptureButton.IsEnabled = true;
                StatusLabel.IsVisible = false;

                Debug.WriteLine("✅ ライブプレビュー開始（DispatcherTimer 10fps）");
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"カメラの初期化に失敗: {ex.Message}", "OK");
                StatusLabel.Text = $"エラー: {ex.Message}";
                StatusLabel.IsVisible = true;
                Debug.WriteLine(ex);
            }
        }

        private async Task ConfigureCameraAsync(MediaCapture mediaCapture)
        {
            var vdc = mediaCapture.VideoDeviceController;

            // 実用解像度を優先的に設定（例：1280x720）
            try
            {
                var all = vdc.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview)
                             .OfType<VideoEncodingProperties>()
                             .ToList();

                // 1280x720 優先、なければ面積が小さい順で最初
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
                Debug.WriteLine($"解像度設定に失敗: {ex.Message}");
            }

            // 電源周波数（フリッカ抑制）
            try
            {
                vdc.TrySetPowerlineFrequency(PowerlineFrequency.FiftyHertz);
            }
            catch { /* ignore */ }

            // 自動露出 & ISO
            try
            {
                if (vdc.ExposureControl.Supported)
                    await vdc.ExposureControl.SetAutoAsync(true);

                if (vdc.IsoSpeedControl.Supported)
                    await vdc.IsoSpeedControl.SetAutoAsync();
            }
            catch { /* ignore */ }

            // 露出補正（範囲内に収めて設定）
            try
            {
                if (vdc.ExposureCompensationControl.Supported)
                {
                    var min = vdc.ExposureCompensationControl.Min;
                    var max = vdc.ExposureCompensationControl.Max;
                    var step = vdc.ExposureCompensationControl.Step;

                    var clamped = Math.Max(min, Math.Min(max, _expComp));
                    await vdc.ExposureCompensationControl.SetValueAsync(clamped);

                    Debug.WriteLine($"露出補正: {clamped} (range {min}..{max}, step {step})");
                }
            }
            catch { /* ignore */ }

            // WB/フォーカス等（対応デバイスのみ）
            try
            {
                if (vdc.WhiteBalanceControl.Supported)
                    await vdc.WhiteBalanceControl.SetPresetAsync(ColorTemperaturePreset.Auto);
            }
            catch { /* ignore */ }

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
                }
            }
            catch { /* ignore */ }

            try
            {
                if (vdc.BacklightCompensation != null && vdc.BacklightCompensation.Capabilities.Supported)
                    vdc.BacklightCompensation.TrySetValue(0);

                if (vdc.HdrVideoControl.Supported)
                    vdc.HdrVideoControl.Mode = HdrVideoMode.Off;

                if (vdc.VideoTemporalDenoisingControl.Supported)
                    vdc.VideoTemporalDenoisingControl.Mode = VideoTemporalDenoisingMode.Off;
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// タイマーで JPEG 1枚をキャプチャして Image に表示。
        /// ※「プレビュー用途」では本来は CaptureElement/FrameReader が理想だが、
        ///   既存XAML(Image)を活かすため JPEG 直出し＋FromStream で最小変更。
        ///   ここでは「再デコード→再エンコード」は一切行わない。
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

                // そのままバイト列へ（デコード/再エンコードなし）
                stream.Seek(0);
                using var netStream = stream.AsStreamForRead();
                using var ms = new MemoryStream();
                await netStream.CopyToAsync(ms);
                var bytes = ms.ToArray();

                _latestJpegBytes = bytes;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // 短命 Stream を毎回作るのはGCに厳しいが、最小変更で維持
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
                    await DisplayAlert("エラー", "保存可能なフレームがありません", "OK");
                    return;
                }

                string fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filePath = ImageHelper.GetImagePath(fileName);
                await File.WriteAllBytesAsync(filePath, _latestJpegBytes);

                await DisplayAlert("成功", $"画像を保存しました！\n{fileName}", "OK");
                Debug.WriteLine($"画像保存: {filePath}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"画像の保存に失敗: {ex.Message}", "OK");
            }
#else
            await DisplayAlert("未対応", "この機能は Windows でのみ動作します。", "OK");
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
                    // フォルダをエクスプローラで開く
                    Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    await DisplayAlert("エラー", "フォルダが存在しません", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"フォルダを開けませんでした: {ex.Message}", "OK");
            }
#else
            await DisplayAlert("未対応", "この機能は Windows でのみ動作します。", "OK");
#endif
        }

        private async void OnJudgeClicked(object sender, EventArgs e)
        {
            try
            {
                var imagePaths = ImageHelper.GetAllImagePaths();
                if (imagePaths.Count == 0)
                {
                    await DisplayAlert("エラー", "画像が見つかりません。先に画像をキャプチャしてください。", "OK");
                    return;
                }

                string latestImagePath = imagePaths[0];
                string fileName = Path.GetFileName(latestImagePath);

                JudgeButton.IsEnabled = false;
                JudgeButton.Text = "⏳ 処理中...";
                ResultContainer.IsVisible = true;
                ResultLabel.Text = "Roboflow APIに画像を送信中...\nお待ちください...";

                string jsonResponse = await RoboflowService.InferImageAsync(latestImagePath);
                string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                ResultLabel.Text = $"画像: {fileName}\n\n{parsedResult}";
                Debug.WriteLine($"API応答: {jsonResponse}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"画像の処理に失敗: {ex.Message}", "OK");
                ResultLabel.Text = $"エラー: {ex.Message}";
            }
            finally
            {
                JudgeButton.IsEnabled = true;
                JudgeButton.Text = "🔍 最新画像を判定";
            }
        }

        private void StopCamera()
        {
#if WINDOWS
            try
            {
                _isCapturing = false;

                // タイマー停止
                _timer?.Stop();
                _timer = null;

                // 直前フレームの完了待ち（最大500ms）
                SpinWait.SpinUntil(() =>
                    _captureGate.CurrentCount == 1, millisecondsTimeout: 500);

                // MediaCapture 破棄
                _mediaCapture?.Dispose();
                _mediaCapture = null;

                _latestJpegBytes = null;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    CaptureButton.IsEnabled = false;
                    StatusLabel.Text = "停止";
                    StatusLabel.IsVisible = true;

                    // 画面も黒にしたい場合は以下
                    // CameraPreview.Source = null;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("エラー", $"停止中にエラーが発生: {ex.Message}", "OK");
                });
            }
#else
            // 他プラットフォームは何もしない
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopCamera();
        }
    }
}
