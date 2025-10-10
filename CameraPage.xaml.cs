#if WINDOWS
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
#endif

namespace VisionFocus
{
    public partial class CameraPage : ContentPage
    {
#if WINDOWS
        private MediaCapture? _mediaCapture;
        private System.Timers.Timer? _timer;
        private bool _isCapturing = false;
#endif

        public CameraPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// �߂�{�^��
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            // �J�������~���Ă���߂�
            StopCamera();
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// �J�����J�n�{�^��
        /// </summary>
        private async void OnStartClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                StatusLabel.Text = "�J��������������...";

                // �J���������`�F�b�N
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("�G���[", "�J�����̌������K�v�ł�", "OK");
                    StatusLabel.Text = "�����G���[";
                    return;
                }

                // MediaCapture��������
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video
                };
                await _mediaCapture.InitializeAsync(settings);

                // �^�C�}�[��1�b���ƂɎB�e
                _timer = new System.Timers.Timer(1000);
                _timer.Elapsed += async (s, args) => await CaptureFrameAsync();
                _timer.Start();

                _isCapturing = true;
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                StatusLabel.IsVisible = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("�G���[", $"�J�����̋N���Ɏ��s���܂���: {ex.Message}", "OK");
                StatusLabel.Text = $"�G���[: {ex.Message}";
            }
#else
            await DisplayAlert("�G���[", "���̋@�\��Windows�ł̂ݗ��p�\�ł�", "OK");
#endif
        }

        /// <summary>
        /// �J������~�{�^��
        /// </summary>
        private void OnStopClicked(object sender, EventArgs e)
        {
            StopCamera();
        }

        /// <summary>
        /// �J�������~���鋤�ʃ��\�b�h
        /// </summary>
        private void StopCamera()
        {
#if WINDOWS
            try
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;

                _mediaCapture?.Dispose();
                _mediaCapture = null;

                _isCapturing = false;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    StatusLabel.Text = "��~���܂���";
                    StatusLabel.IsVisible = true;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("�G���[", $"��~���ɃG���[���������܂���: {ex.Message}", "OK");
                });
            }
#endif
        }

#if WINDOWS
        /// <summary>
        /// �J��������1�t���[�����L���v�`�����ĕ\��
        /// </summary>
        private async Task CaptureFrameAsync()
        {
            if (_mediaCapture == null || !_isCapturing)
                return;

            try
            {
                // �������X�g���[���ɎB�e
                using var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(
                    Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg(),
                    stream);

                // �摜���f�R�[�h
                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                // �s�N�Z���f�[�^���擾
                var pixelData = await decoder.GetPixelDataAsync();
                var pixels = pixelData.DetachPixelData();

                // JPEG�ɍăG���R�[�h
                using var outputStream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    decoder.DpiX,
                    decoder.DpiY,
                    pixels);
                await encoder.FlushAsync();

                // �o�C�g�z��ɕϊ�
                var bytes = new byte[outputStream.Size];
                outputStream.Seek(0);
                await outputStream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);

                // ���C���X���b�h��UI���X�V
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�L���v�`���G���[: {ex.Message}");
            }
        }
#endif

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // �y�[�W�𗣂�鎞�͒�~
            StopCamera();
        }
    }
}