namespace VisionFocus
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// カメラページへ遷移
        /// </summary>
        private async void OnCameraClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//CameraPage");
        }

        /// <summary>
        /// ギャラリーページへ遷移（将来実装予定）
        /// </summary>
        private async void OnGalleryClicked(object sender, EventArgs e)
        {
            await DisplayAlert("お知らせ", "ギャラリー機能は現在開発中です", "OK");
        }

        /// <summary>
        /// 設定ページへ遷移（将来実装予定）
        /// </summary>
        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await DisplayAlert("お知らせ", "設定機能は現在開発中です", "OK");
        }
    }
}