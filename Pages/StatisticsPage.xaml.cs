using Microsoft.Maui.Controls;
using ScottPlot;
using System.Runtime.ConstrainedExecution;

namespace VisionFocus;

public partial class StatisticsPage : ContentPage
{
    public StatisticsPage()
    {
        InitializeComponent();

        // Load charts with sample data
        LoadCharts();
    }

    // Back button click handler
    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
    private void LoadCharts()
    {
        // --- Line Chart: Eye closures over days
        double[] days = { 1, 2, 3, 4, 5 };
        double[] closures = { 2, 3, 1, 4, 2 };

        LinePlot.Plot.Add.Scatter(days, closures);
        LinePlot.Plot.Title("Eye Closures Over Days");
        LinePlot.Plot.XLabel("Day");
        LinePlot.Plot.YLabel("Eye Closures");
        LinePlot.Refresh();

        // ---Bar Chart: Average closures per subject
        double[] avgClosures = { 2.2, 3.1, 1.2 };
        double[] positions = { 0, 1, 2 };   // positions on X axis
        string[] subjects = { "Math", "Science", "English" };

        // Add bars
        var barPlot = BarPlot.Plot.Add.Bars(avgClosures, positions);

        // Titles and axes
        BarPlot.Plot.Title("Average Eye Closures by Subject");
        BarPlot.Plot.YLabel("Average Closures");

        // Refresh to display chart
        BarPlot.Refresh();

        // --- Bar Chart: Average closures per subject
        double[] averageClosures = { 2.2, 3.1, 1.2 };
        double[] positionss = { 0, 1, 2 };
        string[] subjectss = { "Math", "Science", "English" };

        // Add bars individually with positions
        for (int i = 0; i < averageClosures.Length; i++)
        {
            BarPlot.Plot.Add.Bar(position: positionss[i], value: averageClosures[i]);
        }

        // Create labeled ticks
        ScottPlot.Tick[] ticks = new ScottPlot.Tick[subjectss.Length];
        for (int i = 0; i < subjectss.Length; i++)
        {
            ticks[i] = new ScottPlot.Tick(positionss[i], subjectss[i]);
        }

        // Assign ticks to the bottom axis
        BarPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
        BarPlot.Plot.Axes.Bottom.MajorTickStyle.Length = 0;

        // Titles and axes
        BarPlot.Plot.Title("Average Eye Closures by Subject");
        BarPlot.Plot.YLabel("Average Closures");

        // Optional: remove grid if needed
        BarPlot.Plot.HideGrid();

        // Refresh chart
        BarPlot.Refresh();
    }
}