namespace VisionFocus.Core.Models
{
    /// <summary>
    /// Model class for session summary data
    /// </summary>
    public class SessionSummary
    {
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public string Subject { get; set; } = string.Empty;
        public int SessionDurationMinutes { get; set; }
        public int TotalAlertCount { get; set; }

        /// <summary>
        /// Convert to CSV format string
        /// </summary>
        public string ToCsvString()
        {
            return $"{Date:yyyy-MM-dd},{StartTime:hh\\:mm\\:ss},{Subject},{SessionDurationMinutes},{TotalAlertCount}";
        }

        /// <summary>
        /// Create from CSV format string
        /// </summary>
        public static SessionSummary? FromCsvString(string csvLine)
        {
            try
            {
                var parts = csvLine.Split(',');
                if (parts.Length != 5) return null;

                return new SessionSummary
                {
                    Date = DateTime.Parse(parts[0]),
                    StartTime = TimeSpan.Parse(parts[1]),
                    Subject = parts[2],
                    SessionDurationMinutes = int.Parse(parts[3]),
                    TotalAlertCount = int.Parse(parts[4])
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get CSV header
        /// </summary>
        public static string GetCsvHeader()
        {
            return "Date,StartTime,Subject,SessionDurationMinutes,TotalAlertCount";
        }
    }

    /// <summary>
    /// Model class for minute-by-minute alert data
    /// </summary>
    public class MinuteAlertData
    {
        public int MinuteIndex { get; set; }
        public int AlertCount { get; set; }

        public string ToCsvString()
        {
            return $"{MinuteIndex},{AlertCount}";
        }

        public static MinuteAlertData? FromCsvString(string csvLine)
        {
            try
            {
                var parts = csvLine.Split(',');
                if (parts.Length != 2) return null;

                return new MinuteAlertData
                {
                    MinuteIndex = int.Parse(parts[0]),
                    AlertCount = int.Parse(parts[1])
                };
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Model class for detailed session data
    /// </summary>
    public class SessionDetail
    {
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public string Subject { get; set; } = string.Empty;
        public List<MinuteAlertData> MinuteData { get; set; } = new List<MinuteAlertData>();

        /// <summary>
        /// Get unique file name for this session
        /// </summary>
        public string GetFileName()
        {
            return $"Session_{Date:yyyyMMdd}_{StartTime:hhmmss}_{Subject}.csv";
        }

        /// <summary>
        /// Convert to CSV format lines
        /// </summary>
        public List<string> ToCsvLines()
        {
            var lines = new List<string>
            {
                $"{Date:yyyy-MM-dd},{StartTime:hh\\:mm\\:ss},{Subject}",
                "Time,AlertCount"
            };

            lines.AddRange(MinuteData.Select(m => m.ToCsvString()));

            return lines;
        }

        /// <summary>
        /// Create from CSV format lines
        /// </summary>
        public static SessionDetail? FromCsvLines(List<string> lines)
        {
            try
            {
                if (lines.Count < 3) return null;

                // Parse header line
                var headerParts = lines[0].Split(',');
                if (headerParts.Length != 3) return null;

                var detail = new SessionDetail
                {
                    Date = DateTime.Parse(headerParts[0]),
                    StartTime = TimeSpan.Parse(headerParts[1]),
                    Subject = headerParts[2]
                };

                // Skip column header line (lines[1])
                // Parse minute data
                for (int i = 2; i < lines.Count; i++)
                {
                    var minuteData = MinuteAlertData.FromCsvString(lines[i]);
                    if (minuteData != null)
                    {
                        detail.MinuteData.Add(minuteData);
                    }
                }

                return detail;
            }
            catch
            {
                return null;
            }
        }
    }
}