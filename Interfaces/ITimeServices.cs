namespace VisionFocus.Services
{
    /// <summary>
    /// Interface for session timer management
    /// </summary>
    public interface ITimerService : IDisposable
    {
        /// <summary>
        /// Event raised when time is updated
        /// </summary>
        event EventHandler<int>? TimeUpdated;

        /// <summary>
        /// Event raised when session completes
        /// </summary>
        event EventHandler? SessionCompleted;

        /// <summary>
        /// Remaining time in seconds
        /// </summary>
        int RemainingSeconds { get; }

        /// <summary>
        /// Formatted time string (MM:SS)
        /// </summary>
        string FormattedTime { get; }

        /// <summary>
        /// Whether the timer is paused
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Start timer with specified duration
        /// </summary>
        /// <param name="durationMinutes">Duration in minutes</param>
        void Start(int durationMinutes);

        /// <summary>
        /// Stop timer
        /// </summary>
        void Stop();

        /// <summary>
        /// Toggle pause/resume
        /// </summary>
        void TogglePause();

        /// <summary>
        /// Reset timer
        /// </summary>
        void Reset();
    }
}