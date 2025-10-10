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
        /// 戻るボタン
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            // カメラを停止してから戻る
            StopCamera();
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// カメラ開始ボタン
        /// </summary>
        private async void OnStartClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                StatusLabel.Text = "カメラを初期化中...";

                // カメラ権限チェック
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("エラー", "カメラの権限が必要です", "OK");
                    StatusLabel.Text = "権限エラー";
                    return;
                }

                // MediaCaptureを初期化
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video
                };
                await _mediaCapture.InitializeAsync(settings);

                // タイマーで1秒ごとに撮影
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
                await DisplayAlert("エラー", $"カメラの起動に失敗しました: {ex.Message}", "OK");
                StatusLabel.Text = $"エラー: {ex.Message}";
            }
#else
            await DisplayAlert("エラー", "この機能はWindowsでのみ利用可能です", "OK");
#endif
        }

        /// <summary>
        /// カメラ停止ボタン
        /// </summary>
        private void OnStopClicked(object sender, EventArgs e)
        {
            StopCamera();
        }

        /// <summary>
        /// カメラを停止する共通メソッド
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
                    StatusLabel.Text = "停止しました";
                    StatusLabel.IsVisible = true;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("エラー", $"停止中にエラーが発生しました: {ex.Message}", "OK");
                });
            }
#endif
        }

#if WINDOWS
        /// <summary>
        /// カメラから1フレームをキャプチャして表示
        /// </summary>
        private async Task CaptureFrameAsync()
        {
            if (_mediaCapture == null || !_isCapturing)
                return;

            try
            {
                // メモリストリームに撮影
                using var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(
                    Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg(),
                    stream);

                // 画像をデコード
                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                // ピクセルデータを取得
                var pixelData = await decoder.GetPixelDataAsync();
                var pixels = pixelData.DetachPixelData();

                // JPEGに再エンコード
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

                // バイト配列に変換
                var bytes = new byte[outputStream.Size];
                outputStream.Seek(0);
                await outputStream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);

                // メインスレッドでUIを更新
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"キャプチャエラー: {ex.Message}");
            }
        }
#endif

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // ページを離れる時は停止
            StopCamera();
        }
    }
}