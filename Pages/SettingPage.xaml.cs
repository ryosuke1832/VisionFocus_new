using System.Collections.ObjectModel;
using VisionFocus.Services;

namespace VisionFocus
{
    /// <summary>
    /// Settings Page - User preferences management
    /// Demonstrates polymorphism through alert strategy testing
    /// </summary>
    public partial class SettingsPage : ContentPage
    {
        private SettingsModel _currentSettings;
        private ObservableCollection<string> _subjects;

        public SettingsPage()
        {
            InitializeComponent();
            _subjects = new ObservableCollection<string>();
            SubjectsCollectionView.ItemsSource = _subjects;

            LoadSettings();
            InitializeSoundPicker();
        }

        /// <summary>
        /// Load settings from file and reflect them in the UI
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

                // Sound settings
                SoundTypePicker.SelectedIndex = _currentSettings.AlertSoundType;
                VolumeSlider.Value = _currentSettings.AlertVolume;
                VolumeLabel.Text = $"{(_currentSettings.AlertVolume * 100):F0}%";
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to load settings: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Initialize sound picker with available sound types
        /// </summary>
        private void InitializeSoundPicker()
        {
            var soundNames = AlertSoundService.GetAvailableSounds();
            SoundTypePicker.ItemsSource = soundNames;

            if (_currentSettings != null)
            {
                SoundTypePicker.SelectedIndex = _currentSettings.AlertSoundType;
            }
        }

        /// <summary>
        /// Handle session duration slider value change
        /// </summary>
        private void OnSessionDurationChanged(object sender, ValueChangedEventArgs e)
        {
            int value = (int)e.NewValue;
            SessionDurationLabel.Text = value.ToString();
            _currentSettings.SessionDurationMinutes = value;
        }

        /// <summary>
        /// Handle warning threshold slider value change
        /// </summary>
        private void OnWarningThresholdChanged(object sender, ValueChangedEventArgs e)
        {
            double value = e.NewValue;
            WarningThresholdLabel.Text = value.ToString("F1");
            _currentSettings.WarningThresholdSeconds = value;

            // Ensure warning threshold is less than alert threshold
            if (value >= AlertThresholdSlider.Value)
            {
                AlertThresholdSlider.Value = value + 1;
            }
        }

        /// <summary>
        /// Handle alert threshold slider value change
        /// </summary>
        private void OnAlertThresholdChanged(object sender, ValueChangedEventArgs e)
        {
            double value = e.NewValue;
            AlertThresholdLabel.Text = value.ToString("F1");
            _currentSettings.AlertThresholdSeconds = value;

            // Ensure alert threshold is greater than warning threshold
            if (value <= WarningThresholdSlider.Value)
            {
                WarningThresholdSlider.Value = value - 1;
            }
        }

        /// <summary>
        /// Handle volume slider value change
        /// </summary>
        private void OnVolumeChanged(object sender, ValueChangedEventArgs e)
        {
            double value = e.NewValue;
            VolumeLabel.Text = $"{(value * 100):F0}%";
            _currentSettings.AlertVolume = value;
        }

        /// <summary>
        /// Handle sound type selection change
        /// </summary>
        private void OnSoundTypeChanged(object sender, EventArgs e)
        {
            if (SoundTypePicker.SelectedIndex >= 0)
            {
                _currentSettings.AlertSoundType = SoundTypePicker.SelectedIndex;
            }
        }

        /// <summary>
        /// Handle test sound button click
        /// DEMONSTRATES POLYMORPHISM: Uses base class type but executes derived class methods
        /// </summary>
        private void OnTestSoundClicked(object sender, EventArgs e)
        {
            try
            {
                int soundIndex = SoundTypePicker.SelectedIndex;
                double volume = VolumeSlider.Value;

                // POLYMORPHISM DEMONSTRATION:
                // Step 1: Get sound type from index
                var soundType = AlertSoundService.GetSoundTypeFromIndex(soundIndex);

                // Step 2: Create alert strategy (Factory Pattern)
                // Returns base class type, but creates derived class instance
                AlertStrategyBase alertStrategy = AlertSoundService.CreateAlertStrategy(soundType);

                // Step 3: Set volume property
                alertStrategy.Volume = volume;

                // Step 4: Polymorphic call - Play() method
                // Although alertStrategy is declared as base class type,
                // the actual Play() method from the derived class will be executed
                // This is runtime polymorphism (dynamic binding)
                AlertSoundService.PlaySound(alertStrategy);

                // Step 5: Polymorphic call - GetDescription() method
                // Similarly, GetDescription() from the derived class is called
                string description = alertStrategy.GetDescription();
                System.Diagnostics.Debug.WriteLine(
                    $"Playing: {description} at {volume * 100:F0}%"
                );

                // Alternative: Using method overloading (also demonstrates polymorphism)
                // AlertSoundService.PlaySampleSound(alertStrategy);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to play sound: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Handle add subject button click
        /// </summary>
        private void OnAddSubjectClicked(object sender, EventArgs e)
        {
            string newSubject = NewSubjectEntry.Text?.Trim();

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

            // Update settings model
            _currentSettings.Subjects = _subjects.ToList();
        }

        /// <summary>
        /// Handle delete subject button click
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

                        // Update settings model
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
        /// Handle save settings button click
        /// </summary>
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                // Update subjects list
                _currentSettings.Subjects = _subjects.ToList();

                // Save settings to file
                SettingsService.SaveSettings(_currentSettings);

                await DisplayAlert("Success", "Settings saved successfully!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Handle reset button click
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
        /// Handle back button click
        /// Check for unsaved changes before navigating away
        /// </summary>
        private async void OnBackClicked(object sender, EventArgs e)
        {
            // Build current settings from UI state
            var currentFromUI = new SettingsModel
            {
                SessionDurationMinutes = (int)SessionDurationSlider.Value,
                Subjects = _subjects.ToList(),
                AlertThresholdSeconds = AlertThresholdSlider.Value,
                WarningThresholdSeconds = WarningThresholdSlider.Value,
                AlertSoundType = SoundTypePicker.SelectedIndex,
                AlertVolume = VolumeSlider.Value
            };

            var saved = SettingsService.LoadSettings();

            // Check for changes
            bool hasChanges = currentFromUI.SessionDurationMinutes != saved.SessionDurationMinutes ||
                            currentFromUI.AlertThresholdSeconds != saved.AlertThresholdSeconds ||
                            currentFromUI.WarningThresholdSeconds != saved.WarningThresholdSeconds ||
                            currentFromUI.AlertSoundType != saved.AlertSoundType ||
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