#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace VisionFocus.Services
{
    /// <summary>
    /// Beep alert strategy 
    public class BeepAlert : AlertStrategyBase
    {
        public override void Play()
        {
#if WINDOWS
            MessageBeep(0x00000000);
#else
            System.Diagnostics.Debug.WriteLine($"Playing Beep at volume {Volume}");
#endif
        }

        public override string GetDescription()
        {
            return "Simple beep sound";
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);
#endif
    }

    /// <summary>
    /// Asterisk alert strategy
    /// </summary>
    public class AsteriskAlert : AlertStrategyBase
    {
        public override void Play()
        {
#if WINDOWS
            MessageBeep(0x00000040);
#else
            System.Diagnostics.Debug.WriteLine($"Playing Asterisk at volume {Volume}");
#endif
        }

        public override string GetDescription()
        {
            return "Asterisk (information) sound";
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);
#endif
    }

    /// <summary>
    /// Exclamation alert strategy
    /// </summary>
    public class ExclamationAlert : AlertStrategyBase
    {
        public override void Play()
        {
#if WINDOWS
            MessageBeep(0x00000030);
#else
            System.Diagnostics.Debug.WriteLine($"Playing Exclamation at volume {Volume}");
#endif
        }

        public override string GetDescription()
        {
            return "Exclamation (warning) sound";
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);
#endif
    }

    /// <summary>
    /// Hand alert strategy 
    /// </summary>
    public class HandAlert : AlertStrategyBase
    {
        public override void Play()
        {
#if WINDOWS
            MessageBeep(0x00000010);
#else
            System.Diagnostics.Debug.WriteLine($"Playing Hand at volume {Volume}");
#endif
        }

        public override string GetDescription()
        {
            return "Hand (critical stop) sound";
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);
#endif
    }

    /// <summary>
    /// Question alert strategy
    /// </summary>
    public class QuestionAlert : AlertStrategyBase
    {
        public override void Play()
        {
#if WINDOWS
            MessageBeep(0x00000020);
#else
            System.Diagnostics.Debug.WriteLine($"Playing Question at volume {Volume}");
#endif
        }

        public override string GetDescription()
        {
            return "Question sound";
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);
#endif
    }
}