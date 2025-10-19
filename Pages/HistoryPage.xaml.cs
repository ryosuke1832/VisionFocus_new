using VisionFocus.Services;

using VisionFocus.Core.Models;

using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using Microsoft.Maui.Controls;

namespace VisionFocus

{

    public partial class HistoryPage : ContentPage

    {

        public HistoryPage()

        {

            InitializeComponent();

        }

        /// Back button click handler

        private async void OnBackClicked(object sender, EventArgs e)

        {

            await Shell.Current.GoToAsync("..");

        }

        protected override void OnAppearing()

        {

            base.OnAppearing();

            LoadSessionHistory();

        }

        private void LoadSessionHistory()

        {

            try

            {

                var summaries = SessionDataService.LoadAllSessionSummaries();

                if (summaries.Count == 0)

                {

                    DisplayAlert("No Data", "No session history found.", "OK");

                    return;

                }

                // Sort latest first

                var sortedSummaries = summaries

                    .OrderByDescending(s => s.Date)

                    .ThenByDescending(s => s.StartTime)

                    .ToList();

                SessionCollectionView.ItemsSource = sortedSummaries;

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");

                DisplayAlert("Error", "Failed to load session history.", "OK");

            }

        }

    }

}

