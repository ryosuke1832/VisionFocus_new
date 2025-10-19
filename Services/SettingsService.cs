using System.Text.Json;
using VisionFocus.Core.Models;

namespace VisionFocus
{
    /// <summary>
    /// Service class for loading and saving settings
    /// </summary>
    public static class SettingsService
    {
        private static string? _settingsFilePath;
        private static SettingsModel? _cachedSettings;

        /// <summary>
        /// Get settings file path
        /// </summary>
        public static string SettingsFilePath
        {
            get
            {
                if (_settingsFilePath == null)
                {
                    // Get project root directory
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    DirectoryInfo? directory = new DirectoryInfo(baseDirectory);

                    while (directory != null && !File.Exists(Path.Combine(directory.FullName, "VisionFocus.csproj")))
                    {
                        directory = directory.Parent;
                    }

                    if (directory == null)
                    {
                        throw new DirectoryNotFoundException("Could not find project root directory");
                    }

                    // Set path to Resources/Data/Setting.json
                    string dataFolderPath = Path.Combine(directory.FullName, "Resources", "Data");

                    // Create folder if it doesn't exist
                    if (!Directory.Exists(dataFolderPath))
                    {
                        Directory.CreateDirectory(dataFolderPath);
                    }

                    _settingsFilePath = Path.Combine(dataFolderPath, "Setting.json");
                }
                return _settingsFilePath;
            }
        }

        /// <summary>
        /// Load settings from file
        /// </summary>
        public static SettingsModel LoadSettings()
        {
            try
            {
                if (_cachedSettings != null)
                {
                    return _cachedSettings;
                }

                if (File.Exists(SettingsFilePath))
                {
                    string jsonString = File.ReadAllText(SettingsFilePath);
                    _cachedSettings = JsonSerializer.Deserialize<SettingsModel>(jsonString) ?? SettingsModel.GetDefault();
                }
                else
                {
                    // Create default settings if file doesn't exist
                    _cachedSettings = SettingsModel.GetDefault();
                    SaveSettings(_cachedSettings);
                }

                return _cachedSettings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                return SettingsModel.GetDefault();
            }
        }

        /// <summary>
        /// Save settings to file
        /// </summary>
        public static void SaveSettings(SettingsModel settings)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonString = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, jsonString);

                _cachedSettings = settings;

                System.Diagnostics.Debug.WriteLine($"Settings saved to: {SettingsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Clear settings cache
        /// </summary>
        public static void ClearCache()
        {
            _cachedSettings = null;
        }
    }
}