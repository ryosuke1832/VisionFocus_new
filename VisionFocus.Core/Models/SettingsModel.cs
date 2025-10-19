using System.Collections.ObjectModel;
namespace VisionFocus.Core.Models

{
    /// <summary>
    /// Settings data model class
    /// </summary>
    public class SettingsModel
    {
        /// <summary>
        /// Session duration in minutes
        /// </summary>
        public int SessionDurationMinutes { get; set; } = 25;

        /// <summary>
        /// List of subjects/categories
        /// </summary>
        public List<string> Subjects { get; set; } = new List<string> { "Math", "Science", "English" };

        /// <summary>
        /// Alert threshold in seconds
        /// </summary>
        public double AlertThresholdSeconds { get; set; } = 5.0;

        /// <summary>
        /// Warning threshold in seconds
        /// </summary>
        public double WarningThresholdSeconds { get; set; } = 3.0;


        /// <summary>
        /// Alert volume (0.0 - 1.0)
        /// </summary>
        public double AlertVolume { get; set; } = 0.8;

        /// <summary>
        /// Get default settings
        /// </summary>
        public static SettingsModel GetDefault()
        {
            return new SettingsModel
            {
                SessionDurationMinutes = 25,
                Subjects = new List<string> { "Math", "Science", "English" },
                AlertThresholdSeconds = 5.0,
                WarningThresholdSeconds = 3.0,
                AlertVolume = 0.8
            };
        }
    }
}