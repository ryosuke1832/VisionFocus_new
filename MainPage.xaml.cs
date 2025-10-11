namespace VisionFocus
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Navigate to Camera Page
        /// </summary>
        private async void OnCameraClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//CameraPage");
        }

        /// <summary>
        /// Navigate to Gallery Page (planned for future implementation)
        /// </summary>
        private async void OnGalleryClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Notice", "Gallery feature is currently under development", "OK");
        }

        /// <summary>
        /// Navigate to Settings Page (planned for future implementation)
        /// </summary>
        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Notice", "Settings feature is currently under development", "OK");
        }
    }
}