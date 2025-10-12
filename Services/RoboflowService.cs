using System.Net;
using System.Text;
using System.Text.Json;

namespace VisionFocus.Services
{
    /// <summary>
    /// Service class for communicating with Roboflow API
    /// </summary>
    public class RoboflowService
    {
        /// <summary>
        /// Sends an image to Roboflow API for inference
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>JSON response from API</returns>
        public static async Task<string> InferImageAsync(string imagePath)
        {
            try
            {
                // Read image file and convert to base64
                byte[] imageArray = await File.ReadAllBytesAsync(imagePath);
                string encoded = Convert.ToBase64String(imageArray);
                byte[] data = Encoding.ASCII.GetBytes(encoded);

                // Configure Service Point Manager for HTTPS
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // Create HTTP request
                WebRequest request = WebRequest.Create(ApiConfig.InferenceUrl);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;

                // Write image data to request stream
                using (Stream stream = request.GetRequestStream())
                {
                    await stream.WriteAsync(data, 0, data.Length);
                }

                // Get response from API
                string responseContent;
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            responseContent = await reader.ReadToEndAsync();
                        }
                    }
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                throw new Exception($"Roboflow API Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses the Roboflow API response and extracts the detection result
        /// </summary>
        /// <param name="jsonResponse">JSON response from API</param>
        /// <returns>Human-readable result string</returns>
        public static string ParseResponse(string jsonResponse)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;

                // Check if predictions exist
                if (root.TryGetProperty("predictions", out JsonElement predictions))
                {
                    int predictionCount = predictions.GetArrayLength();

                    if (predictionCount == 0)
                    {
                        return "No detection found";
                    }

                    StringBuilder result = new StringBuilder();
                    result.AppendLine($"Detections found: {predictionCount}\n");

                    // Parse each prediction
                    int index = 1;
                    foreach (JsonElement prediction in predictions.EnumerateArray())
                    {
                        string className = prediction.GetProperty("class").GetString() ?? "Unknown";
                        double confidence = prediction.GetProperty("confidence").GetDouble();

                        result.AppendLine($"Detection {index}:");
                        result.AppendLine($"  Class: {className}");
                        result.AppendLine($"  Confidence: {confidence:P2}");

                        // Get bounding box coordinates if available
                        if (prediction.TryGetProperty("x", out JsonElement x) &&
                            prediction.TryGetProperty("y", out JsonElement y) &&
                            prediction.TryGetProperty("width", out JsonElement width) &&
                            prediction.TryGetProperty("height", out JsonElement height))
                        {
                            result.AppendLine($"  Position: X={x.GetDouble():F1}, Y={y.GetDouble():F1}");
                            result.AppendLine($"  Size: {width.GetDouble():F1}x{height.GetDouble():F1}");
                        }

                        result.AppendLine();
                        index++;
                    }

                    return result.ToString();
                }
                else
                {
                    return "Invalid response format";
                }
            }
            catch (Exception ex)
            {
                return $"Error parsing response: {ex.Message}";
            }
        }
    }
}