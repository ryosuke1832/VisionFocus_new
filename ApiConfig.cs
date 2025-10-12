namespace VisionFocus
{
    /// <summary>
    /// Configuration class for API credentials and endpoints
    /// </summary>
    public static class ApiConfig
    {
        // Roboflow API Key
        public const string ROBOFLOW_API_KEY = "VQqYMFYDOhB74NKHXpXw";

        // Dataset name
        public const string DATASET_NAME = "eyes-closed-eyes-open";

        // Model version
        public const string MODEL_VERSION = "1";

        // Inference API endpoint
        public static string InferenceUrl =>
            $"https://detect.roboflow.com/{DATASET_NAME}/{MODEL_VERSION}?api_key={ROBOFLOW_API_KEY}";
    }
}

