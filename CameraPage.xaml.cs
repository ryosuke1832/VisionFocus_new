using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
        private bool isCapturing = false;

        public CameraPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeCameraAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // カメラを停止
            CameraView.CameraEnabled = false;
        }

        /// <summary>
        /// カメラの初期化
        /// </summary>
        private async Task InitializeCameraAsync()
        {
            try
            {
                // カメラの権限を確認
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("権限エラー", "カメラの使用許可が必要です", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                // カメラを有効化
                CameraView.CameraEnabled = true;

                ShowStatus("カメラ準備完了");
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"カメラの初期化に失敗しました: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// 写真を撮影
        /// </summary>
        private async void OnCaptureClicked(object sender, EventArgs e)
        {
            if (isCapturing) return;

            try
            {
                isCapturing = true;
                ShowStatus("撮影中...");

                // 写真を撮影
                var result = await CameraView.CaptureImage(default);

                if (result != null)
                {
                    ShowStatus("撮影成功！");

                    // 画像を保存
                    var fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                    await File.WriteAllBytesAsync(filePath, result);

                    // 保存完了メッセージ
                    await DisplayAlert("保存完了", $"画像を保存しました\n{fileName}", "OK");

                    // 画像プレビュー画面に遷移（オプション）
                    await Navigation.PushAsync(new ImagePreviewPage(filePath));
                }
                else
                {
                    ShowStatus("撮影失敗");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"エラー: {ex.Message}");
                await DisplayAlert("エラー", $"撮影に失敗しました: {ex.Message}", "OK");
            }
            finally
            {
                isCapturing = false;
            }
        }

        /// <summary>
        /// ズーム変更
        /// </summary>
        private void OnZoomChanged(object sender, ValueChangedEventArgs e)
        {
            if (CameraView != null)
            {
                CameraView.ZoomFactor = (float)e.NewValue;
            }
        }

        /// <summary>
        /// カメラ切り替え（前面/背面）
        /// </summary>
        private void OnSwitchCameraClicked(object sender, EventArgs e)
        {
            try
            {
                // カメラの向きを切り替え
                if (CameraView.CameraFlashMode == CameraFlashMode.Off)
                {
                    // 実装例：カメラの切り替えロジック
                    ShowStatus("カメラを切り替えました");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"切り替えエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 戻るボタン
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /// <summary>
        /// ステータスメッセージを表示
        /// </summary>
        private async void ShowStatus(string message)
        {
            StatusLabel.Text = message;
            StatusLabel.IsVisible = true;

            // 2秒後に非表示
            await Task.Delay(2000);
            StatusLabel.IsVisible = false;
        }
    }
}