using System.Runtime.ConstrainedExecution;
using Microsoft.Maui.Controls;
using ScottPlot.Maui;
using VisionFocus.Services;
using System.Globalization;
using System.IO;

namespace VisionFocus;

public partial class StatisticsPage : ContentPage
{
    private string _selectedSubject = string.Empty;
    private DateTime _selectedDate = DateTime.Today;

    public StatisticsPage()
    {
        InitializeComponent();
        LoadSubjects();   // Reuse function logic
        DatePickerFilter.Date = _selectedDate;
        DatePickerFilter.MaximumDate = DateTime.Today;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    // ? Reuse logic from CameraPage
    private void LoadSubjects()
    {
        try
        {
            var settings = SettingsService.LoadSettings();
            SubjectPicker.ItemsSource = settings.Subjects;

            if (settings.Subjects.Count > 0)
            {
                SubjectPicker.SelectedIndex = 0;
                _selectedSubject = settings.Subjects[0];
                LoadChartsForSubjectAndDate();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Subject loading error: {ex.Message}");
        }
    }

    private void OnSubjectChanged(object sender, EventArgs e)
    {
        if (SubjectPicker.SelectedItem is string subject)
        {
            _selectedSubject = subject;
            LoadChartsForSubjectAndDate();
        }
    }

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        _selectedDate = e.NewDate;
        LoadChartsForSubjectAndDate();
    }

    private void LoadChartsForSubjectAndDate()
    {
        ChartsContainer.Children.Clear();

        if (string.IsNullOrEmpty(_selectedSubject))
            return;

        try
        {
            var files = SessionDataService.GetAllSessionDetailFiles()
                .Where(f =>
                {
                    // Extract session date & subject from filename
                    // Format: Session_20251014_130432_Math.csv
                    var parts = Path.GetFileNameWithoutExtension(f).Split('_');
                    if (parts.Length < 4) return false;

                    string datePart = parts[1];
                    string subject = parts[3];

                    if (!DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime fileDate))
                        return false;

                    return subject.Equals(_selectedSubject, StringComparison.OrdinalIgnoreCase)
&& fileDate.Date == _selectedDate.Date;
                })
                .ToList();

            if (files.Count == 0)
            {
                ChartsContainer.Children.Add(new Label
                {
                    Text = "No session data available for selected filters.",
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = Microsoft.Maui.Graphics.Colors.Gray
                });
                return;
            }

            foreach (var file in files)
            {
                var filePath = Path.Combine(SessionDataService.EachDataFolderPath, file);
                var lines = File.ReadAllLines(filePath).Skip(2).ToList(); // skip first 2 header lines

                double[] time = new double[lines.Count];
                double[] alerts = new double[lines.Count];

                for (int i = 0; i < lines.Count; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out double t) &&
                        double.TryParse(parts[1], out double a))
                    {
                        time[i] = t;
                        alerts[i] = a;
                    }
                }

                var plot = new MauiPlot
                {
                    HeightRequest = 350,
                    WidthRequest = 650,
                    Margin = new Thickness(0, 10)
                };

                plot.Plot.Add.Scatter(time, alerts, color: ScottPlot.Colors.Blue);
                plot.Plot.Title(Path.GetFileNameWithoutExtension(file));
                plot.Plot.XLabel("Minutes");
                plot.Plot.YLabel("Alert Count");
                plot.Plot.Axes.SetLimitsY(0, alerts.Max() + 1);
                plot.Refresh();

                ChartsContainer.Children.Add(plot);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading charts: {ex.Message}");
        }
    }
}