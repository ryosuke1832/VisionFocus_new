namespace VisionFocus
{
    public partial class ImagePreviewPage : ContentPage
    {
        private readonly string imagePath;

        public ImagePreviewPage(string imagePath)
        {
            InitializeComponent();
            this.imagePath = imagePath;

            // Display image
            if (File.Exists(imagePath))
            {
                PreviewImage.Source = ImageSource.FromFile(imagePath);
            }
        }

        /// <summary>
        /// Share button click handler
        /// </summary>
        private async void OnShareClicked(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    await DisplayAlert("Error", "Image file not found", "OK");
                    return;
                }

                // Use share functionality
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Share Image",
                    File = new ShareFile(imagePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to share: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Delete button click handler
        /// </summary>
        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            try
            {
                bool answer = await DisplayAlert(
                    "Confirm",
                    "Do you want to delete this image?",
                    "Delete",
                    "Cancel"
                );

                if (answer)
                {
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                        await DisplayAlert("Complete", "Image deleted", "OK");
                        await Navigation.PopAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Close button click handler
        /// </summary>
        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}