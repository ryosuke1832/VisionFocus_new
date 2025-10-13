#if WINDOWS
using Microsoft.UI.Xaml;
#endif

namespace VisionFocus.Services
{
    /// <summary>
    /// Session timer management service
    /// </summary>
    public class SessionTimerService : ITimerService
    {
#if WINDOWS
        private DispatcherTimer? _timer;
#endif
        private int _remainingSeconds;
        private int _totalSeconds;
        private bool _isPaused = false;

        // Events
        public event EventHandler<int>? TimeUpdated;
        public event EventHandler? SessionCompleted;

        /// <summary>
        /// Remaining time in seconds
        /// </summary>
        public int RemainingSeconds => _remainingSeconds;

        /// <summary>
        /// Remaining time in MM:SS format
        /// </summary>
        public string FormattedTime
        {
            get
            {
                int minutes = _remainingSeconds / 60;
                int seconds = _remainingSeconds % 60;
                return $"{minutes:D2}:{seconds:D2}";
            }
        }

        /// <summary>
        /// Whether the timer is paused
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Start timer
        /// </summary>
        /// <param name="durationMinutes">Session duration in minutes</param>
        public void Start(int durationMinutes)
        {
#if WINDOWS
            _totalSeconds = durationMinutes * 60;
            _remainingSeconds = _totalSeconds;
            _isPaused = false;

            // Initialize timer
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += OnTimerTick;
            _timer.Start();

            // Notify initial value
            TimeUpdated?.Invoke(this, _remainingSeconds);
#endif
        }

        /// <summary>
        /// Stop timer
        /// </summary>
        public void Stop()
        {
#if WINDOWS
            _timer?.Stop();
            _timer = null;
            _isPaused = false;
#endif
        }

        /// <summary>
        /// Pause/Resume timer
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;
        }

        /// <summary>
        /// Reset timer
        /// </summary>
        public void Reset()
        {
            _remainingSeconds = _totalSeconds;
            _isPaused = false;
            TimeUpdated?.Invoke(this, _remainingSeconds);
        }

#if WINDOWS
        /// <summary>
        /// Timer tick event
        /// </summary>
        private void OnTimerTick(object? sender, object e)
        {
            if (_isPaused) return;

            _remainingSeconds--;

            // Notify time update
            TimeUpdated?.Invoke(this, _remainingSeconds);

            // Check if time is up
            if (_remainingSeconds <= 0)
            {
                Stop();
                SessionCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
#endif

        public void Dispose()
        {
            Stop();
        }
    }
}