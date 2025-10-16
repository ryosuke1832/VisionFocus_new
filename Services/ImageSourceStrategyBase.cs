// Services/ImageSourceStrategyBase.cs
namespace VisionFocus.Services
{
    /// <summary>
    /// get image source strategy base class
    /// </summary>
    public abstract class ImageSourceStrategyBase
    {
        public abstract Task<string> GetImagePathAsync();

        public virtual string GetDescription()
        {
            return "Image source strategy";
        }
    }
}