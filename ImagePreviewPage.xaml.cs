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
        /// �摜��ǂݍ���ŕ\��
        /// </summary>
        private void LoadImage()
        {
            try
            {
                PreviewImage.Source = ImageSource.FromStream(() => imageStream);
            }
            catch (Exception ex)
            {
                DisplayAlert("�G���[", $"�摜�̓ǂݍ��݂Ɏ��s���܂���: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// ���L�{�^�����N���b�N���ꂽ��
        /// </summary>
        private async void OnShareClicked(object sender, EventArgs e)
        {
            try
            {
                // �摜���ꎞ�t�@�C���Ƃ��ĕۑ�
                if (string.IsNullOrEmpty(savedFilePath))
                {
                    savedFilePath = await SaveImageToTempAsync();
                }

                if (!string.IsNullOrEmpty(savedFilePath))
                {
                    // ���L�@�\���g�p
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "�摜�����L",
                        File = new ShareFile(savedFilePath)
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("�G���[", $"���L�Ɏ��s���܂���: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// �폜�{�^�����N���b�N���ꂽ��
        /// </summary>
        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            var result = await DisplayAlert("�m�F", "���̉摜���폜���܂���?", "�폜", "�L�����Z��");

            if (result)
            {
                try
                {
                    // �ꎞ�t�@�C�����폜
                    if (!string.IsNullOrEmpty(savedFilePath) && File.Exists(savedFilePath))
                    {
                        File.Delete(savedFilePath);
                    }

                    await DisplayAlert("����", "�摜���폜���܂���", "OK");
                    await Navigation.PopAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("�G���[", $"�폜�Ɏ��s���܂���: {ex.Message}", "OK");
                }
            }
        }

        /// <summary>
        /// ����{�^�����N���b�N���ꂽ��
        /// </summary>
        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /// <summary>
        /// �摜���ꎞ�t�@�C���Ƃ��ĕۑ�
        /// </summary>
        private async Task<string> SaveImageToTempAsync()
        {
            try
            {
                var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                // �X�g���[�������Z�b�g
                if (imageStream.CanSeek)
                {
                    imageStream.Seek(0, SeekOrigin.Begin);
                }

                // �t�@�C���ɕۑ�
                using (var fileStream = File.Create(filePath))
                {
                    await imageStream.CopyToAsync(fileStream);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                await DisplayAlert("�G���[", $"�摜�̕ۑ��Ɏ��s���܂���: {ex.Message}", "OK");
                return null;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // �X�g���[�������
            imageStream?.Dispose();
        }
    }
}