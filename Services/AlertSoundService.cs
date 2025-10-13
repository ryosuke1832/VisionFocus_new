#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace VisionFocus
{
    /// <summary>
    /// Alert sound types
    /// </summary>
    public enum AlertSoundType
    {
        Beep = 0,
        Asterisk = 1,
        Exclamation = 2,
        Hand = 3,
        Question = 4
    }

    /// <summary>
    /// Service class for playing alert sounds
    /// Now supports both enum-based and polymorphic strategy-based approach
    /// </summary>
    public static class AlertSoundService
    {
#if WINDOWS
        // Windows API constants for MessageBeep
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONASTERISK = 0x00000040;
        private const uint MB_ICONEXCLAMATION = 0x00000030;
        private const uint MB_ICONHAND = 0x00000010;
        private const uint MB_ICONQUESTION = 0x00000020;

        // Import MessageBeep from Windows API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MessageBeep(uint uType);
#endif

        /// <summary>
        /// Get list of available alert sound names
        /// </summary>
        public static List<string> GetAvailableSounds()
        {
            return new List<string>
            {
                "Beep",
                "Asterisk",
                "Exclamation",
                "Hand",
                "Question"
            };
        }

        /// <summary>
        /// Play alert sound -original
        /// </summary>
        public static void PlaySound(AlertSoundType soundType, double volume = 0.8)
        {
#if WINDOWS
            try
            {
                uint beepType = soundType switch
                {
                    AlertSoundType.Beep => MB_OK,
                    AlertSoundType.Asterisk => MB_ICONASTERISK,
                    AlertSoundType.Exclamation => MB_ICONEXCLAMATION,
                    AlertSoundType.Hand => MB_ICONHAND,
                    AlertSoundType.Question => MB_ICONQUESTION,
                    _ => MB_OK
                };

                MessageBeep(beepType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
            }
#else
            System.Diagnostics.Debug.WriteLine($"Playing sound: {soundType} at volume {volume}");
#endif
        }

        /// <summary>
        /// Play alert using polymorphic strategy 
        /// This method accepts the base class and calls the overridden Play() method
        /// </summary>
        public static void PlaySound(Services.AlertStrategyBase alertStrategy)
        {
            try
            {
                alertStrategy.Play(); // Polymorphic call - runtime binding
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
            }
        }

        /// <summary>
        /// Get alert strategy from sound type 
        /// Demonstrates polymorphism - returns base class but creates derived instances
        /// </summary>
        public static Services.AlertStrategyBase CreateAlertStrategy(AlertSoundType soundType)
        {
            return soundType switch
            {
                AlertSoundType.Beep => new Services.BeepAlert(),
                AlertSoundType.Asterisk => new Services.AsteriskAlert(),
                AlertSoundType.Exclamation => new Services.ExclamationAlert(),
                AlertSoundType.Hand => new Services.HandAlert(),
                AlertSoundType.Question => new Services.QuestionAlert(),
                _ => new Services.BeepAlert()
            };
        }

        /// <summary>
        /// Get alert sound type from index
        /// </summary>
        public static AlertSoundType GetSoundTypeFromIndex(int index)
        {
            return (AlertSoundType)Math.Clamp(index, 0, 4);
        }

        /// <summary>
        /// Play sample sound for testing
        /// </summary>
        public static void PlaySampleSound(int soundIndex, double volume = 0.8)
        {
            var soundType = GetSoundTypeFromIndex(soundIndex);
            PlaySound(soundType, volume);
        }

        /// <summary>
        /// Play sample sound using polymorphic strategy 
        /// </summary>
        public static void PlaySampleSound(Services.AlertStrategyBase alertStrategy)
        {
            PlaySound(alertStrategy);
        }
    }
}