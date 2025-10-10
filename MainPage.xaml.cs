namespace VisionFocus
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 埋め込みカメラページに遷移
        /// </summary>
        private async void OnCameraClicked(object sender, EventArgs e)
        {
            try
            {
                // カメラ権限を確認
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    StatusLabel.Text = "カメラへのアクセス許可が必要です";
                    await DisplayAlert("権限エラー", "カメラへのアクセス権限が必要です", "OK");
                    return;
                }

                // 埋め込みカメラページに遷移
                await Navigation.PushAsync(new CameraPage());
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"カメラページを開けませんでした: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// ギャラリーから画像を選択
        /// </summary>
        private async void OnGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                StatusLabel.Text = "ギャラリーを開いています...";

                // 写真の読み取り権限を確認
                var status = await Permissions.CheckStatusAsync<Permissions.Photos>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Photos>();
                }

                if (status != PermissionStatus.Granted)
                {
                    StatusLabel.Text = "ギャラリーへのアクセス許可が必要です";
                    await DisplayAlert("権限エラー", "写真へのアクセス権限が必要です", "OK");
                    return;
                }

                // ギャラリーから画像を選択
                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "画像を選択してください"
                });

                if (photo != null)
                {
                    // 選択した画像を表示
                    await LoadPhotoAsync(photo);
                    StatusLabel.Text = $"選択完了: {photo.FileName}";
                }
                else
                {
                    StatusLabel.Text = "画像の選択がキャンセルされました";
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"エラー: {ex.Message}";
                await DisplayAlert("エラー", $"画像の選択に失敗しました: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// 撮影/選択した画像を読み込んで表示
        /// </summary>
        private async Task LoadPhotoAsync(FileResult photo)
        {
            // 画像ストリームを開く
            var stream = await photo.OpenReadAsync();

            // ImageSourceに変換
            CapturedImage.Source = ImageSource.FromStream(() => stream);
            CapturedImage.IsVisible = true;
        }
    }
}