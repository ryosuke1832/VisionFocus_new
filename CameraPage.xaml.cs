using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
        private bool isCapturing = false;

        public CameraPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeCameraAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // �J�������~
            CameraView.CameraEnabled = false;
        }

        /// <summary>
        /// �J�����̏�����
        /// </summary>
        private async Task InitializeCameraAsync()
        {
            try
            {
                // �J�����̌������m�F
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("�����G���[", "�J�����̎g�p�����K�v�ł�", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                // �J������L����
                CameraView.CameraEnabled = true;

                ShowStatus("�J������������");
            }
            catch (Exception ex)
            {
                await DisplayAlert("�G���[", $"�J�����̏������Ɏ��s���܂���: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// �ʐ^���B�e
        /// </summary>
        private async void OnCaptureClicked(object sender, EventArgs e)
        {
            if (isCapturing) return;

            try
            {
                isCapturing = true;
                ShowStatus("�B�e��...");

                // �ʐ^���B�e
                var result = await CameraView.CaptureImage(default);

                if (result != null)
                {
                    ShowStatus("�B�e�����I");

                    // �摜��ۑ�
                    var fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                    await File.WriteAllBytesAsync(filePath, result);

                    // �ۑ��������b�Z�[�W
                    await DisplayAlert("�ۑ�����", $"�摜��ۑ����܂���\n{fileName}", "OK");

                    // �摜�v���r���[��ʂɑJ�ځi�I�v�V�����j
                    await Navigation.PushAsync(new ImagePreviewPage(filePath));
                }
                else
                {
                    ShowStatus("�B�e���s");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"�G���[: {ex.Message}");
                await DisplayAlert("�G���[", $"�B�e�Ɏ��s���܂���: {ex.Message}", "OK");
            }
            finally
            {
                isCapturing = false;
            }
        }

        /// <summary>
        /// �Y�[���ύX
        /// </summary>
        private void OnZoomChanged(object sender, ValueChangedEventArgs e)
        {
            if (CameraView != null)
            {
                CameraView.ZoomFactor = (float)e.NewValue;
            }
        }

        /// <summary>
        /// �J�����؂�ւ��i�O��/�w�ʁj
        /// </summary>
        private void OnSwitchCameraClicked(object sender, EventArgs e)
        {
            try
            {
                // �J�����̌�����؂�ւ�
                if (CameraView.CameraFlashMode == CameraFlashMode.Off)
                {
                    // ������F�J�����̐؂�ւ����W�b�N
                    ShowStatus("�J������؂�ւ��܂���");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"�؂�ւ��G���[: {ex.Message}");
            }
        }

        /// <summary>
        /// �߂�{�^��
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /// <summary>
        /// �X�e�[�^�X���b�Z�[�W��\��
        /// </summary>
        private async void ShowStatus(string message)
        {
            StatusLabel.Text = message;
            StatusLabel.IsVisible = true;

            // 2�b��ɔ�\��
            await Task.Delay(2000);
            StatusLabel.IsVisible = false;
        }
    }
}