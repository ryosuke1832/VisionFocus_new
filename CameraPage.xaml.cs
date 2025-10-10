using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
        private bool isCapturing = false;
        private int currentCameraIndex = 0;

        public CameraPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // �J���������̊m�F
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("�����G���[", "�J�����ւ̃A�N�Z�X�������K�v�ł�", "OK");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("�G���[", $"�J�����̏������Ɏ��s���܂���: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// �Y�[���l���ύX���ꂽ��
        /// </summary>
        private void OnZoomChanged(object sender, ValueChangedEventArgs e)
        {
            try
            {
                CameraView.ZoomFactor = (float)e.NewValue;
            }
            catch (Exception ex)
            {
                ShowStatus($"�Y�[���G���[: {ex.Message}");
            }
        }

        /// <summary>
        /// �B�e�{�^�����N���b�N���ꂽ��
        /// </summary>
        private async void OnCaptureClicked(object sender, EventArgs e)
        {
            if (isCapturing)
                return;

            try
            {
                isCapturing = true;
                ShowStatus("�B�e��...");

                // �摜���L���v�`��
                var imageStream = await CameraView.CaptureImage(default);

                if (imageStream != null)
                {
                    ShowStatus("�B�e�����I");

                    // �B�e�����摜���v���r���[�y�[�W�ɕ\��
                    await Navigation.PushAsync(new ImagePreviewPage(imageStream));
                }
                else
                {
                    ShowStatus("�B�e�Ɏ��s���܂���");
                    await DisplayAlert("�G���[", "�摜�̃L���v�`���Ɏ��s���܂���", "OK");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"�G���[: {ex.Message}");
                await DisplayAlert("�B�e�G���[", $"�摜�̎B�e�Ɏ��s���܂���: {ex.Message}", "OK");
            }
            finally
            {
                isCapturing = false;
                // 2�b��ɃX�e�[�^�X���\��
                await Task.Delay(2000);
                HideStatus();
            }
        }

        /// <summary>
        /// �J�����؂�ւ��{�^�����N���b�N���ꂽ��
        /// </summary>
        private async void OnSwitchCameraClicked(object sender, EventArgs e)
        {
            try
            {
                // ���p�\�ȃJ�������擾
                var cameras = CameraView.AvailableCameras;

                if (cameras != null && cameras.Count > 1)
                {
                    // ���̃J�����ɐ؂�ւ�
                    currentCameraIndex = (currentCameraIndex + 1) % cameras.Count;
                    CameraView.SelectedCamera = cameras[currentCameraIndex];

                    ShowStatus("�J������؂�ւ��܂���");
                    await Task.Delay(1500);
                    HideStatus();
                }
                else
                {
                    ShowStatus("���̃J���������p�ł��܂���");
                    await Task.Delay(1500);
                    HideStatus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"�؂�ւ��G���[: {ex.Message}");
                await DisplayAlert("�G���[", $"�J�����̐؂�ւ��Ɏ��s���܂���: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// �߂�{�^�����N���b�N���ꂽ��
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /// <summary>
        /// �X�e�[�^�X���b�Z�[�W��\��
        /// </summary>
        private void ShowStatus(string message)
        {
            StatusLabel.Text = message;
            StatusLabel.IsVisible = true;
        }

        /// <summary>
        /// �X�e�[�^�X���b�Z�[�W���\��
        /// </summary>
        private void HideStatus()
        {
            StatusLabel.IsVisible = false;
        }
    }
}