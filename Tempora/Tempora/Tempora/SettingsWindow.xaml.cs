using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Storage;
using Windows.Foundation.Metadata;

namespace Tempora
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly FrameworkElement? _root;

        public SettingsWindow()
        {
            this.InitializeComponent();

            // Retreive the current app window
            AppWindow appWindow = this.AppWindow;

            // Initialize the presenter properties
            if (appWindow != null && appWindow.Presenter is OverlappedPresenter presenter)
            {
                // Set the intial size of the window
                appWindow.Resize(new SizeInt32(800, 750));

                // Set the minimum and maximum preferred size of the window
                presenter.PreferredMinimumHeight = 600;
                presenter.PreferredMinimumWidth = 800;
            }

            // Extend the content to the title bar
            this.ExtendsContentIntoTitleBar = true;

            // Apply Mica Backdrop
            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
            {
                SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
            }

            _root = (FrameworkElement)Content;

            // 2) On open, apply whatever was saved last time
            var local = ApplicationData.Current.LocalSettings.Values;
            if (local.TryGetValue("theme", out var themeRaw) && themeRaw is int saved)
            {
                _root.RequestedTheme = (ElementTheme)saved;
            }

            // 3) Pre-select the ComboBox to match that theme
            if (themeSelector.SelectedIndex < 0)
            {
                themeSelector.SelectedItem = _root.RequestedTheme switch
                {
                    ElementTheme.Light => "Light",
                    ElementTheme.Dark => "Dark",
                    _ => "Use system setting"
                };
            }

            if (local.TryGetValue("backdrop", out var backdropRaw) && backdropRaw is string savedPick)
            {
                // create and assign the correct backdrop
                var kind = savedPick == "MicaAlt"
                    ? MicaKind.BaseAlt
                    : MicaKind.Base;
                if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
                {
                    SystemBackdrop = new MicaBackdrop() { Kind = kind };
                }
            }
            else
            {
                // default to plain Mica
                if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
                {
                    SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
                }
            }

            // PRE-SELECT the ComboBox to match
            if (backdropSelector.SelectedIndex < 0)
            {
                backdropSelector.SelectedItem = local.TryGetValue("backdrop", out var v)
                    ? (string)v
                    : "Mica";
            }

            focusTime.Value = ReadIntSetting("focusTime", 25);
            breakTime.Value = ReadIntSetting("breakDuration", 5);
            numberOfBreaks.Value = ReadIntSetting("breakCount", 5);

            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            versionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

        }

        private int ReadIntSetting(string key, int fallback)
        {
            var local = ApplicationData.Current.LocalSettings.Values;
            return local.TryGetValue(key, out var val) && val is double d
                ? (int)d
                : fallback;
        }

        private void ThemeSelector_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the default theme based on the current system theme
            if (themeSelector.SelectedIndex < 0)
            {
                var root = (FrameworkElement)Content;
                themeSelector.SelectedItem = root.RequestedTheme switch
                {
                    ElementTheme.Light => "Light",
                    ElementTheme.Dark => "Dark",
                    _ => "Use system setting"
                };
            }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (themeSelector.SelectedItem is not string pick)
                return;

            // 4a) Figure out the new ElementTheme
            var newTheme = pick switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                "Use system setting" => ElementTheme.Default,
                _ => ElementTheme.Default
            };

            // 4b) Apply it to this window's root
            if (_root != null)
            {
                _root.RequestedTheme = newTheme;
            }

            // 4c) Also apply it to your MainWindow
            if (App.MainWindowInstance?.Content is FrameworkElement mainRoot)
            {
                mainRoot.RequestedTheme = newTheme;
            }

            // 4d) Persist for next launch
            var local = ApplicationData.Current.LocalSettings.Values;
            if (newTheme == ElementTheme.Default)
            {
                local.Remove("theme");
            }
            else
            {
                local["theme"] = (int)newTheme;
            }
        }

        private void BackdropSelector_Loaded(object sender, RoutedEventArgs e)
        {
            if (backdropSelector.SelectedIndex < 0)
                return;
        }

        private void BackdropSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (backdropSelector.SelectedItem is not string pick)
                return;

            // 1) choose MicaKind
            var kind = pick == "MicaAlt"
                ? MicaKind.BaseAlt
                : MicaKind.Base;

            // 2) apply to this window
            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
            {
                SystemBackdrop = new MicaBackdrop() { Kind = kind };
            }

            // 3) also apply to MainWindow if it's open
            if (App.MainWindowInstance is Window main && main.SystemBackdrop is MicaBackdrop)
            {
                if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
                {
                    main.SystemBackdrop = new MicaBackdrop() { Kind = kind };
                }
            }

            // 4) persist choice
            var local = ApplicationData.Current.LocalSettings.Values;
            local["backdrop"] = pick;
        }

        // TIMER RELATED SETTINGS
        private void FocusTime_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["focusTime"] = e.NewValue;
            if (App.MainWindowInstance is MainWindow main)
            {
                main.FocusDuration = (int)e.NewValue;
                main.ResetSession();
                main.ClearBreakIndicators();
            }
        }

        private void BreakTime_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["breakDuration"] = e.NewValue;
            if (App.MainWindowInstance is MainWindow main)
            {
                main.BreakDuration = (int)e.NewValue;
                main.ResetSession();
                main.ClearBreakIndicators();
            }
        }

        private void NumberOfBreaks_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["breakCount"] = e.NewValue;
            if (App.MainWindowInstance is MainWindow main)
            {
                main.NumberOfBreaks = (int)e.NewValue;
                main.ResetSession();
                main.ClearBreakIndicators();
            }
        }
    }
}
