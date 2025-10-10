namespace VisionFocus
{
    public partial class ImagePreviewPage : ContentPage
    {
        private readonly string imagePath;

        public ImagePreviewPage(string imagePath)
        {
            InitializeComponent();
            this.imagePath = imagePath;

            // �摜��\��
            if (File.Exists(imagePath))
            {
                PreviewImage.Source = ImageSource.FromFile(imagePath);
            }
        }

        /// <summary>
        /// ���L�{�^���N���b�N
        /// </summary>
        private async void OnShareClicked(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    await DisplayAlert("�G���[", "�摜�t�@�C����������܂���", "OK");
                    return;
                }

                // ���L�@�\���g�p
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "�摜�����L",
                    File = new ShareFile(imagePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("�G���[", $"���L�Ɏ��s���܂���: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// �폜�{�^���N���b�N
        /// </summary>
        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            try
            {
                bool answer = await DisplayAlert(
                    "�m�F",
                    "���̉摜���폜���܂����H",
                    "�폜",
                    "�L�����Z��"
                );

                if (answer)
                {
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                        await DisplayAlert("����", "�摜���폜���܂���", "OK");
                        await Navigation.PopAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("�G���[", $"�폜�Ɏ��s���܂���: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// ����{�^���N���b�N
        /// </summary>
        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}