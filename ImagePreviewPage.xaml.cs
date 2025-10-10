namespace VisionFocus
{
    public partial class ImagePreviewPage : ContentPage
    {
        private readonly Stream imageStream;
        private string savedFilePath;

        public ImagePreviewPage(Stream stream)
        {
            InitializeComponent();
            imageStream = stream;
            LoadImage();
        }

        /// <summary>
        /// 画像を読み込んで表示
        /// </summary>
        private void LoadImage()
        {
            try
            {
                PreviewImage.Source = ImageSource.FromStream(() => imageStream);
            }
            catch (Exception ex)
            {
                DisplayAlert("エラー", $"画像の読み込みに失敗しました: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// 共有ボタンがクリックされた時
        /// </summary>
        private async void OnShareClicked(object sender, EventArgs e)
        {
            try
            {
                // 画像を一時ファイルとして保存
                if (string.IsNullOrEmpty(savedFilePath))
                {
                    savedFilePath = await SaveImageToTempAsync();
                }

                if (!string.IsNullOrEmpty(savedFilePath))
                {
                    // 共有機能を使用
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "画像を共有",
                        File = new ShareFile(savedFilePath)
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"共有に失敗しました: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// 削除ボタンがクリックされた時
        /// </summary>
        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            var result = await DisplayAlert("確認", "この画像を削除しますか?", "削除", "キャンセル");

            if (result)
            {
                try
                {
                    // 一時ファイルを削除
                    if (!string.IsNullOrEmpty(savedFilePath) && File.Exists(savedFilePath))
                    {
                        File.Delete(savedFilePath);
                    }

                    await DisplayAlert("完了", "画像を削除しました", "OK");
                    await Navigation.PopAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("エラー", $"削除に失敗しました: {ex.Message}", "OK");
                }
            }
        }

        /// <summary>
        /// 閉じるボタンがクリックされた時
        /// </summary>
        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /// <summary>
        /// 画像を一時ファイルとして保存
        /// </summary>
        private async Task<string> SaveImageToTempAsync()
        {
            try
            {
                var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                // ストリームをリセット
                if (imageStream.CanSeek)
                {
                    imageStream.Seek(0, SeekOrigin.Begin);
                }

                // ファイルに保存
                using (var fileStream = File.Create(filePath))
                {
                    await imageStream.CopyToAsync(fileStream);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"画像の保存に失敗しました: {ex.Message}", "OK");
                return null;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // ストリームを閉じる
            imageStream?.Dispose();
        }
    }
}