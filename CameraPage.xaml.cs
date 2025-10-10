using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
        private bool isCapturing = false;
        private int currentCameraIndex = 0;

        public CameraPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // カメラ権限の確認
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("権限エラー", "カメラへのアクセス権限が必要です", "OK");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"カメラの初期化に失敗しました: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// ズーム値が変更された時
        /// </summary>
        private void OnZoomChanged(object sender, ValueChangedEventArgs e)
        {
            try
            {
                CameraView.ZoomFactor = (float)e.NewValue;
            }
            catch (Exception ex)
            {
                ShowStatus($"ズームエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 撮影ボタンがクリックされた時
        /// </summary>
        private async void OnCaptureClicked(object sender, EventArgs e)
        {
            if (isCapturing)
                return;

            try
            {
                isCapturing = true;
                ShowStatus("撮影中...");

                // 画像をキャプチャ
                var imageStream = await CameraView.CaptureImage(default);

                if (imageStream != null)
                {
                    ShowStatus("撮影完了！");

                    // 撮影した画像をプレビューページに表示
                    await Navigation.PushAsync(new ImagePreviewPage(imageStream));
                }
                else
                {
                    ShowStatus("撮影に失敗しました");
                    await DisplayAlert("エラー", "画像のキャプチャに失敗しました", "OK");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"エラー: {ex.Message}");
                await DisplayAlert("撮影エラー", $"画像の撮影に失敗しました: {ex.Message}", "OK");
            }
            finally
            {
                isCapturing = false;
                // 2秒後にステータスを非表示
                await Task.Delay(2000);
                HideStatus();
            }
        }

        /// <summary>
        /// カメラ切り替えボタンがクリックされた時
        /// </summary>
        private async void OnSwitchCameraClicked(object sender, EventArgs e)
        {
            try
            {
                // 利用可能なカメラを取得
                var cameras = CameraView.AvailableCameras;

                if (cameras != null && cameras.Count > 1)
                {
                    // 次のカメラに切り替え
                    currentCameraIndex = (currentCameraIndex + 1) % cameras.Count;
                    CameraView.SelectedCamera = cameras[currentCameraIndex];

                    ShowStatus("カメラを切り替えました");
                    await Task.Delay(1500);
                    HideStatus();
                }
                else
                {
                    ShowStatus("他のカメラが利用できません");
                    await Task.Delay(1500);
                    HideStatus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"切り替えエラー: {ex.Message}");
                await DisplayAlert("エラー", $"カメラの切り替えに失敗しました: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// 戻るボタンがクリックされた時
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /// <summary>
        /// ステータスメッセージを表示
        /// </summary>
        private void ShowStatus(string message)
        {
            StatusLabel.Text = message;
            StatusLabel.IsVisible = true;
        }

        /// <summary>
        /// ステータスメッセージを非表示
        /// </summary>
        private void HideStatus()
        {
            StatusLabel.IsVisible = false;
        }
    }
}