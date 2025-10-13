namespace VisionFocus.Services
{
    /// <summary>
    /// Abstract base class for alert strategies 
    /// Demonstrates inheritance and method overriding
    /// </summary>
    public abstract class AlertStrategyBase
    {
        /// <summary>
        /// Volume level (0.0 to 1.0)
        /// </summary>
        public double Volume { get; set; } = 0.8;

        /// <summary>
        /// Play the alert sound - must be implemented by derived classes
        /// </summary>
        public abstract void Play();

        /// <summary>
        /// Get description of the alert type
        /// </summary>
        public virtual string GetDescription()
        {
            return "Alert sound";
        }

        /// <summary>
        /// Play with specified volume (method overloading example)
        /// </summary>
        public void Play(double volume)
        {
            Volume = volume;
            Play();
        }
    }
}