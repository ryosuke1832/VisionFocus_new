#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace VisionFocus
{
    /// <summary>
    /// Service class for playing alert sounds
    /// </summary>
    public static class AlertSoundService
    {
#if WINDOWS
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MessageBeep(uint uType);

        private const uint MB_OK = 0x00000000;
#endif

        /// <summary>
        /// Play alert sound with specified volume
        /// </summary>
        public static void PlaySound(double volume = 0.8)
        {
#if WINDOWS
            try
            {
                MessageBeep(MB_OK);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
            }
#else
            System.Diagnostics.Debug.WriteLine($"Playing sound at volume {volume}");
#endif
        }
    }
}