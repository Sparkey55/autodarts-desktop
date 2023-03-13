using autodarts_desktop.control;
using autodarts_desktop.model;
using autodarts_desktop.Properties;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using Avalonia.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Avalonia.Media;
using System.Runtime.InteropServices;

namespace autodarts_desktop
{
    public partial class MainWindow : Window
    {

        // ATTRIBUTES

        private ProfileManager profileManager;
        private Profile? selectedProfile;
        private List<Control> selectedProfileElements;

        private double fontSize;
        private int elementWidth;
        private HorizontalAlignment elementHoAl;



        private Task ShowDialog(string title,
                                string message,
                                Window parentWindow,
                                Icon icon = MessageBox.Avalonia.Enums.Icon.Info,
                                DispatcherPriority priority = DispatcherPriority.Normal)
        {
            return Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var messageBoxParams = new MessageBoxStandardParams()
                {
                    CanResize = false,
                    ContentTitle = title,
                    ContentMessage = message,
                    Icon = icon,
                    ButtonDefinitions = ButtonEnum.Ok,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                    //Style = GetDialogStyle()
                };
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandardWindow(messageBoxParams);
                await messageBoxStandardWindow.Show(parentWindow);

            }, priority);
        }

        //private static Style GetDialogStyle()
        //{
        //    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return Style.UbuntuLinux;
        //    return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Style.MacOs : Style.Windows;
        //}


        public MainWindow()
        {
            InitializeComponent();

            fontSize = 18.0;
            elementWidth = (int)(Width * 0.80);
            elementHoAl = HorizontalAlignment.Left;

            Comboboxportal.Width = elementWidth;
            Comboboxportal.FontSize = fontSize;
            Comboboxportal.HorizontalAlignment = elementHoAl;

            SelectProfile.FontSize = fontSize - 4;

            selectedProfileElements = new();
            CheckBoxStartProfileOnProgramStart.IsChecked = Settings.Default.start_profile_on_start;
            CheckBoxStartProfileOnProgramStart.FontSize = fontSize - 6;

            try
            {
                profileManager = new ProfileManager();
                profileManager.AppDownloadStarted += ProfileManager_AppDownloadStarted;
                profileManager.AppDownloadFinished += ProfileManager_AppDownloadFinished;
                profileManager.AppDownloadFailed += ProfileManager_AppDownloadFailed;
                profileManager.AppDownloadProgressed += ProfileManager_AppDownloadProgressed;
                profileManager.AppInstallStarted += ProfileManager_AppInstallStarted;
                profileManager.AppInstallFinished += ProfileManager_AppInstallFinished;
                profileManager.AppInstallFailed += ProfileManager_AppInstallFailed;
                profileManager.AppConfigurationRequired += ProfileManager_AppConfigurationRequired;

                profileManager.LoadAppsAndProfiles();

                RenderProfiles();

                Updater.NewReleaseFound += Updater_NewReleaseFound;
                Updater.ReleaseInstallInitialized += Updater_ReleaseInstallInitialized;
                Updater.ReleaseDownloadStarted += Updater_ReleaseDownloadStarted;
                Updater.ReleaseDownloadFailed += Updater_ReleaseDownloadFailed;
                Updater.ReleaseDownloadProgressed += Updater_ReleaseDownloadProgressed;
                Updater.CheckNewVersion();
            }
            catch (ConfigurationException ex)
            {
                var msBoxStandardWindow = MessageBoxManager
                .GetMessageBoxStandardWindow(new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.YesNo,
                    ContentTitle = "Configuration Error",
                    ContentMessage = "$Configuration - file '{ex.File}' not readable.You can fix it by yourself or let it go to hell and I recreate it for you.Do you want me to reset it ? (All of your settings will be lost)"
                });

                // TODO
                //msBoxStandardWindow.ShowDialog(this)
                
                //if (await ShowDialog("Configuration Error", "ok", this) == ButtonResult.Yes)
                //{
                //    try
                //    {
                //        profileManager.DeleteConfigurationFile(ex.File);
                //    }
                //    catch (Exception e)
                //    {
                //        MessageBoxManager.GetMessageBoxStandardWindow("Error", "Configuration-file-deletion failed. Please delete it by yourself. " + e.Message).Show();
                //    }
                //}
                MessageBoxManager.GetMessageBoxStandardWindow("Restart", "Please restart the application.").Show();
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                MessageBoxManager.GetMessageBoxStandardWindow("Restart", "Something went wrong: " + ex.Message).Show();
                Environment.Exit(1);
            }
        }


        private void Buttonstart_Click(object sender, RoutedEventArgs e)
        {
            RunSelectedProfile();
        }

        private void Buttonabout_Click(object sender, RoutedEventArgs e)
        {
            //IsVisible = false;
            // TODO
            new About().ShowDialog(this);
            IsVisible = true;
        }

        private void Comboboxportal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectedProfile != null) selectedProfile.IsTaggedForStart = false;
            selectedProfile = ((ComboBoxItem)Comboboxportal.SelectedItem).Tag as Profile;
            if (selectedProfile == null) return;
            selectedProfile.IsTaggedForStart = true;
            RenderProfile();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.start_profile_on_start = (bool)CheckBoxStartProfileOnProgramStart.IsChecked;
            Settings.Default.Save();

            try
            {
                profileManager.StoreApps();
                profileManager.CloseApps();
            }
            catch (Exception ex)
            {
                MessageBoxManager.GetMessageBoxStandardWindow("Error", "Error occured: " + ex.Message).Show();
            }
        }

        private void WaitingText_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            WaitingText.IsVisible = false;
        }



        private async void Updater_NewReleaseFound(object? sender, ReleaseEventArgs e)
        {
            var questionBox = MessageBoxManager
            .GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                ContentTitle = "New Version",
                ContentMessage = $"New Version '{e.Version}' available! Do you want to update?"
            });

            // TODO
            if (await questionBox.Show(this) == ButtonResult.Yes)
            {
                try
                {
                    Updater.UpdateToNewVersion();
                }
                catch (Exception ex)
                {   
                    // TODO
                    await MessageBoxManager.GetMessageBoxStandardWindow("Error", "Update to new version failed: " + ex.Message).Show();
                }
            }
        }

        private void Updater_ReleaseDownloadStarted(object? sender, ReleaseEventArgs e)
        {
            SetWait(true, "Downloading " + e.Version + "..");
        }

        private void Updater_ReleaseDownloadFailed(object? sender, ReleaseEventArgs e)
        {
            Hide();
            MessageBoxManager.GetMessageBoxStandardWindow("Error", "Checking for new release failed! Please check your internet-connection and try again. " + e.Message).Show();
            Close();
            return;
        }

        private void Updater_ReleaseDownloadProgressed(object? sender, DownloadProgressChangedEventArgs e)
        {
            SetWait(true);
        }

        private void Updater_ReleaseInstallInitialized(object? sender, ReleaseEventArgs e)
        {
            Close();
        }



        private void ProfileManager_AppDownloadStarted(object? sender, AppEventArgs e)
        {
            SetWait(true, "Downloading " + e.App.Name + "..");
        }

        private void ProfileManager_AppDownloadFinished(object? sender, AppEventArgs e)
        {
            SetWait(false);
        }

        private void ProfileManager_AppDownloadFailed(object? sender, AppEventArgs e)
        {
            SetWait(false, "Download " + e.App.Name + " failed. Please check your internet-connection and try again. " + e.Message);
        }

        private void ProfileManager_AppDownloadProgressed(object? sender, DownloadProgressChangedEventArgs e)
        {
            SetWait(true);
        }

        private void ProfileManager_AppInstallStarted(object? sender, AppEventArgs e)
        {
            SetWait(true, "Installing " + e.App.Name + "..");
        }

        private void ProfileManager_AppInstallFinished(object? sender, AppEventArgs e)
        {
            SetWait(false);
        }

        private void ProfileManager_AppInstallFailed(object? sender, AppEventArgs e)
        {
            SetWait(false, "Install " + e.App.Name + " failed. " + e.Message);
        }

        private void ProfileManager_AppConfigurationRequired(object? sender, AppEventArgs e)
        {
            MessageBoxManager.GetMessageBoxStandardWindow("Configuration Required", e.Message).Show();
            new SettingsWindow(profileManager, e.App).ShowDialog(this);
            RunSelectedProfile();
        }


        private void RunSelectedProfile()
        {
            try
            {
                scroller.ScrollToHome();
                SetWait(true, "Starting profile ..");
                if (ProfileManager.RunProfile(selectedProfile)) WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                MessageBoxManager.GetMessageBoxStandardWindow("Error", "An error ocurred: " + ex.Message).Show();
            }
            finally
            {
                SetWait(false);
            }
        }

        private void SetWait(bool wait, string waitingText = "")
        {
            string waitingMessage = String.IsNullOrEmpty(waitingText) ? WaitingText.Text : waitingText;

            if (wait)
            {
                Opacity = 0.5;
                GridMain.IsEnabled = false;
                Waiting.IsVisible = true;
                WaitingText.IsVisible = true;
            }
            else
            {
                Opacity = 1.0;
                GridMain.IsEnabled = true;
                Waiting.IsVisible = false;
                WaitingText.IsVisible = !String.IsNullOrEmpty(waitingText);
            }
            WaitingText.Text = waitingMessage;
        }

        private void RenderProfiles()
        {
            // TODO
            //Comboboxportal.Items.Clear();
            ComboBoxItem lastItemTaggedForStart = null;
            var profiles = profileManager.GetProfiles();
            if (profiles.Count == 0)
            {
                MessageBoxManager.GetMessageBoxStandardWindow("Error", "No profiles available.").Show();
                Environment.Exit(1);
            }

            var cbiProfiles = new List<ComboBoxItem>();
            foreach (var profile in profiles)
            {
                var comboBoxItem = new ComboBoxItem();
                comboBoxItem.Content = profile.Name;
                comboBoxItem.Tag = profile;
                cbiProfiles.Add(comboBoxItem);

                if (profile.IsTaggedForStart) lastItemTaggedForStart = comboBoxItem;
            }
            Comboboxportal.Items = cbiProfiles;
            if (lastItemTaggedForStart != null) Comboboxportal.SelectedItem = lastItemTaggedForStart;
            RenderProfile();

            if (Settings.Default.start_profile_on_start) RunSelectedProfile();
        }

        private void RenderProfile()
        {
            if (selectedProfile == null) return;

            foreach (var e in selectedProfileElements) GridMain.Children.Remove(e);
            selectedProfileElements.Clear();

            var startMargin = Comboboxportal.Margin;
            int top = 30;
            int counter = 1;

            foreach (var app in selectedProfile.Apps.OrderByDescending(a => a.Value.TaggedForStart))
            {
                var marginTop = counter * top + 10;
                selectedProfile.Apps.TryGetValue(app.Key, out ProfileState? appProfile);
                var nextMargin = new Thickness(startMargin.Left, startMargin.Top + marginTop, startMargin.Right, startMargin.Bottom);

                var imageConfiguration = new Image();
                imageConfiguration.HorizontalAlignment = HorizontalAlignment.Left;
                imageConfiguration.Width = 18;
                imageConfiguration.Height = 18;
                // TODO
                //imageConfiguration.Source = "/Assets/configuration.png";

                var buttonConfiguration = new Button();
                buttonConfiguration.Margin = new Thickness(nextMargin.Left, nextMargin.Top + 5, nextMargin.Right, nextMargin.Bottom);
                //buttonConfiguration.Style = (Style)GridMain.Resources["BtnStyle"];
                buttonConfiguration.Content = imageConfiguration;
                buttonConfiguration.HorizontalAlignment = HorizontalAlignment.Left;
                buttonConfiguration.VerticalAlignment = VerticalAlignment.Top;
                buttonConfiguration.VerticalContentAlignment = VerticalAlignment.Center;
                buttonConfiguration.FontSize = fontSize;
                //buttonConfiguration.Background = Brushes.Transparent;
                buttonConfiguration.BorderThickness = new Thickness(0);
                buttonConfiguration.IsEnabled = appProfile.App.IsConfigurable() || appProfile.App.IsInstallable();

                buttonConfiguration.Click += async (s, e) =>
                {
                    IsVisible = false;
                    // TODO
                    await new SettingsWindow(profileManager, app.Value.App).ShowDialog(this);
                    scroller.ScrollToHome();
                    IsVisible = true;
                };
                GridMain.Children.Add(buttonConfiguration);
                selectedProfileElements.Add(buttonConfiguration);

                var checkBoxTagger = new CheckBox();
                checkBoxTagger.Margin = new Thickness(nextMargin.Left + 25, nextMargin.Top + 3, nextMargin.Right, nextMargin.Bottom);
                checkBoxTagger.Content = appProfile.App.Name;
                checkBoxTagger.HorizontalAlignment = HorizontalAlignment.Left;
                checkBoxTagger.VerticalAlignment = VerticalAlignment.Top;
                checkBoxTagger.VerticalContentAlignment = VerticalAlignment.Center;
                checkBoxTagger.DataContext = appProfile;
                checkBoxTagger.FontSize = fontSize;

                //checkBoxTagger.SetBinding(CheckBox.IsCheckedProperty, new Binding("TaggedForStart"));
                checkBoxTagger.IsEnabled = !appProfile.IsRequired;
                checkBoxTagger.Foreground = appProfile.TaggedForStart ? Brushes.White : Brushes.Gray;
                checkBoxTagger.FontWeight = appProfile.TaggedForStart ? FontWeight.Bold : FontWeight.Normal;
                checkBoxTagger.Checked += (s, e) =>
                {
                    checkBoxTagger.Foreground = Brushes.White;
                    checkBoxTagger.FontWeight = FontWeight.Bold;
                };
                checkBoxTagger.Unchecked += (s, e) =>
                {
                    checkBoxTagger.Foreground = Brushes.Gray;
                    checkBoxTagger.FontWeight = FontWeight.Normal;
                };
                // TODO
                //if (!String.IsNullOrEmpty(appProfile.App.DescriptionShort)) checkBoxTagger.ToolTip = appProfile.App.DescriptionShort;
                GridMain.Children.Add(checkBoxTagger);
                selectedProfileElements.Add(checkBoxTagger);

                counter += 1;
            }


        }



    }
}
