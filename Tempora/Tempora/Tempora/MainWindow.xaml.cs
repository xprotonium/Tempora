using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.UI;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Foundation.Metadata;

namespace Tempora
{
    public sealed partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;
        private int _focusDuration;
        private bool _hasSessionStarted = false;
        private bool _sessionCompleted = false;
        public int FocusDuration
        {
            get => _focusDuration;
            set
            {
                _focusDuration = value;
                // e.g. format as minutes:seconds
                DispatcherQueue.TryEnqueue(() =>
                {
                    timer.Text = $"{_focusDuration:D2}:00";
                });
            }
        }
        public int BreakDuration
        {
            get; set;
        }
        public int NumberOfBreaks
        {
            get; set;
        }

        public MainWindow()
        {
            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;

            // Retreive the current app window
            AppWindow appWindow = this.AppWindow;

            if (appWindow != null && appWindow.Presenter is OverlappedPresenter presenter)
            {
                // Set the intial size of the window
                appWindow.Resize(new SizeInt32(350, 350));

                // Set the minimum and maximum preferred size of the window
                presenter.PreferredMinimumHeight = 350;
                presenter.PreferredMinimumWidth = 350;
                presenter.PreferredMaximumHeight = 650;
                presenter.PreferredMaximumWidth = 650;

                // Disable the option to maximise the window
                presenter.IsMaximizable = false;
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue("focusTime", out var f) && f is double fd)
                FocusDuration = (int)fd;
            if (values.TryGetValue("breakDuration", out var b) && b is double bd)
                BreakDuration = (int)bd;
            if (values.TryGetValue("breakCount", out var c) && c is double cc)
                NumberOfBreaks = (int)cc;

            // Load theme settings
            if (values.TryGetValue("theme", out var themeRaw) && themeRaw is int savedTheme)
            {
                if (Content is FrameworkElement root)
                {
                    root.RequestedTheme = (ElementTheme)savedTheme;
                }
            }

            // Load backdrop settings
            if (values.TryGetValue("backdrop", out var backdropRaw) && backdropRaw is string savedBackdrop)
            {
                if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
                {
                    var kind = savedBackdrop == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base;
                    SystemBackdrop = new MicaBackdrop() { Kind = kind };
                }
            }

            SetupTimer(FocusDuration, BreakDuration, NumberOfBreaks);
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Elapsed;
            ((FrameworkElement)Content).Loaded += (_, _) => ResetSession();
        }

        // TIMER LOGIC
        private DispatcherTimer _timer;
        private TimeSpan _timeLeft;
        private bool _isInFocus;
        private int _breaksLeft;

        private void SetupTimer(int focusMins, int breakMins, int breaks)
        {
            FocusDuration = focusMins;
        }

        private void StartSession()
        {
            _isInFocus = true;
            _breaksLeft = NumberOfBreaks;
            _timeLeft = TimeSpan.FromMinutes(FocusDuration);
            UpdateDisplay(_timeLeft);
            _timer.Start();
            _hasSessionStarted = true;
            UpdateBreakIndicators();
        }

        public void ResetSession()
        {
            _timer.Stop();
            _isInFocus = true;
            _breaksLeft = NumberOfBreaks;
            _timeLeft = TimeSpan.FromMinutes(FocusDuration);
            UpdateDisplay(_timeLeft);
            _hasSessionStarted = false;
        }

        private void Timer_Elapsed(object? sender, object e)
        {
            if (_timeLeft.TotalSeconds > 0)
            {
                _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
                UpdateDisplay(_timeLeft);
                return;
            }

            _timer.Stop();

            if (_isInFocus)
            {
                _isInFocus = false;
                _timeLeft = TimeSpan.FromMinutes(BreakDuration);
                _breaksLeft--;

                if (_hasSessionStarted)
                    ShowToast("Break Time", "Take a short break!");

                UpdateBreakIndicators();
            }
            else if (_breaksLeft > 0)
            {
                _isInFocus = true;
                _timeLeft = TimeSpan.FromMinutes(FocusDuration);
                ShowToast("Focus Time", "Back to work!");
            }
            else
            {
                ShowToast("Session Complete", "You've finished all focus sessions.");
                _sessionCompleted = true;
                return;
            }

            UpdateDisplay(_timeLeft);
            _timer.Start();
        }


        private void UpdateBreakIndicators()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var accentColor = (Color)Application.Current.Resources["SystemAccentColor"];
                var accentBrush = new SolidColorBrush(accentColor);

                breakIndicatorPanel.Children.Clear();

                for (int i = 0; i < NumberOfBreaks; i++)
                {
                    var ellipse = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Stroke = accentBrush,
                        StrokeThickness = 1,
                        Margin = new Thickness(2)
                    };

                    if (i < (NumberOfBreaks - _breaksLeft))
                    {
                        ellipse.Fill = accentBrush;
                    }

                    breakIndicatorPanel.Children.Add(ellipse);
                }
            });
        }



        public void ClearBreakIndicators()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                breakIndicatorPanel.Children.Clear();
            });
        }

        private void ShowToast(string title, string body)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var toast = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body)
                    .BuildNotification();

                AppNotificationManager.Default.Show(toast); // COM-bound, needs UI thread
            });
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSessionStarted)
            {
                StartSession();
            }
            else if (_sessionCompleted)
            {
                ResetSession();
                StartSession();
            }
            else
            {
                _timer.Start();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            ResetSession();
            breakIndicatorPanel.Children.Clear();
            _hasSessionStarted = false;
        }

        private void UpdateDisplay(TimeSpan ts)
        {
            // Because Timer_Elapsed runs on a worker thread, marshal back to UI:
            this.DispatcherQueue.TryEnqueue(() =>
            {
                // Ensure the timer does not reset after 60 minutes
                int totalMinutes = (int)ts.TotalMinutes;
                int seconds = ts.Seconds;

                // Format the timer as hours:minutes:seconds if it exceeds 60 minutes
                if (totalMinutes >= 60)
                {
                    int hours = totalMinutes / 60;
                    int minutes = totalMinutes % 60;
                    timer.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                }
                else
                {
                    timer.Text = ts.ToString(@"mm\:ss");
                }
            });
        }

        // SETTINGS WINDOW

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;
                _settingsWindow.Activate();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private void Button_PointerEntered(object sender, RoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "PointerOver");
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "Normal");
        }
    }
}
