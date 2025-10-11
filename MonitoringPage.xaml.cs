using System.Diagnostics;

namespace VisionFocus
{
    public partial class MonitoringPage : ContentPage
    {
        private Task? _monitoringTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isMonitoring = false;
        private int _checkCount = 0;

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
            StatusLabel.Text = "Monitoring... (Continuous checking)";

            // Clear log
            LogContainer.Children.Clear();
            AddLogEntry("Monitoring started", "#4CAF50");

            // Start monitoring loop
            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cancellationTokenSource.Token));
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

            // Cancel monitoring task
            _cancellationTokenSource?.Cancel();

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
        /// Monitoring loop - continuously checks after each API response
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isMonitoring)
            {
                try
                {
                    await PerformCheckAsync();
                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                        AddLogEntry($"[{timestamp}] Error: {ex.Message}", "#F44336");
                        Debug.WriteLine($"[Monitoring] Error: {ex.Message}");
                    });
                }

                // Small delay to prevent overwhelming the system
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Perform single check with timing information
        /// </summary>
        private async Task PerformCheckAsync()
        {
            if (!_isMonitoring)
                return;

            try
            {
                _checkCount++;

                // Get latest image
                var imagePaths = ImageHelper.GetAllImagePaths();
                if (imagePaths.Count == 0)
                {
                    string errorTime = DateTime.Now.ToString("HH:mm:ss.fff");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        AddLogEntry($"[{errorTime}] Error: No images found", "#FF9800");
                    });
                    return;
                }

                string latestImagePath = imagePaths[0];
                string fileName = Path.GetFileName(latestImagePath);

                // Record send time
                DateTime sendTime = DateTime.Now;
                string sendTimeStr = sendTime.ToString("HH:mm:ss.fff");

                // Log check start with send time
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AddLogEntry($"„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª„ª", "#E0E0E0");
                    AddLogEntry($"Check #{_checkCount}: {fileName}", "#2196F3");
                    AddLogEntry($"?? API Sent: {sendTimeStr}", "#9C27B0");
                });

                // Send to API
                string jsonResponse = await RoboflowService.InferImageAsync(latestImagePath);

                // Record receive time
                DateTime receiveTime = DateTime.Now;
                string receiveTimeStr = receiveTime.ToString("HH:mm:ss.fff");
                TimeSpan responseTime = receiveTime - sendTime;

                string parsedResult = RoboflowService.ParseResponse(jsonResponse);

                // Display result with timing information
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AddLogEntry($"?? API Received: {receiveTimeStr}", "#9C27B0");
                    AddLogEntry($"??  Response Time: {responseTime.TotalMilliseconds:F0}ms", "#FF9800");
                    AddLogEntry($"Result:", "#4CAF50");
                    AddLogEntry(parsedResult, "#333333", isIndented: true);

                    // Scroll log to bottom
                    ScrollToBottom();
                });

                Debug.WriteLine($"[Monitoring] Check #{_checkCount} | Send: {sendTimeStr} | Receive: {receiveTimeStr} | Response: {responseTime.TotalMilliseconds}ms");
            }
            catch (Exception ex)
            {
                DateTime errorTime = DateTime.Now;
                string errorTimeStr = errorTime.ToString("HH:mm:ss.fff");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AddLogEntry($"[{errorTimeStr}] Error: {ex.Message}", "#F44336");
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