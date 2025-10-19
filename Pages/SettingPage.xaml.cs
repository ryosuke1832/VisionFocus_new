using System.Collections.ObjectModel;
using VisionFocus.Core.Models;

namespace VisionFocus
{
    public partial class SettingsPage : ContentPage
    {
        private SettingsModel _currentSettings = SettingsModel.GetDefault();
        private ObservableCollection<string> _subjects;

        public SettingsPage()
        {
            InitializeComponent();
            _subjects = new ObservableCollection<string>();
            SubjectsCollectionView.ItemsSource = _subjects;

            LoadSettings();
        }

        /// <summary>
        /// Load settings and reflect them in the UI
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                _currentSettings = SettingsService.LoadSettings();

                // Session duration
                SessionDurationSlider.Value = _currentSettings.SessionDurationMinutes;
                SessionDurationLabel.Text = _currentSettings.SessionDurationMinutes.ToString();

                // Subjects
                _subjects.Clear();
                foreach (var subject in _currentSettings.Subjects)
                {
                    _subjects.Add(subject);
                }

                // Alert settings
                WarningThresholdSlider.Value = _currentSettings.WarningThresholdSeconds;
                WarningThresholdLabel.Text = _currentSettings.WarningThresholdSeconds.ToString("F1");

                AlertThresholdSlider.Value = _currentSettings.AlertThresholdSeconds;
                AlertThresholdLabel.Text = _currentSettings.AlertThresholdSeconds.ToString("F1");

                // Sound settings (volume only)
                VolumeSlider.Value = _currentSettings.AlertVolume;
                VolumeLabel.Text = $"{(_currentSettings.AlertVolume * 100):F0}%";
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to load settings: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Session duration slider value changed event
        /// </summary>
        private void OnSessionDurationChanged(object sender, ValueChangedEventArgs e)
        {
            int value = (int)e.NewValue;
            SessionDurationLabel.Text = value.ToString();
            _currentSettings.SessionDurationMinutes = value;
        }

        /// <summary>
        /// Warning threshold slider value changed event
        /// </summary>
        private void OnWarningThresholdChanged(object sender, ValueChangedEventArgs e)
        {
            double value = e.NewValue;
            WarningThresholdLabel.Text = value.ToString("F1");
            _currentSettings.WarningThresholdSeconds = value;

            // Adjust so that warning time is not greater than alert time
            if (value >= AlertThresholdSlider.Value)
            {
                AlertThresholdSlider.Value = value + 1;
            }
        }

        /// <summary>
        /// Alert threshold slider value changed event
        /// </summary>
        private void OnAlertThresholdChanged(object sender, ValueChangedEventArgs e)
        {
            double value = e.NewValue;
            AlertThresholdLabel.Text = value.ToString("F1");
            _currentSettings.AlertThresholdSeconds = value;

            // Adjust so that alert time is not less than warning time
            if (value <= WarningThresholdSlider.Value)
            {
                WarningThresholdSlider.Value = value - 1;
            }
        }

        /// <summary>
        /// Volume slider value changed event
        /// </summary>
        private void OnVolumeChanged(object sender, ValueChangedEventArgs e)
        {
            double value = e.NewValue;
            VolumeLabel.Text = $"{(value * 100):F0}%";
            _currentSettings.AlertVolume = value;
        }

        /// <summary>
        /// Test sound button click event
        /// </summary>
        private void OnTestSoundClicked(object sender, EventArgs e)
        {
            try
            {
                double volume = VolumeSlider.Value;
                // Play simple beep sound
                AlertSoundService.PlaySound(volume);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to play sound: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Add subject button click event
        /// </summary>
        private void OnAddSubjectClicked(object sender, EventArgs e)
        {
            string? newSubject = NewSubjectEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(newSubject))
            {
                DisplayAlert("Error", "Please enter a subject name", "OK");
                return;
            }

            if (_subjects.Contains(newSubject))
            {
                DisplayAlert("Error", "This subject already exists", "OK");
                return;
            }

            _subjects.Add(newSubject);
            NewSubjectEntry.Text = string.Empty;

            // Add to settings model as well
            _currentSettings.Subjects = _subjects.ToList();
        }

        /// <summary>
        /// Delete subject button click event
        /// </summary>
        private async void OnDeleteSubjectClicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is Button button && button.CommandParameter is string subject)
                {
                    bool answer = await DisplayAlert(
                        "Confirm",
                        $"Delete '{subject}'?",
                        "Delete",
                        "Cancel"
                    );

                    if (answer)
                    {
                        _subjects.Remove(subject);

                        // Remove from settings model as well
                        _currentSettings.Subjects = _subjects.ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Save settings button click event
        /// </summary>
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                // Update subjects list
                _currentSettings.Subjects = _subjects.ToList();

                // Save settings
                SettingsService.SaveSettings(_currentSettings);

                await DisplayAlert("Success", "Settings saved successfully!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Reset button click event
        /// </summary>
        private async void OnResetClicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert(
                "Confirm",
                "Reset all settings to default values?",
                "Reset",
                "Cancel"
            );

            if (answer)
            {
                _currentSettings = SettingsModel.GetDefault();
                LoadSettings();
                await DisplayAlert("Success", "Settings reset to default", "OK");
            }
        }

        /// <summary>
        /// Back button click event
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            // Check if there are unsaved changes
            var currentFromUI = new SettingsModel
            {
                SessionDurationMinutes = (int)SessionDurationSlider.Value,
                Subjects = _subjects.ToList(),
                AlertThresholdSeconds = AlertThresholdSlider.Value,
                WarningThresholdSeconds = WarningThresholdSlider.Value,
                AlertVolume = VolumeSlider.Value
            };

            var saved = SettingsService.LoadSettings();

            // Simple change check
            bool hasChanges = currentFromUI.SessionDurationMinutes != saved.SessionDurationMinutes ||
                            currentFromUI.AlertThresholdSeconds != saved.AlertThresholdSeconds ||
                            currentFromUI.WarningThresholdSeconds != saved.WarningThresholdSeconds ||
                            Math.Abs(currentFromUI.AlertVolume - saved.AlertVolume) > 0.01 ||
                            !currentFromUI.Subjects.SequenceEqual(saved.Subjects);

            if (hasChanges)
            {
                bool answer = await DisplayAlert(
                    "Unsaved Changes",
                    "You have unsaved changes. Do you want to save before leaving?",
                    "Save",
                    "Discard"
                );

                if (answer)
                {
                    SettingsService.SaveSettings(currentFromUI);
                }
            }

            await Shell.Current.GoToAsync("..");
        }
    }
}