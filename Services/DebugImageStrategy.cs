// Services/DebugImageStrategy.cs
using VisionFocus.Utilities;

namespace VisionFocus.Services
{
    public class DebugImageStrategy : ImageSourceStrategyBase
    {
        private string _debugFileName;

        // constructor with default image
        public DebugImageStrategy() : this("Closed.jpg") { }

        public DebugImageStrategy(string fileName)
        {
            _debugFileName = fileName;
        }

        // change debug image at runtime
        public void SetDebugImage(string fileName)
        {
            _debugFileName = fileName;
            System.Diagnostics.Debug.WriteLine($"?? Debug image changed to: {fileName}");
        }

        public override async Task<string> GetImagePathAsync()
        {
            // use the specified debug image
            return await Task.FromResult(ImageHelper.GetImagePath(_debugFileName));
        }

        public override string GetDescription()
        {
            return $"?? Debug mode: {_debugFileName}";
        }
    }
}