// Services/CameraImageStrategy.cs
using VisionFocus.Utilities;

namespace VisionFocus.Services
{
    public class CameraImageStrategy : ImageSourceStrategyBase
    {
        public override async Task<string> GetImagePathAsync()
        {
            // use a static image as a placeholder for real camera feed
            return await Task.FromResult(ImageHelper.GetImagePath("RealtimePic.jpg"));
        }

        public override string GetDescription()
        {
            return "?? Real camera feed";
        }
    }
}