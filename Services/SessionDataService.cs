using VisionFocus.Core.Models;

namespace VisionFocus.Services
{
    /// <summary>
    /// Service class for managing session data persistence
    /// </summary>
    public static class SessionDataService
    {
        private static string? _dataFolderPath;
        private static string? _eachDataFolderPath;
        private const string SUMMARY_FILE_NAME = "SessionSummary.csv";
        private const string EACH_DATA_FOLDER_NAME = "EachData";

        /// <summary>
        /// Get data folder path (Resources/Data)
        /// </summary>
        public static string DataFolderPath
        {
            get
            {
                if (_dataFolderPath == null)
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

                    // Set path to Resources/Data
                    _dataFolderPath = Path.Combine(directory.FullName, "Resources", "Data");

                    // Create folder if it doesn't exist
                    if (!Directory.Exists(_dataFolderPath))
                    {
                        Directory.CreateDirectory(_dataFolderPath);
                    }
                }
                return _dataFolderPath;
            }
        }

        /// <summary>
        /// Get each data folder path (Resources/Data/EachData)
        /// </summary>
        public static string EachDataFolderPath
        {
            get
            {
                if (_eachDataFolderPath == null)
                {
                    _eachDataFolderPath = Path.Combine(DataFolderPath, EACH_DATA_FOLDER_NAME);

                    // Create folder if it doesn't exist
                    if (!Directory.Exists(_eachDataFolderPath))
                    {
                        Directory.CreateDirectory(_eachDataFolderPath);
                    }
                }
                return _eachDataFolderPath;
            }
        }

        /// <summary>
        /// Get summary file path
        /// </summary>
        private static string SummaryFilePath => Path.Combine(DataFolderPath, SUMMARY_FILE_NAME);

        /// <summary>
        /// Save session summary (append to master file)
        /// </summary>
        public static void SaveSessionSummary(SessionSummary summary)
        {
            try
            {
                bool fileExists = File.Exists(SummaryFilePath);

                using (var writer = new StreamWriter(SummaryFilePath, append: true))
                {
                    // Write header if file doesn't exist
                    if (!fileExists)
                    {
                        writer.WriteLine(SessionSummary.GetCsvHeader());
                    }

                    // Write summary data
                    writer.WriteLine(summary.ToCsvString());
                }

                System.Diagnostics.Debug.WriteLine($"Session summary saved: {SummaryFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session summary: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load all session summaries
        /// </summary>
        public static List<SessionSummary> LoadAllSessionSummaries()
        {
            var summaries = new List<SessionSummary>();

            try
            {
                if (!File.Exists(SummaryFilePath))
                {
                    return summaries;
                }

                var lines = File.ReadAllLines(SummaryFilePath);

                // Skip header line
                for (int i = 1; i < lines.Length; i++)
                {
                    var summary = SessionSummary.FromCsvString(lines[i]);
                    if (summary != null)
                    {
                        summaries.Add(summary);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading session summaries: {ex.Message}");
            }

            return summaries;
        }

        /// <summary>
        /// Load session summaries filtered by subject
        /// </summary>
        public static List<SessionSummary> LoadSessionSummariesBySubject(string subject)
        {
            var allSummaries = LoadAllSessionSummaries();
            return allSummaries.Where(s => s.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Load session summaries filtered by date range
        /// </summary>
        public static List<SessionSummary> LoadSessionSummariesByDateRange(DateTime startDate, DateTime endDate)
        {
            var allSummaries = LoadAllSessionSummaries();
            return allSummaries.Where(s => s.Date >= startDate && s.Date <= endDate).ToList();
        }

        /// <summary>
        /// Save detailed session data (saved to EachData folder)
        /// </summary>
        public static void SaveSessionDetail(SessionDetail detail)
        {
            try
            {
                string fileName = detail.GetFileName();
                string filePath = Path.Combine(EachDataFolderPath, fileName);

                var lines = detail.ToCsvLines();
                File.WriteAllLines(filePath, lines);

                System.Diagnostics.Debug.WriteLine($"Session detail saved: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session detail: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load detailed session data by file name
        /// </summary>
        public static SessionDetail? LoadSessionDetail(string fileName)
        {
            try
            {
                string filePath = Path.Combine(EachDataFolderPath, fileName);

                if (!File.Exists(filePath))
                {
                    return null;
                }

                var lines = File.ReadAllLines(filePath).ToList();
                return SessionDetail.FromCsvLines(lines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading session detail: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all session detail file names
        /// </summary>
        public static List<string> GetAllSessionDetailFiles()
        {
            try
            {
                if (!Directory.Exists(EachDataFolderPath))
                {
                    return new List<string>();
                }

                return Directory.GetFiles(EachDataFolderPath, "Session_*.csv")
                               .Select(Path.GetFileName)
                               .Where(f => f != null)
                               .Select(f => f!)
                               .OrderByDescending(f => f)
                               .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting session files: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Delete a session detail file
        /// </summary>
        public static bool DeleteSessionDetail(string fileName)
        {
            try
            {
                string filePath = Path.Combine(EachDataFolderPath, fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting session detail: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get statistics for a specific subject
        /// </summary>
        public static SubjectStatistics GetSubjectStatistics(string subject)
        {
            var sessions = LoadSessionSummariesBySubject(subject);

            return new SubjectStatistics
            {
                Subject = subject,
                TotalSessions = sessions.Count,
                TotalStudyMinutes = sessions.Sum(s => s.SessionDurationMinutes),
                TotalAlerts = sessions.Sum(s => s.TotalAlertCount),
                AverageAlertsPerSession = sessions.Count > 0 ? sessions.Average(s => s.TotalAlertCount) : 0
            };
        }
    }

    /// <summary>
    /// Statistics model for a specific subject
    /// </summary>
    public class SubjectStatistics
    {
        public string Subject { get; set; } = string.Empty;
        public int TotalSessions { get; set; }
        public int TotalStudyMinutes { get; set; }
        public int TotalAlerts { get; set; }
        public double AverageAlertsPerSession { get; set; }
    }
}