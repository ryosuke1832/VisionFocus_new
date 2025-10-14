using System.Text;
using System.Text.Json;

namespace VisionFocus.Services
{
    /// <summary>
    /// Service class for communicating with Roboflow API (Enhanced debugging version)
    /// </summary>
    public class RoboflowService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Send image to Roboflow API for inference
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>JSON response from API</returns>
        public static async Task<string> InferImageAsync(string imagePath)
        {
            try
            {
                // 🔍 Debug: Log image path and size to be sent
                System.Diagnostics.Debug.WriteLine($"📤 API transmission started");
                System.Diagnostics.Debug.WriteLine($"   File path: {imagePath}");

                if (File.Exists(imagePath))
                {
                    var fileInfo = new FileInfo(imagePath);
                    System.Diagnostics.Debug.WriteLine($"   File size: {fileInfo.Length} bytes");
                    System.Diagnostics.Debug.WriteLine($"   Last modified: {fileInfo.LastWriteTime}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"   ❌ File does not exist!");
                    throw new FileNotFoundException($"Image file not found: {imagePath}");
                }

                // Read image file and convert to Base64
                byte[] imageArray = await File.ReadAllBytesAsync(imagePath);
                string encoded = Convert.ToBase64String(imageArray);

                System.Diagnostics.Debug.WriteLine($"   Base64 encoding complete: {encoded.Length} characters");

                // Create HTTP content
                var content = new StringContent(encoded, Encoding.ASCII, "application/x-www-form-urlencoded");

                // Send POST request
                System.Diagnostics.Debug.WriteLine($"   API URL: {ApiConfig.InferenceUrl}");
                System.Diagnostics.Debug.WriteLine($"   Sending request...");

                var startTime = DateTime.Now;
                HttpResponseMessage response = await _httpClient.PostAsync(ApiConfig.InferenceUrl, content);
                var responseTime = (DateTime.Now - startTime).TotalMilliseconds;

                System.Diagnostics.Debug.WriteLine($"   Response time: {responseTime:F0}ms");
                System.Diagnostics.Debug.WriteLine($"   Status code: {response.StatusCode}");

                response.EnsureSuccessStatusCode();

                // Read response content
                string responseContent = await response.Content.ReadAsStringAsync();

                // 🔍 Debug: Log entire response
                System.Diagnostics.Debug.WriteLine($"📥 API Response:");
                System.Diagnostics.Debug.WriteLine($"   {responseContent}");

                // Format JSON and extract key information
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(responseContent);
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("predictions", out JsonElement predictions))
                    {
                        int count = predictions.GetArrayLength();
                        System.Diagnostics.Debug.WriteLine($"   Detection count: {count}");

                        foreach (JsonElement prediction in predictions.EnumerateArray())
                        {
                            string className = prediction.GetProperty("class").GetString() ?? "Unknown";
                            double confidence = prediction.GetProperty("confidence").GetDouble();
                            System.Diagnostics.Debug.WriteLine($"   • Class: {className}, Confidence: {confidence:P2}");
                        }
                    }
                }
                catch (Exception parseEx)
                {
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ JSON parse error: {parseEx.Message}");
                }

                return responseContent;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Roboflow API HTTP error: {ex.Message}");
                throw new Exception($"Roboflow API HTTP error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Roboflow API error: {ex.Message}");
                throw new Exception($"Roboflow API error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse Roboflow API response and convert to human-readable format
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
                        return "No detection";
                    }

                    StringBuilder result = new StringBuilder();
                    result.AppendLine($"Detection count: {predictionCount}\n");

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
                return $"Response parse error: {ex.Message}";
            }
        }
    }
}