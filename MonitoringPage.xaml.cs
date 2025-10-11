using System.Diagnostics;

namespace VisionFocus
{
    public partial class MonitoringPage : ContentPage
    {
        private System.Threading.Timer? _monitoringTimer;
        private bool _isMonitoring = false;
        private int _checkCount = 0;
        private const int CheckIntervalSeconds = 4;

        public MonitoringPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Start monitoring
        /// </summary>
        private void OnStartClicked(object sender, EventArgs e)
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _checkCount = 0;

            // Update UI
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusLabel.Text = "Monitoring... (Checking every 4 seconds)";

            // Clear log
            LogContainer.Children.Clear();
            AddLogEntry("Monitoring started", "#4CAF50");

            // Start timer (execute immediately once, then every 4 seconds)
            _monitoringTimer = new System.Threading.Timer(
                async _ => await PerformCheckAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(CheckIntervalSeconds)
            );
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        private void OnStopClicked(object sender, EventArgs e)
        {
            StopMonitoring();
        }

        /// <summary>
        /// Stop monitoring process
        /// </summary>
        private void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;

            // Stop timer
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;

            // Update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusLabel.Text = $"Monitoring stopped (Total {_checkCount} checks)";
                AddLogEntry($"Monitoring stopped (Total checks: {_checkCount})", "#F44336");
            });
        }

        /// <summary>
        /// Periodic check process
        /// </summary>
        private async Task PerformCheckAsync()
        {
            if (!_isMonitoring)
                return;

            try
            {
                _checkCount++;
                string timestamp = DateTime.Now.ToString("HH:mm:ss");

                // Get latest image
                var imagePaths = ImageHelper.GetAllImagePaths();
                if (imagePaths.Count == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"[{timestamp}] Error: No images found", "#FF9800");
                    });
                    return;
                }

                string latestImagePath = imagePaths[0];
                string fileName = Path.GetFileName(latestImagePath);

                // Log check start
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AddLogEntry($"[{timestamp}] Check #{_checkCount}: {fileName}", "#2196F3");
                });

                // Send to API
                string jsonResponse = await RoboflowService.InferImageAsync(latestImagePath);
                string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                // Display result
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AddLogEntry($"[{timestamp}] Result:", "#4CAF50");
                    AddLogEntry(parsedResult, "#333333", isIndented: true);

                    // Scroll log to bottom
                    ScrollToBottom();
                });

                Debug.WriteLine($"[Monitoring] Check #{_checkCount} completed at {timestamp}");
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss");
                    AddLogEntry($"[{timestamp}] Error: {ex.Message}", "#F44336");
                    Debug.WriteLine($"[Monitoring] Error: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// Add log entry
        /// </summary>
        private void AddLogEntry(string message, string color, bool isIndented = false)
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.Transparent,
                BorderColor = Color.FromArgb(color),
                CornerRadius = 5,
                Padding = new Thickness(isIndented ? 20 : 10, 5),
                Margin = new Thickness(0, 2),
                HasShadow = false
            };

            var label = new Label
            {
                Text = message,
                TextColor = Color.FromArgb(color),
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap
            };

            frame.Content = label;
            LogContainer.Children.Add(frame);

            // Remove old logs (keep only latest 50 entries)
            while (LogContainer.Children.Count > 50)
            {
                LogContainer.Children.RemoveAt(0);
            }
        }

        /// <summary>
        /// Scroll log to bottom
        /// </summary>
        private async void ScrollToBottom()
        {
            // Add slight delay before scrolling (wait for layout update)
            await Task.Delay(100);
            await LogScrollView.ScrollToAsync(0, LogContainer.Height, animated: true);
        }

        /// <summary>
        /// Back button click handler
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            StopMonitoring();
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Stop monitoring when page disappears
        /// </summary>
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopMonitoring();
        }
    }
}