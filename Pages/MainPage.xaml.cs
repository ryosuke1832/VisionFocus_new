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
            await Shell.Current.GoToAsync(nameof(CameraPage));
        }

        /// <summary>
        /// Navigate to Statistics Page
        /// </summary>
        private async void OnMonitoringClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(StatisticsPage));
        }

        /// <summary>
        /// Navigate to History Page
        /// </summary>
        private async void OnGalleryClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(HistoryPage));
        }

        /// <summary>
        /// Navigate to Settings Page
        /// </summary>
        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }
    }
}