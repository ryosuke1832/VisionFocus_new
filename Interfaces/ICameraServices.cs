namespace VisionFocus.Services
{
    /// <summary>
    /// Interface for camera capture operations
    /// </summary>
    public interface ICameraService : IDisposable
    {
        /// <summary>
        /// Event raised when a frame is captured
        /// </summary>
        event EventHandler<byte[]>? FrameCaptured;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<string>? ErrorOccurred;

        /// <summary>
        /// Event raised when camera starts
        /// </summary>
        event EventHandler? CameraStarted;

        /// <summary>
        /// Event raised when camera stops
        /// </summary>
        event EventHandler? CameraStopped;

        /// <summary>
        /// Initialize and start camera
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> StartCameraAsync();

        /// <summary>
        /// Stop camera
        /// </summary>
        void StopCamera();
    }
}