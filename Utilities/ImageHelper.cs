namespace VisionFocus.Utilities
{
    /// <summary>
    /// Helper class for managing captured images
    /// </summary>
    public static class ImageHelper
    {
        private static string? _imagesFolderPath;

        /// <summary>
        /// Gets the path to the captured images folder
        /// </summary>
        public static string ImagesFolderPath
        {
            get
            {
                if (_imagesFolderPath == null)
                {
                    // Get the solution directory by going up from bin folder
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    // Navigate up to find the .csproj file
                    DirectoryInfo? directory = new DirectoryInfo(baseDirectory);
                    while (directory != null && !File.Exists(Path.Combine(directory.FullName, "VisionFocus.csproj")))
                    {
                        directory = directory.Parent;
                    }

                    if (directory == null)
                    {
                        throw new DirectoryNotFoundException("Could not find project root directory");
                    }

                    // Set path to Resources/Raw/CapturedImages in project source
                    _imagesFolderPath = Path.Combine(directory.FullName, "Resources", "Raw", "CapturedImages");

                    // Ensure folder exists
                    if (!Directory.Exists(_imagesFolderPath))
                    {
                        Directory.CreateDirectory(_imagesFolderPath);
                    }

                    System.Diagnostics.Debug.WriteLine($"Images folder path: {_imagesFolderPath}");
                }
                return _imagesFolderPath;
            }
        }

        /// <summary>
        /// Gets all captured image file paths
        /// </summary>
        /// <returns>List of image file paths</returns>
        public static List<string> GetAllImagePaths()
        {
            if (!Directory.Exists(ImagesFolderPath))
                return new List<string>();

            return Directory.GetFiles(ImagesFolderPath, "*.jpg")
                           .OrderByDescending(f => File.GetCreationTime(f))
                           .ToList();
        }

        /// <summary>
        /// Gets all captured image file names
        /// </summary>
        /// <returns>List of image file names</returns>
        public static List<string> GetAllImageNames()
        {
            return GetAllImagePaths()
                   .Select(p => Path.GetFileName(p))
                   .ToList();
        }

        /// <summary>
        /// Gets the full path for a specific image file name
        /// </summary>
        /// <param name="fileName">Image file name</param>
        /// <returns>Full path to the image</returns>
        public static string GetImagePath(string fileName)
        {
            return Path.Combine(ImagesFolderPath, fileName);
        }

        /// <summary>
        /// Checks if an image exists
        /// </summary>
        /// <param name="fileName">Image file name</param>
        /// <returns>True if image exists</returns>
        public static bool ImageExists(string fileName)
        {
            return File.Exists(GetImagePath(fileName));
        }

        /// <summary>
        /// Deletes a specific image
        /// </summary>
        /// <param name="fileName">Image file name to delete</param>
        /// <returns>True if deleted successfully</returns>
        public static bool DeleteImage(string fileName)
        {
            try
            {
                string filePath = GetImagePath(fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets ImageSource from file path for MAUI Image control
        /// </summary>
        /// <param name="filePath">Full path to image file</param>
        /// <returns>ImageSource object</returns>
        public static ImageSource? GetImageSource(string filePath)
        {
            if (File.Exists(filePath))
            {
                return ImageSource.FromFile(filePath);
            }
            return null;
        }

        /// <summary>
        /// Gets the count of captured images
        /// </summary>
        /// <returns>Number of images</returns>
        public static int GetImageCount()
        {
            return GetAllImagePaths().Count;
        }
    }
}