namespace VisionFocus
{
    public partial class ImagePreviewPage : ContentPage
    {
        private readonly string imagePath;

        public ImagePreviewPage(string imagePath)
        {
            InitializeComponent();
            this.imagePath = imagePath;
            LoadImage();
        }

        /// <summary>
        /// 画像を読み込んで表示
        /// </summary>
        private void LoadImage()
        {
            if (File.Exists(imagePath))
            {
                PreviewImage.Source = ImageSource.FromFile(imagePath);
            }
            else
            {
                DisplayAlert("エラー", "画像が見つかりません", "OK");
            }
        }

        /// <summary>
        /// 画像を共有
        /// </summary>
        private async void OnShareClicked(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    await DisplayAlert("エラー", "画像が見つかりません", "OK");
                    return;
                }

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "画像を共有",
                    File = new ShareFile(imagePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("エラー", $"共有に失敗しました: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// 画像を削除
        /// </summary>
        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert(
                "確認",
                "この画像を削除しますか？",
                "削除",
                "キャンセル");

            if (answer)
            {
                try
                {
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                        await DisplayAlert("完了", "画像を削除しました", "OK");
                        await Navigation.PopAsync();
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("エラー", $"削除に失敗しました: {ex.Message}", "OK");
                }
            }
        }

        /// <summary>
        /// プレビューを閉じる
        /// </summary>
        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}