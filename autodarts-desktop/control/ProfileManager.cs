﻿using autodarts_desktop.model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using File = System.IO.File;
using Path = System.IO.Path;


namespace autodarts_desktop.control
{

    /// <summary>
    /// Manages everything around apps-lifecycle.
    /// </summary>
    public class ProfileManager
    {

        // ATTRIBUTES

        private readonly string appsDownloadableFile = "apps-downloadable.json";
        private readonly string appsInstallableFile = "apps-installable.json";
        private readonly string appsLocalFile = "apps-local.json";
        private readonly string appsOpenFile = "apps-open.json";
        private readonly string profilesFile = "profiles.json";

        public event EventHandler<AppEventArgs>? AppDownloadStarted;
        public event EventHandler<AppEventArgs>? AppDownloadFinished;
        public event EventHandler<AppEventArgs>? AppDownloadFailed;
        public event EventHandler<DownloadProgressChangedEventArgs>? AppDownloadProgressed;

        public event EventHandler<AppEventArgs>? AppInstallStarted;
        public event EventHandler<AppEventArgs>? AppInstallFinished;
        public event EventHandler<AppEventArgs>? AppInstallFailed;
        public event EventHandler<AppEventArgs>? AppConfigurationRequired;

        private List<AppBase> AppsAll;
        private List<AppDownloadable> AppsDownloadable;
        private List<AppInstallable> AppsInstallable;
        private List<AppLocal> AppsLocal;
        private List<AppOpen> AppsOpen;
        private List<Profile> Profiles;





        // METHODS

        public ProfileManager()
        {
            var basePath = Helper.GetAppBasePath();
            appsDownloadableFile = Path.Combine(basePath, appsDownloadableFile);
            appsInstallableFile = Path.Combine(basePath, appsInstallableFile);
            appsLocalFile = Path.Combine(basePath, appsLocalFile);
            appsOpenFile = Path.Combine(basePath, appsOpenFile);
            profilesFile = Path.Combine(basePath, profilesFile);
        }



        public void LoadAppsAndProfiles()
        {
            AppsAll = new();
            AppsDownloadable = new();
            AppsInstallable = new();
            AppsLocal = new();
            AppsOpen = new();

            Profiles = new();

            if (File.Exists(appsDownloadableFile))
            {
                try
                {
                    var appsDownloadable = JsonConvert.DeserializeObject<List<AppDownloadable>>(File.ReadAllText(appsDownloadableFile));
                    AppsDownloadable.AddRange(appsDownloadable);
                    MigrateAppsDownloadable();
                    AppsAll.AddRange(AppsDownloadable);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsDownloadableFile, ex.Message);
                }
            }
            else
            {
                CreateDummyAppsDownloadable();
            }

            if (File.Exists(appsInstallableFile))
            {
                try
                {
                    var appsInstallable = JsonConvert.DeserializeObject<List<AppInstallable>>(File.ReadAllText(appsInstallableFile));
                    AppsInstallable.AddRange(appsInstallable);
                    MigrateAppsInstallable();
                    AppsAll.AddRange(AppsInstallable);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsInstallableFile, ex.Message);
                }
            }
            else
            {
                CreateDummyAppsInstallable();
            }


            if (File.Exists(appsLocalFile))
            {
                try
                {
                    var appsLocal = JsonConvert.DeserializeObject<List<AppLocal>>(File.ReadAllText(appsLocalFile));
                    AppsLocal.AddRange(appsLocal);
                    MigrateAppsLocal();
                    AppsAll.AddRange(AppsLocal);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsLocalFile, ex.Message);
                }
            }
            else
            {
                CreateDummyAppsLocal();
            }

            if (File.Exists(appsOpenFile))
            {
                try
                {
                    var appsOpen = JsonConvert.DeserializeObject<List<AppOpen>>(File.ReadAllText(appsOpenFile));
                    AppsOpen.AddRange(appsOpen);
                    MigrateAppsOpen();
                    AppsAll.AddRange(AppsOpen);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsOpenFile, ex.Message);
                }
            }
            else
            {
                CreateDummyAppsOpen();
            }


            if (File.Exists(profilesFile))
            {
                try
                {
                    Profiles = JsonConvert.DeserializeObject<List<Profile>>(File.ReadAllText(profilesFile));
                    MigrateProfiles();
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(profilesFile, ex.Message);
                }
            }
            else
            {
                CreateDummyProfiles();
            }

            // Sets apps`s custom-name to app-name if custom-name is empty
            foreach (var a in AppsAll)
            {
                if (String.IsNullOrEmpty(a.CustomName))
                {
                    a.CustomName = a.Name;
                }
            }
            
            
            foreach (var profile in Profiles)
            {
                foreach(KeyValuePair<string, ProfileState> profileLink in profile.Apps)
                {
                    var appFound = false;
                    foreach(var app in AppsAll)
                    {
                        if(app.Name == profileLink.Key)
                        {
                            appFound = true;
                            profileLink.Value.SetApp(app);
                            break;
                        }
                    }
                    if (!appFound) throw new Exception($"Profile-App '{profileLink.Key}' not found");
                }
            }


            foreach (var appDownloadable in AppsDownloadable)
            {
                appDownloadable.DownloadStarted += AppDownloadable_DownloadStarted;
                appDownloadable.DownloadFinished += AppDownloadable_DownloadFinished;
                appDownloadable.DownloadFailed += AppDownloadable_DownloadFailed;
                appDownloadable.DownloadProgressed += AppDownloadable_DownloadProgressed;
                appDownloadable.AppConfigurationRequired += App_AppConfigurationRequired;
            }
            foreach (var appInstallable in AppsInstallable)
            {
                appInstallable.DownloadStarted += AppDownloadable_DownloadStarted;
                appInstallable.DownloadFinished += AppDownloadable_DownloadFinished;
                appInstallable.DownloadFailed += AppDownloadable_DownloadFailed;
                appInstallable.DownloadProgressed += AppDownloadable_DownloadProgressed;
                appInstallable.InstallStarted += AppInstallable_InstallStarted;
                appInstallable.InstallFinished += AppInstallable_InstallFinished;
                appInstallable.InstallFailed += AppInstallable_InstallFailed;
                appInstallable.AppConfigurationRequired += App_AppConfigurationRequired;
            }
            foreach (var appLocal in AppsLocal)
            {
                appLocal.AppConfigurationRequired += App_AppConfigurationRequired;
            }
            foreach (var appOpen in AppsOpen)
            {
                appOpen.AppConfigurationRequired += App_AppConfigurationRequired;
            }
        }

        public void StoreApps()
        {
            SerializeApps(AppsDownloadable, appsDownloadableFile);
            SerializeApps(AppsInstallable, appsInstallableFile);
            SerializeApps(AppsLocal, appsLocalFile);
            SerializeApps(AppsOpen, appsOpenFile);
            SerializeProfiles(Profiles, profilesFile);
        }

        public void DeleteConfigurationFile(string configurationFile)
        {
            File.Delete(configurationFile);
        }

        public static bool RunProfile(Profile? profile)
        {
            if (profile == null) return false;

            var allAppsRunning = true;
            var appsTaggedForStart = profile.Apps.Where(x => x.Value.TaggedForStart).OrderBy(x => x.Value.App.CustomName);
            foreach (KeyValuePair<string, ProfileState> app in appsTaggedForStart)
            {
                // as here is no catch, apps-run stops when there is an error
                if (!app.Value.App.Run(app.Value.RuntimeArguments)) allAppsRunning = false;
            }
            return allAppsRunning;
        }

        public void CloseApps()
        {
            foreach (var app in AppsAll)
            {
                try
                {
                    app.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Closing failed for app: {app.Name} - {ex.Message}");
                }
            }
        }

        public List<Profile> GetProfiles()
        {
            return Profiles;
        }




        private void CreateDummyAppsLocal()
        {
            List<AppLocal> apps = new();

            AppLocal custom1 =
               new(
                   name: "custom-1",
                   descriptionShort: "Starts a program on your file-system"
                   );

            AppLocal custom2 =
                new(
                    name: "custom-2",
                    descriptionShort: "Starts a program on your file-system"
                    );

            AppLocal custom3 =
                   new(
                       name: "custom-3",
                       descriptionShort: "Starts a program on your file-system"
                       );

            apps.Add(custom1);
            apps.Add(custom2);
            apps.Add(custom3);

            AppsLocal.AddRange(apps);
            AppsAll.AddRange(apps);
            SerializeApps(apps, appsLocalFile);
        }

        private void MigrateAppsLocal()
        {
            // 1. Mig (Update customName), adds custom-2, custom-3
            var custom = AppsLocal.Find(a => a.Name == "custom");
            if (custom != null)
            {
                custom.Name = "custom-1";
            }

            var custom2 = AppsLocal.FindIndex(a => a.Name == "custom-2");
            if (custom2 == -1)
            {
                AppLocal custom2Create =
                    new(
                      name: "custom-2",
                      descriptionShort: "Starts a program on your file-system"
                      );

                AppsLocal.Add(custom2Create);
            }

            var custom3 = AppsLocal.FindIndex(a => a.Name == "custom-3");
            if (custom3 == -1)
            {
                AppLocal custom3Create =
                    new(
                      name: "custom-3",
                      descriptionShort: "Starts a program on your file-system"
                      );

                AppsLocal.Add(custom3Create);
            }


            // Add more migs..
        }


        private void CreateDummyAppsOpen()
        {
            List<AppOpen> apps = new();

            AppOpen autodartsWeb =
                new(
                    name: "autodarts.io",
                    descriptionShort: "Opens autodart`s web-platform",
                    defaultValue: "https://play.autodarts.io"
                    );
            AppOpen autodartsBoardManager =
                new(
                    name: "autodarts-boardmanager",
                    descriptionShort: "Opens autodart`s board-manager",
                    defaultValue: "http://127.0.0.1:3180"
                    );

            AppOpen customUrl1 =
                new(
                    name: "custom-url-1",
                    descriptionShort: "Opens an url"
                    );

            AppOpen customUrl2 =
                new(
                    name: "custom-url-2",
                    descriptionShort: "Opens an url"
                    );

            AppOpen customUrl3 =
                    new(
                        name: "custom-url-3",
                        descriptionShort: "Opens an url"
                        );

            apps.Add(autodartsWeb);
            apps.Add(autodartsBoardManager);
            apps.Add(customUrl1);
            apps.Add(customUrl2);
            apps.Add(customUrl3);

            AppsOpen.AddRange(apps);
            AppsAll.AddRange(apps);
            SerializeApps(apps, appsOpenFile);
        }

        private void MigrateAppsOpen()
        {
            var autodartsBoardManager = AppsOpen.FindIndex(a => a.Name == "autodarts-boardmanager");
            if (autodartsBoardManager == -1)
            {
                AppOpen autodartsBoardManagerCreate =
                                               new(
                                                   name: "autodarts-boardmanager",
                                                   descriptionShort: "Opens autodart`s board-manager",
                                                   defaultValue: "http://127.0.0.1:3180"
                                                   );

                AppsOpen.Add(autodartsBoardManagerCreate);
            }


            var customUrl1 = AppsOpen.FindIndex(a => a.Name == "custom-url-1");
            if (customUrl1 == -1)
            {
                AppOpen customUrl1Create =
                                        new(
                                            name: "custom-url-1",
                                            descriptionShort: "Opens an url"
                                            );

                AppsOpen.Add(customUrl1Create);
            }

            var customUrl2 = AppsOpen.FindIndex(a => a.Name == "custom-url-2");
            if (customUrl2 == -1)
            {
                AppOpen customUrl2Create =
                                        new(
                                            name: "custom-url-2",
                                            descriptionShort: "Opens an url"
                                            );

                AppsOpen.Add(customUrl2Create);
            }

            var customUrl3 = AppsOpen.FindIndex(a => a.Name == "custom-url-3");
            if (customUrl3 == -1)
            {
                AppOpen customUrl3Create =
                                        new(
                                            name: "custom-url-3",
                                            descriptionShort: "Opens an url"
                                            );

                AppsOpen.Add(customUrl3Create);
            }


            foreach(var a in AppsOpen)
            {
                a.DescriptionShort = "Opens a file or url";
                var file = a.Configuration.Arguments.Find(arg => arg.Name == "file");
                if(file != null)
                {
                    file.NameHuman = "file/url";
                }
            }


            // Add more migs..
        }


        private void CreateDummyAppsInstallable()
        {
            // Define Download-Maps for Apps with os
            var dartboardsClientDownloadMap = new DownloadMap();
            dartboardsClientDownloadMap.WindowsX64 = "https://dartboards.online/dboclient_***VERSION***.exe";
            //dartboardsClientDownloadMap.MacX64 = "https://dartboards.online/dboclient_***VERSION***.dmg";
            var dartboardsClientDownloadUrl = dartboardsClientDownloadMap.GetDownloadUrlByOs("0.9.2");

            var droidCamDownloadMap = new DownloadMap();
            droidCamDownloadMap.WindowsX64 = "https://github.com/dev47apps/windows-releases/releases/download/win-***VERSION***/DroidCam.Setup.***VERSION***.exe";
            var droidCamDownloadUrl = droidCamDownloadMap.GetDownloadUrlByOs("6.5.2");

            var epocCamDownloadMap = new DownloadMap();
            epocCamDownloadMap.WindowsX64 = "https://edge.elgato.com/egc/windows/epoccam/EpocCam_Installer64_***VERSION***.exe";
            //epocCamDownloadMap.MacX64 = "https://edge.elgato.com/egc/macos/epoccam/EpocCam_Installer_***VERSION***.pkg";
            var epocCamDownloadUrl = epocCamDownloadMap.GetDownloadUrlByOs("3_4_0");



            List <AppInstallable> apps = new();

            if(dartboardsClientDownloadUrl != null)
            {
                AppInstallable dartboardsClient =
                new(
                    downloadUrl: dartboardsClientDownloadUrl,
                    name: "dartboards-client",
                    helpUrl: "https://dartboards.online/client",
                    descriptionShort: "Connects webcam to dartboards.online",
                    executable: "dartboardsonlineclient.exe",
                    defaultPathExecutable: Path.Join(Helper.GetUserDirectoryPath(), @"AppData\Local\Programs\dartboardsonlineclient"),
                    startsAfterInstallation: true
                    );
                apps.Add(dartboardsClient);
            }

            if (droidCamDownloadUrl != null)
            {
                AppInstallable droidCam =
                new(
                    downloadUrl: droidCamDownloadUrl,
                    name: "droid-cam",
                    helpUrl: "https://www.dev47apps.com",
                    descriptionShort: "Connects to your android phone- or tablet-camera",
                    defaultPathExecutable: @"C:\Program Files (x86)\DroidCam",
                    executable: "DroidCamApp.exe",
                    runAsAdminInstall: true,
                    startsAfterInstallation: false
                    );
                apps.Add(droidCam);
            }

            if (epocCamDownloadUrl != null)
            {
                AppInstallable epocCam =
                new(
                    downloadUrl: epocCamDownloadUrl,
                    name: "epoc-cam",
                    helpUrl: "https://www.elgato.com/de/epoccam",
                    descriptionShort: "Connects to your iOS phone- or tablet-camera",
                    defaultPathExecutable: @"C:\Program Files (x86)\Elgato\EpocCam",
                    // epoccamtray.exe
                    executable: "EpocCamService.exe",
                    runAsAdminInstall: false,
                    startsAfterInstallation: false,
                    isService: true
                    );
                apps.Add(epocCam);
            }

            AppsInstallable.AddRange(apps);
            AppsAll.AddRange(apps);
            SerializeApps(apps, appsInstallableFile);
        }

        private void MigrateAppsInstallable()
        {
            // Define Download-Maps for Apps with os
            var dartboardsClientDownloadMap = new DownloadMap();
            dartboardsClientDownloadMap.WindowsX64 = "https://dartboards.online/dboclient_***VERSION***.exe";
            //dartboardsClientDownloadMap.MacX64 = "https://dartboards.online/dboclient_***VERSION***.dmg";
            var dartboardsClientDownloadUrl = dartboardsClientDownloadMap.GetDownloadUrlByOs("0.9.2");

            var droidCamDownloadMap = new DownloadMap();
            droidCamDownloadMap.WindowsX64 = "https://github.com/dev47apps/windows-releases/releases/download/win-***VERSION***/DroidCam.Setup.***VERSION***.exe";
            var droidCamDownloadUrl = droidCamDownloadMap.GetDownloadUrlByOs("6.5.2");

            var epocCamDownloadMap = new DownloadMap();
            epocCamDownloadMap.WindowsX64 = "https://edge.elgato.com/egc/windows/epoccam/EpocCam_Installer64_***VERSION***.exe";
            //epocCamDownloadMap.MacX64 = "https://edge.elgato.com/egc/macos/epoccam/EpocCam_Installer_***VERSION***.pkg";
            var epocCamDownloadUrl = epocCamDownloadMap.GetDownloadUrlByOs("3_4_0");




            var dartboardsClient = AppsInstallable.Find(a => a.Name == "dartboards-client");
            if (dartboardsClient != null)
            {
                if (dartboardsClientDownloadUrl != null)
                {
                    dartboardsClient.DownloadUrl = dartboardsClientDownloadUrl;
                    dartboardsClient.DescriptionShort = "Connects webcam to dartboards.online";
                }
                else
                {
                    var dartboardsClientIndex = AppsInstallable.FindIndex(a => a.Name == "dartboards-client");
                    if (dartboardsClientIndex != -1)
                    {
                        AppsInstallable.RemoveAt(dartboardsClientIndex);
                    }
                }
            }

            var droidCam = AppsInstallable.Find(a => a.Name == "droid-cam");
            if (droidCam != null)
            {
                if (droidCamDownloadUrl != null)
                {
                    droidCam.DownloadUrl = droidCamDownloadUrl;
                    droidCam.DescriptionShort = "Connects to your android phone- or tablet-camera";
                }
                else
                {
                    var droidCamIndex = AppsInstallable.FindIndex(a => a.Name == "droid-cam");
                    if (droidCamIndex != -1)
                    {
                        AppsInstallable.RemoveAt(droidCamIndex);
                    }
                }
            }

            var epocCam = AppsInstallable.Find(a => a.Name == "epoc-cam");
            if (epocCam != null)
            {
                if (epocCamDownloadUrl != null)
                {
                    epocCam.DownloadUrl = epocCamDownloadUrl;
                    epocCam.DescriptionShort = "Connects to your iOS phone- or tablet-camera";
                }
                else
                {
                    var epocCamIndex = AppsInstallable.FindIndex(a => a.Name == "epoc-cam");
                    if (epocCamIndex != -1)
                    {
                        AppsInstallable.RemoveAt(epocCamIndex);
                    }
                }
            }

            // make all apps chmod-able
            foreach (var a in AppsInstallable) a.Chmod = true;


            // Add more migs..
        }


        private void CreateDummyAppsDownloadable()
        {
            // Define os-specific download-Maps for each app
            var autodartsClientDownloadMap = new DownloadMap();
            autodartsClientDownloadMap.WindowsX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.windows-amd64.zip";
            autodartsClientDownloadMap.LinuxX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-amd64.tar.gz";
            autodartsClientDownloadMap.LinuxArm64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-arm64.tar.gz";
            autodartsClientDownloadMap.LinuxArm = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-armv7l.tar.gz"; 
            autodartsClientDownloadMap.MacX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.darwin-amd64.tar.gz";
            autodartsClientDownloadMap.MacArm64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.darwin-arm64.tar.gz";
            var autodartsClientDownloadUrl = autodartsClientDownloadMap.GetDownloadUrlByOs("0.22.0");

            var autodartsCallerDownloadMap = new DownloadMap();
            autodartsCallerDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller.exe";
            autodartsCallerDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller";
            autodartsCallerDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller-arm64";
            //autodartsCallerDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller-arm";
            autodartsCallerDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller-mac";
            var autodartsCallerDownloadUrl = autodartsCallerDownloadMap.GetDownloadUrlByOs("2.8.2");

            var autodartsExternDownloadMap = new DownloadMap();
            autodartsExternDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern.exe";
            autodartsExternDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern";
            //autodartsExternDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern-arm64";
            //autodartsExternDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern-arm";
            autodartsExternDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern-mac";
            var autodartsExternDownloadUrl = autodartsExternDownloadMap.GetDownloadUrlByOs("1.5.8");

            var autodartsWledDownloadMap = new DownloadMap();
            autodartsWledDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled.exe";
            autodartsWledDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled";
            autodartsWledDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled-arm64";
            //autodartsWledDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled-arm";
            autodartsWledDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled-mac";
            var autodartsWledDownloadUrl = autodartsWledDownloadMap.GetDownloadUrlByOs("1.4.10");

            var virtualDartsZoomDownloadMap = new DownloadMap();
            virtualDartsZoomDownloadMap.WindowsX64 = "https://www.lehmann-bo.de/Downloads/VDZ/Virtual Darts Zoom.zip";
            var virtualDartsZoomDownloadUrl = virtualDartsZoomDownloadMap.GetDownloadUrlByOs();

            var autodartsGifDownloadMap = new DownloadMap();
            autodartsGifDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif.exe";
            autodartsGifDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif";
            autodartsGifDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif-arm64";
            //autodartsGifDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif-arm";
            autodartsGifDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif-mac";
            var autodartsGifDownloadUrl = autodartsGifDownloadMap.GetDownloadUrlByOs("1.0.6");

            var autodartsVoiceDownloadMap = new DownloadMap();
            autodartsVoiceDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice.exe";
            autodartsVoiceDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice";
            autodartsVoiceDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice-arm64";
            //autodartsVoiceDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice-arm";
            autodartsVoiceDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice-mac";
            var autodartsVoiceDownloadUrl = autodartsVoiceDownloadMap.GetDownloadUrlByOs("1.0.8");

            var camLoaderDownloadMap = new DownloadMap();
            camLoaderDownloadMap.WindowsX86 = "https://github.com/lbormann/cam-loader/releases/download/v***VERSION***/cam-loader.zip";
            camLoaderDownloadMap.WindowsX64 = "https://github.com/lbormann/cam-loader/releases/download/v***VERSION***/cam-loader.zip";
            var camLoaderDownloadUrl = camLoaderDownloadMap.GetDownloadUrlByOs("1.0.0");




            List<AppDownloadable> apps = new();

            if (!String.IsNullOrEmpty(autodartsClientDownloadUrl))
            {
                AppDownloadable autodarts =
                new(
                    downloadUrl: autodartsClientDownloadUrl,
                    name: "autodarts-client",
                    helpUrl: "https://docs.autodarts.io/",
                    descriptionShort: "Recognizes dart-positions"
                    );
                apps.Add(autodarts);
            }

            if (!String.IsNullOrEmpty(autodartsCallerDownloadUrl))
            {
                AppDownloadable autodartsCaller =
                    new(
                        downloadUrl: autodartsCallerDownloadUrl,
                        name: "autodarts-caller",
                        helpUrl: "https://github.com/lbormann/autodarts-caller",
                        descriptionShort: "Calls out thrown points",
                        configuration: new(
                            prefix: "-",
                            delimitter: " ",
                            arguments: new List<Argument> {
                            new(name: "U", type: "string", required: true, nameHuman: "-U / --autodarts_email", section: "Autodarts"),
                            new(name: "P", type: "password", required: true, nameHuman: "-P / --autodarts_password", section: "Autodarts"),
                            new(name: "B", type: "string", required: true, nameHuman: "-B / --autodarts_board_id", section: "Autodarts"),
                            new(name: "M", type: "path", required: true, nameHuman: "-M / --media_path", section: "Media"),
                            new(name: "MS", type: "path", required: false, nameHuman: "-MS / --media_path_shared", section: "Media"),
                            new(name: "V", type: "float[0.0..1.0]", required: false, nameHuman: "-V / --caller_volume", section: "Media"),
                            new(name: "C", type: "string", required: false, nameHuman: "-C / --caller", section: "Calls"),
                            new(name: "R", type: "bool", required: false, nameHuman: "-R / --random_caller", section: "Random", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "L", type: "bool", required: false, nameHuman: "-L / --random_caller_each_leg", section: "Random", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "RL", type: "int[0..6]", required: false, nameHuman: "-RL / --random_caller_language", section: "Random"),
                            new(name: "RG", type: "int[0..2]", required: false, nameHuman: "-RG / --random_caller_gender", section: "Random"),
                            new(name: "CCP", type: "bool", required: false, nameHuman: "-CCP / --call_current_player", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "CCPA", type: "bool", required: false, nameHuman: "-CCPA / --call_current_player_always", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "E", type: "bool", required: false, nameHuman: "-E / --call_every_dart", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "ESF", type: "bool", required: false, nameHuman: "-ESF / --call_every_dart_single_files", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "PCC", type: "int", required: false, nameHuman: "-PCC / --possible_checkout_call", section: "Calls"),
                            new(name: "PCCSF", type: "bool", required: false, nameHuman: "-PCCSF / --possible_checkout_call_single_files", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "PCCYO", type: "bool", required: false, nameHuman: "-PCCYO / --possible_checkout_call_yourself_only", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "A", type: "float[0.0..1.0]", required: false, nameHuman: "-A / --ambient_sounds", section: "Calls"),
                            new(name: "AAC", type: "bool", required: false, nameHuman: "-AAC / --ambient_sounds_after_calls", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "DL", type: "bool", required: false, nameHuman: "-DL / --downloads", section: "Downloads", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "DLLA", type: "int[0..6]", required: false, nameHuman: "-DLLA / --downloads_language", section: "Downloads"),
                            new(name: "DLL", type: "int", required: false, nameHuman: "-DLL / --downloads_limit", section: "Downloads"),
                            new(name: "DLN", type: "string", required: false, nameHuman: "-DLN / --downloads_name", section: "Downloads"),
                            new(name: "BLP", type: "path", required: false, nameHuman: "-BLP / --blacklist_path", section: "Media"),
                            new(name: "BAV", type: "float[0.0..1.0]", required: false, nameHuman: "-BAV / --background_audio_volume", section: "Calls"),
                            new(name: "WEB", type: "int[0..2]", required: false, nameHuman: "-WEB / --web_caller", section: "Service"),
                            new(name: "WEBSB", type: "bool", required: false, nameHuman: "-WEBSB / --web_caller_scoreboard", section: "Service", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "WEBP", type: "int", required: false, nameHuman: "-WEBP / --web_caller_port", section: "Service"),
                            new(name: "HP", type: "int", required: false, nameHuman: "-HP / --host_port", section: "Service"),
                            new(name: "DEB", type: "bool", required: false, nameHuman: "-DEB / --debug", section: "Service", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "CC", type: "bool", required: false, nameHuman: "-CC / --cert_check", section: "Service", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"})
                            })
                        );
                apps.Add(autodartsCaller);
            }

            if (!String.IsNullOrEmpty(autodartsExternDownloadUrl)) {
                AppDownloadable autodartsExtern =
                new(
                    downloadUrl: autodartsExternDownloadUrl,
                    name: "autodarts-extern",
                    helpUrl: "https://github.com/lbormann/autodarts-extern",
                    descriptionShort: "Bridges and automates other dart-platforms",
                    configuration: new(
                        prefix: "--",
                        delimitter: " ",
                        arguments: new List<Argument> {
                            new(name: "connection", type: "string", required: false, nameHuman: "--connection", section: "Service"),
                            new(name: "browser_path", type: "file", required: true, nameHuman: "--browser_path", section: "", description: "Path to browser. fav. Chrome"),
                            new(name: "autodarts_user", type: "string", required: true, nameHuman: "--autodarts_user", section: "Autodarts"),
                            new(name: "autodarts_password", type: "password", required: true, nameHuman: "--autodarts_password", section: "Autodarts"),
                            new(name: "autodarts_board_id", type: "string", required: true, nameHuman: "--autodarts_board_id", section: "Autodarts"),
                            new(name: "extern_platform", type: "selection[lidarts,nakka,dartboards]", required: true, nameHuman: "", isRuntimeArgument: true),
                            new(name: "time_before_exit", type: "int[0..150000]", required: false, nameHuman: "--time_before_exit", section: "Match"),
                            new(name: "lidarts_user", type: "string", required: false, nameHuman: "--lidarts_user", section: "Lidarts", requiredOnArgument: "extern_platform=lidarts"),
                            new(name: "lidarts_password", type: "password", required: false, nameHuman: "--lidarts_password", section: "Lidarts", requiredOnArgument: "extern_platform=lidarts"),
                            new(name: "lidarts_skip_dart_modals", type: "bool", required: false, nameHuman: "--lidarts_skip_dart_modals", section: "Lidarts"),
                            new(name: "lidarts_chat_message_start", type: "string", required: false, nameHuman: "--lidarts_chat_message_start", section: "Lidarts", value: "Hi, GD! Automated darts-scoring - powered by autodarts.io - Enter the community: https://discord.gg/bY5JYKbmvM"),
                            new(name: "lidarts_chat_message_end", type: "string", required: false, nameHuman: "--lidarts_chat_message_end", section: "Lidarts", value: "Thanks GG, WP!"),
                            new(name: "lidarts_cam_fullscreen", type: "bool", required: false, nameHuman: "--lidarts_cam_fullscreen", section: "Lidarts"),
                            new(name: "nakka_skip_dart_modals", type: "bool", required: false, nameHuman: "--nakka_skip_dart_modal", section: "Nakka"),
                            new(name: "dartboards_user", type: "string", required: false, nameHuman: "--dartboards_user", section: "Dartboards", requiredOnArgument: "extern_platform=dartboards"),
                            new(name: "dartboards_password", type: "password", required: false, nameHuman: "--dartboards_password", section: "Dartboards", requiredOnArgument: "extern_platform=dartboards"),
                            new(name: "dartboards_skip_dart_modals", type: "bool", required: false, nameHuman: "--dartboards_skip_dart_modals", section: "Dartboards"),
                        })
                );
                apps.Add(autodartsExtern);
            }

            if (!String.IsNullOrEmpty(autodartsWledDownloadUrl))
            {
                var autodartsWledArguments = new List<Argument> {
                        new(name: "CON", type: "string", required: false, nameHuman: "-CON / --connection", section: "Service"),
                        new(name: "WEPS", type: "string", required: true, isMulti: true, nameHuman: "-WEPS / --wled_endpoints", section: "WLED"),
                        new(name: "DU", type: "int[0..10]", required: false, nameHuman: "-DU / --effect_duration", section: "WLED"),
                        new(name: "BSS", type: "float[0.0..10.0]", required: false, nameHuman: "-BSS / --board_stop_start", section: "Autodarts"),
                        new(name: "BRI", type: "int[1..255]", required: false, nameHuman: "-BRI / --effect_brightness", section: "WLED"),
                        new(name: "HFO", type: "int[2..170]", required: false, nameHuman: "-HFO / --high_finish_on", section: "Autodarts"),
                        new(name: "HF", type: "string", required: false, isMulti: true, nameHuman: "-HF / --high_finish_effects", section: "WLED"),
                        new(name: "IDE", type: "string", required: false, nameHuman: "-IDE / --idle_effect", section: "WLED"),
                        new(name: "G", type: "string", required: false, isMulti: true, nameHuman: "-G / --game_won_effects", section: "WLED"),
                        new(name: "M", type: "string", required: false, isMulti : true, nameHuman: "-M / --match_won_effects", section: "WLED"),
                        new(name: "B", type: "string", required: false, isMulti : true, nameHuman: "-B / --busted_effects", section: "WLED"),
                        new(name: "DEB", type: "bool", required: false, nameHuman: "-DEB / --debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })

                    };
                for (int i = 0; i <= 180; i++)
                {
                    var score = i.ToString();
                    Argument scoreArgument = new(name: "S" + score, type: "string", required: false, isMulti: true, nameHuman: "-S" + score + " / --score_" + score + "_effects", section: "WLED");
                    autodartsWledArguments.Add(scoreArgument);
                }
                for (int i = 1; i <= 12; i++)
                {
                    var areaNumber = i.ToString();
                    Argument areaArgument = new(name: "A" + areaNumber, type: "string", required: false, isMulti: true, nameHuman: "-A" + areaNumber + " / --score_area_" + areaNumber + "_effects", section: "WLED");
                    autodartsWledArguments.Add(areaArgument);
                }

                AppDownloadable autodartsWled =
                new(
                    downloadUrl: autodartsWledDownloadUrl,
                    name: "autodarts-wled",
                    helpUrl: "https://github.com/lbormann/autodarts-wled",
                    descriptionShort: "Controls WLED installations by autodarts-events",
                    configuration: new(
                        prefix: "-",
                        delimitter: " ",
                        arguments: autodartsWledArguments)
                    );
                apps.Add(autodartsWled);
            }

            if (!String.IsNullOrEmpty(virtualDartsZoomDownloadUrl))
            {
                AppDownloadable virtualDartsZoom =
                new(
                    downloadUrl: virtualDartsZoomDownloadUrl,
                    name: "virtual-darts-zoom",
                    helpUrl: "https://lehmann-bo.de/?p=28",
                    descriptionShort: "Zooms webcam-image onto thrown darts",
                    runAsAdmin: true
                    );
                apps.Add(virtualDartsZoom);
            }

            if (!String.IsNullOrEmpty(autodartsGifDownloadUrl))
            {
                var autodartsGifArguments = new List<Argument> {
                         new(name: "MP", type: "path", required: false, nameHuman: "-MP / --media_path", section: "Media"),
                         new(name: "CON", type: "string", required: false, nameHuman: "-CON / --connection", section: "Service"),
                         new(name: "HFO", type: "int[2..170]", required: false, nameHuman: "-HFO / --high_finish_on", section: "Autodarts"),
                         new(name: "HF", type: "string", required: false, isMulti: true, nameHuman: "-HF / --high_finish_images", section: "Images"),
                         new(name: "G", type: "string", required: false, isMulti: true, nameHuman: "-G / --game_won_images", section: "Images"),
                         new(name: "M", type: "string", required: false, isMulti : true, nameHuman: "-M / --match_won_images", section: "Images"),
                         new(name: "B", type: "string", required: false, isMulti : true, nameHuman: "-B / --busted_images", section: "Images"),
                         new(name: "WEB", type: "int[0..2]", required: false, nameHuman: "-WEB / --web_gif", section: "Service"),
                         new(name: "DEB", type: "bool", required: false, nameHuman: "-DEB / --debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })

                     };
                for (int i = 0; i <= 180; i++)
                {
                    var score = i.ToString();
                    Argument scoreArgument = new(name: "S" + score, type: "string", required: false, isMulti: true, nameHuman: "-S" + score + " / --score_" + score + "_images", section: "Images");
                    autodartsGifArguments.Add(scoreArgument);
                }
                for (int i = 1; i <= 12; i++)
                {
                    var areaNumber = i.ToString();
                    Argument areaArgument = new(name: "A" + areaNumber, type: "string", required: false, isMulti: true, nameHuman: "-A" + areaNumber + " / --score_area_" + areaNumber + "_images", section: "Images");
                    autodartsGifArguments.Add(areaArgument);
                }

                AppDownloadable autodartsGif =
                new(
                    downloadUrl: autodartsGifDownloadUrl,
                    name: "autodarts-gif",
                    helpUrl: "https://github.com/lbormann/autodarts-gif",
                    descriptionShort: "Displays images according to autodarts-events",
                    configuration: new(
                        prefix: "-",
                        delimitter: " ",
                        arguments: autodartsGifArguments)
                    );
                apps.Add(autodartsGif);
            }

            if (!String.IsNullOrEmpty(autodartsVoiceDownloadUrl))
            {
                var autodartsVoiceArguments = new List<Argument> {
                        new(name: "CON", type: "string", required: false, nameHuman: "-CON / --connection", section: "Service"),
                        new(name: "MP", type: "path", required: true, nameHuman: "-MP / --model_path", section: "Voice-Recognition"),
                        new(name: "L", type: "int[0..2]", required: false, nameHuman: "-L / --language", section: "Voice-Recognition"),
                        new(name: "KNG", type: "string", required: false, isMulti: true, nameHuman: "-KNG / --keywords_next_game", section: "Voice-Recognition"),
                        new(name: "KN", type: "string", required: false, isMulti: true, nameHuman: "-KN / --keywords_next", section: "Voice-Recognition"),
                        new(name: "KU", type: "string", required: false, isMulti: true, nameHuman: "-KU / --keywords_undo", section: "Voice-Recognition"),
                        new(name: "KBC", type: "string", required: false, isMulti: true, nameHuman: "-KBC / --keywords_ban_caller", section: "Voice-Recognition"),
                        new(name: "KCC", type: "string", required: false, isMulti: true, nameHuman: "-KCC / --keywords_change_caller", section: "Voice-Recognition"),
                        new(name: "KSB", type: "string", required: false, isMulti: true, nameHuman: "-KSB / --keywords_start_board", section: "Voice-Recognition"),
                        new(name: "KSPB", type: "string", required: false, isMulti: true, nameHuman: "-KSPB / --keywords_stop_board", section: "Voice-Recognition"),
                        new(name: "KRB", type: "string", required: false, isMulti: true, nameHuman: "-KRB / --keywords_reset_board", section: "Voice-Recognition"),
                        new(name: "KCB", type: "string", required: false, isMulti: true, nameHuman: "-KCB / --keywords_calibrate_board", section: "Voice-Recognition"),
                        new(name: "KFD", type: "string", required: false, isMulti: true, nameHuman: "-KFD / --keywords_first_dart", section: "Voice-Recognition"),
                        new(name: "KSD", type: "string", required: false, isMulti: true, nameHuman: "-KSD / --keywords_second_dart", section: "Voice-Recognition"),
                        new(name: "KTD", type: "string", required: false, isMulti: true, nameHuman: "-KTD / --keywords_third_dart", section: "Voice-Recognition"),
                        new(name: "KS", type: "string", required: false, isMulti: true, nameHuman: "-KS / --keywords_single", section: "Voice-Recognition"),
                        new(name: "KD", type: "string", required: false, isMulti: true, nameHuman: "-KD / --keywords_double", section: "Voice-Recognition"),
                        new(name: "KT", type: "string", required: false, isMulti: true, nameHuman: "-KT / --keywords_triple", section: "Voice-Recognition"),
                        new(name: "KZERO", type: "string", required: false, isMulti: true, nameHuman: "-KZERO / --keywords_zero", section: "Voice-Recognition"),
                        new(name: "KONE", type: "string", required: false, isMulti: true, nameHuman: "-KONE / --keywords_one", section: "Voice-Recognition"),
                        new(name: "KTWO", type: "string", required: false, isMulti: true, nameHuman: "-KTWO / --keywords_two", section: "Voice-Recognition"),
                        new(name: "KTHREE", type: "string", required: false, isMulti: true, nameHuman: "-KTHREE / --keywords_three", section: "Voice-Recognition"),
                        new(name: "KFOUR", type: "string", required: false, isMulti: true, nameHuman: "-KFOUR / --keywords_four", section: "Voice-Recognition"),
                        new(name: "KFIVE", type: "string", required: false, isMulti: true, nameHuman: "-KFIVE / --keywords_five", section: "Voice-Recognition"),
                        new(name: "KSIX", type: "string", required: false, isMulti: true, nameHuman: "-KSIX / --keywords_six", section: "Voice-Recognition"),
                        new(name: "KSEVEN", type: "string", required: false, isMulti: true, nameHuman: "-KSEVEN / --keywords_seven", section: "Voice-Recognition"),
                        new(name: "KEIGHT", type: "string", required: false, isMulti: true, nameHuman: "-KEIGHT / --keywords_eight", section: "Voice-Recognition"),
                        new(name: "KNINE", type: "string", required: false, isMulti: true, nameHuman: "-KNINE / --keywords_nine", section: "Voice-Recognition"),
                        new(name: "KTEN", type: "string", required: false, isMulti: true, nameHuman: "-KTEN / --keywords_ten", section: "Voice-Recognition"),
                        new(name: "KELEVEN", type: "string", required: false, isMulti: true, nameHuman: "-KELEVEN / --keywords_eleven", section: "Voice-Recognition"),
                        new(name: "KTWELVE", type: "string", required: false, isMulti: true, nameHuman: "-KTWELVE / --keywords_twelve", section: "Voice-Recognition"),
                        new(name: "KTHIRTEEN", type: "string", required: false, isMulti: true, nameHuman: "-KTHIRTEEN / --keywords_thirteen", section: "Voice-Recognition"),
                        new(name: "KFOURTEEN", type: "string", required: false, isMulti: true, nameHuman: "-KFOURTEEN / --keywords_fourteen", section: "Voice-Recognition"),
                        new(name: "KFIFTEEN", type: "string", required: false, isMulti: true, nameHuman: "-KFIFTEEN / --keywords_fifteen", section: "Voice-Recognition"),
                        new(name: "KSIXTEEN", type: "string", required: false, isMulti: true, nameHuman: "-KSIXTEEN / --keywords_sixteen", section: "Voice-Recognition"),
                        new(name: "KSEVENTEEN", type: "string", required: false, isMulti: true, nameHuman: "-KSEVENTEEN / --keywords_seventeen", section: "Voice-Recognition"),
                        new(name: "KEIGHTEEN", type: "string", required: false, isMulti: true, nameHuman: "-KEIGHTEEN / --keywords_eighteen", section: "Voice-Recognition"),
                        new(name: "KNINETEEN", type: "string", required: false, isMulti: true, nameHuman: "-KNINETEEN / --keywords_nineteen", section: "Voice-Recognition"),
                        new(name: "KTWENTY", type: "string", required: false, isMulti: true, nameHuman: "-KTWENTY / --keywords_twenty", section: "Voice-Recognition"),
                        new(name: "KTWENTYFIVE", type: "string", required: false, isMulti: true, nameHuman: "-KTWENTY_FIVE / --keywords_twenty_five", section: "Voice-Recognition"),
                        new(name: "KFIFTY", type: "string", required: false, isMulti: true, nameHuman: "-KFIFTY / --keywords_fifty", section: "Voice-Recognition"),
                        new(name: "DEB", type: "bool", required: false, nameHuman: "-DEB / --debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })
                    };

                AppDownloadable autodartsVoice =
                new(
                    downloadUrl: autodartsVoiceDownloadUrl,
                    name: "autodarts-voice",
                    helpUrl: "https://github.com/lbormann/autodarts-voice",
                    descriptionShort: "Controls autodarts by using your voice",
                    configuration: new(
                        prefix: "-",
                        delimitter: " ",
                        arguments: autodartsVoiceArguments)
                    );
                apps.Add(autodartsVoice);
            }

            if (!String.IsNullOrEmpty(camLoaderDownloadUrl))
            {
                AppDownloadable camLoader =
                new(
                    downloadUrl: camLoaderDownloadUrl,
                    name: "cam-loader",
                    helpUrl: "https://github.com/lbormann/cam-loader",
                    descriptionShort: "Saves and loads settings for multiple cameras"
                    );
                apps.Add(camLoader);
            }



            AppsDownloadable.AddRange(apps);
            AppsAll.AddRange(apps);
            
            SerializeApps(apps, appsDownloadableFile);
        }

        private void MigrateAppsDownloadable()
        {
            // !!! DO NOT TOUCH THIS ANYMORE !!!!
            var autodartsCaller = AppsDownloadable.Find(a => a.Name == "autodarts-caller");
            if (autodartsCaller != null)
            {
                // 2. Mig (Add ValueMapping for bool)
                foreach (var arg in autodartsCaller.Configuration.Arguments)
                {
                    switch (arg.Name)
                    {
                        case "R":
                        case "L":
                        case "E":
                        case "PCC":
                            arg.ValueMapping = new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" };
                            break;
                    }
                }

                // 3. Mig (Set default values)
                var wtt = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "WTT");
                if (wtt != null && String.IsNullOrEmpty(wtt.Value)) wtt.Value = "http://localhost:8080/throw";

                // 5. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.3.3/autodarts-caller.exe";

                // 6. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.3.5/autodarts-caller.exe";

                // 7. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.3.6/autodarts-caller.exe";

                // 8. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.3.7/autodarts-caller.exe";

                // 10. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.3.8/autodarts-caller.exe";

                // 11. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.4.0/autodarts-caller.exe";

                // 12. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.5.0/autodarts-caller.exe";

                // 13. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.5.1/autodarts-caller.exe";

                // 16. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.5.2/autodarts-caller.exe";

                // 17. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.5.3/autodarts-caller.exe";

                // 18. Mig (Adjust WTT Argument)
                var wtt2 = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "WTT");
                if (wtt2 != null && !String.IsNullOrEmpty(wtt2.Value)) wtt2.Value = wtt2.Value.Replace("throw", "");
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.5.4/autodarts-caller.exe";

                // 19. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.5.5/autodarts-caller.exe";

                // 20. Mig (WTT is multi)
                var wtt3 = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "WTT");
                if (wtt3 != null) wtt3.IsMulti = true;

                // 26. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.6.0/autodarts-caller.exe";

                // 27. Mig (Update download version)
                var ambientSounds = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "A");
                if (ambientSounds == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "A", type: "bool", required: false, nameHuman: "ambient-sounds", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.6.1/autodarts-caller.exe";

                // 28. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.7.0/autodarts-caller.exe";

                // 29. Mig (Update download version)
                var ambientSounds2 = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "A");
                if (ambientSounds2 != null)
                {
                    if (ambientSounds2.Value == "True")
                    {
                        ambientSounds2.Value = "1.0";
                    }
                    else if (ambientSounds2.Value == "False")
                    {
                        ambientSounds2.Value = "0.0";
                    }
                    ambientSounds2.Type = "float[0.0..1.0]";
                    ambientSounds2.ValueMapping = null;
                    ambientSounds2.ValidateType();
                }
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.7.1/autodarts-caller.exe";

                // 32. Mig (Update download version)
                autodartsCaller.Configuration.Arguments.RemoveAll(a => a.Name == "WTT");
                var hostPort = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "HP");
                if (hostPort == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "HP", type: "int", required: false, nameHuman: "host-port", section: "Service"));
                }
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.8.1/autodarts-caller.exe";

                // 35. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v1.8.2/autodarts-caller.exe";


                // 36. Mig (Update download version)

                var ms = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "MS");
                if (ms == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "MS", type: "path", required: false, nameHuman: "path-to-shared-sound-files", section: "Media"));
                }
                var caller = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "C");
                if (caller == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "C", type: "string", required: false, nameHuman: "specific-caller", section: "Calls"));
                }
                var cpp = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "CCP");
                if (cpp == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "CCP", type: "bool", required: false, nameHuman: "call-current-player", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                var esf = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "ESF");
                if (esf == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "ESF", type: "bool", required: false, nameHuman: "call-every-dart-single-files", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                var pccsf = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "PCCSF");
                if (pccsf == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "PCCSF", type: "bool", required: false, nameHuman: "possible-checkout-call-single-files", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                var acc = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "ACC");
                if (acc == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "ACC", type: "bool", required: false, nameHuman: "ambient-sounds-after-calls", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                var dl = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DL");
                if (dl == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "DL", type: "bool", required: false, nameHuman: "downloads", section: "Downloads", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                var dll = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DLL");
                if (dll == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "DLL", type: "int[0..1000]", required: false, nameHuman: "downloads-limit", section: "Downloads"));
                }
                var dlp = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DLP");
                if (dlp == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "DLP", type: "path", required: false, nameHuman: "downloads-path", section: "Downloads"));
                }

                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.0/autodarts-caller.exe";

                // 37. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.1/autodarts-caller.exe";

                // 38. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.2/autodarts-caller.exe";

                // 39. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.3/autodarts-caller.exe";

                // 40. add bav-arg
                var bav = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "BAV");
                if (bav == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "BAV", type: "float[0.0..1.0]", required: false, nameHuman: "background-audio-volume", section: "Downloads"));
                }

                // 41. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.5/autodarts-caller.exe";

                // 42. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.6/autodarts-caller.exe";

                // 43. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.7/autodarts-caller.exe";

                // 44. Mig (Update download version)
                autodartsCaller.Configuration.Arguments.RemoveAll(a => a.Name == "ACC");
                var aac = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "AAC");
                if (aac == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "AAC", type: "bool", required: false, nameHuman: "ambient-sounds-after-calls", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.8/autodarts-caller.exe";

                // 47. Mig (Update download version)
                var deb = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DEB");
                if (deb == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }

                
                var dll2 = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DLL");
                var ddl2Value = String.Empty;
                if (dll2 != null)
                {
                    ddl2Value = dll2.Value;
                }
                autodartsCaller.Configuration.Arguments.RemoveAll(a => a.Name == "DLL");
                dll2 = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DLL");
                if (dll2 == null)
                {
                    autodartsCaller.Configuration.Arguments.Add(new(name: "DLL", type: "int", required: false, nameHuman: "downloads-limit", section: "Downloads", value: ddl2Value));
                }
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.9/autodarts-caller.exe";

                // 48. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.10/autodarts-caller.exe";

                // 51. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.11/autodarts-caller.exe";

                // 53. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.12/autodarts-caller.exe";

                // 54. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.13/autodarts-caller.exe";

                // 55. Mig (Update download version)
                autodartsCaller.DownloadUrl = "https://github.com/lbormann/autodarts-caller/releases/download/v2.0.14/autodarts-caller.exe";
            }

            var autodartsExtern = AppsDownloadable.Find(a => a.Name == "autodarts-extern");
            if (autodartsExtern != null)
            {
                // 1. Mig (Update download version)
                autodartsExtern.DownloadUrl = "https://github.com/lbormann/autodarts-extern/releases/download/v1.4.4/autodarts-extern.exe";

                // 4. Mig (Set default values)
                var hostPort = autodartsExtern.Configuration.Arguments.Find(a => a.Name == "host_port");
                if (hostPort != null && String.IsNullOrEmpty(hostPort.Value))
                {
                    hostPort.Value = "8080";
                }

                var lidartsChatMessageStart = autodartsExtern.Configuration.Arguments.Find(a => a.Name == "lidarts_chat_message_start");
                if (lidartsChatMessageStart != null && String.IsNullOrEmpty(lidartsChatMessageStart.Value))
                {
                    lidartsChatMessageStart.Value = "Hi, GD! Automated darts-scoring - powered by autodarts.io - Enter the community: https://discord.gg/bY5JYKbmvM";
                }

                var lidartsChatMessageEnd = autodartsExtern.Configuration.Arguments.Find(a => a.Name == "lidarts_chat_message_end");
                if (lidartsChatMessageEnd != null && String.IsNullOrEmpty(lidartsChatMessageEnd.Value))
                {
                    lidartsChatMessageEnd.Value = "Thanks GG, WP!";
                }

                // 14. Mig (Update download version)
                autodartsExtern.DownloadUrl = "https://github.com/lbormann/autodarts-extern/releases/download/v1.4.5/autodarts-extern.exe";

                // 25. Mig (Update download version)
                autodartsExtern.DownloadUrl = "https://github.com/lbormann/autodarts-extern/releases/download/v1.4.6/autodarts-extern.exe";

                // 27. Mig (Update download version)
                autodartsExtern.DownloadUrl = "https://github.com/lbormann/autodarts-extern/releases/download/v1.4.7/autodarts-extern.exe";

                // 33. Mig (Update download version)
                autodartsExtern.Configuration.Arguments.RemoveAll(a => a.Name == "host_port");
                var connection = autodartsExtern.Configuration.Arguments.Find(a => a.Name == "connection");
                if (connection == null)
                {
                    autodartsExtern.Configuration.Arguments.Add(new(name: "connection", type: "string", required: false, nameHuman: "Connection", section: "Service"));
                }
                autodartsExtern.DownloadUrl = "https://github.com/lbormann/autodarts-extern/releases/download/v1.5.0/autodarts-extern.exe";

                // 46. Mig (Update download version)
                autodartsExtern.DownloadUrl = "https://github.com/lbormann/autodarts-extern/releases/download/v1.5.1/autodarts-extern.exe";

                // 50. Mig (Update download version)
                autodartsExtern.DownloadUrl = "https://github.com/lbormann/autodarts-extern/releases/download/v1.5.2/autodarts-extern.exe";

            }


            // 9. Mig (Remove app)
            var autodartsBotIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-bot");
            if (autodartsBotIndex != -1)
            {
                AppsDownloadable.RemoveAt(autodartsBotIndex);
            }


            // 15. Mig (Add app)
            var autodartsWledIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-wled");
            if (autodartsWledIndex == -1)
            {
                var autodartsWledArguments = new List<Argument> {
                    new(name: "-I", type: "string", required: false, nameHuman: "host-ip", section: "App"),
                    new(name: "-P", type: "string", required: false, nameHuman: "host-port", section: "App"),
                    new(name: "WEPS", type: "string", required: true, isMulti: true, nameHuman: "wled-endpoints", section: "WLED"),
                    new(name: "HSO", type: "int[1..180]", required: false, nameHuman: "highscore-on", section: "Autodarts"),
                    new(name: "HFO", type: "int[2..170]", required: false, nameHuman: "highfinish-on", section: "Autodarts"),
                    new(name: "HS", type: "string", required: false, isMulti: true, nameHuman: "high-score-effects", section: "WLED"),
                    new(name: "HF", type: "string", required: false, isMulti: true, nameHuman: "high-finish-effects", section: "WLED"),
                    new(name: "G", type: "string", required: false, isMulti: true, nameHuman: "game-won-effects", section: "WLED"),
                    new(name: "M", type: "string", required: false, isMulti : true, nameHuman: "match-won-effects", section: "WLED"),
                    new(name: "B", type: "string", required: false, isMulti : true, nameHuman: "busted-effects", section: "WLED")
                    };
                for (int i = 0; i <= 180; i++)
                {
                    var score = i.ToString();
                    Argument scoreArgument = new(name: "S" + score, type: "string", required: false, isMulti: true, nameHuman: "score " + score, section: "WLED");
                    autodartsWledArguments.Add(scoreArgument);
                }

                AppDownloadable autodartsWledCreate =
                    new(
                        downloadUrl: "https://github.com/lbormann/autodarts-wled/releases/download/v1.2.1/autodarts-wled.exe",
                        name: "autodarts-wled",
                        helpUrl: "https://github.com/lbormann/autodarts-wled",
                        descriptionShort: "control wled installations",
                        configuration: new(
                            prefix: "-",
                            delimitter: " ",
                            arguments: autodartsWledArguments)
                        );

                AppsDownloadable.Add(autodartsWledCreate);
            }


            var autodartsWled = AppsDownloadable.Find(a => a.Name == "autodarts-wled");
            if (autodartsWled != null)
            {
                // 21. Remove HSO, HS -- Add A1-A12, BRI
                autodartsWled.Configuration.Arguments.RemoveAll(a => a.Name == "HSO");
                autodartsWled.Configuration.Arguments.RemoveAll(a => a.Name == "HS");

                var bri = autodartsWled.Configuration.Arguments.Find(a => a.Name == "BRI");
                if (bri == null)
                {
                    autodartsWled.Configuration.Arguments.Add(new(name: "BRI", type: "int[1..255]", required: false, nameHuman: "brightness-effects", section: "WLED"));
                }

                for (int i = 1; i <= 12; i++)
                {
                    var areaNumber = i.ToString();
                    var areaX = autodartsWled.Configuration.Arguments.Find(a => a.Name == "A" + areaNumber);
                    if (areaX == null)
                    {
                        Argument areaArgument = new(name: "A" + areaNumber, type: "string", required: false, isMulti: true, nameHuman: "area-" + areaNumber, section: "WLED");
                        autodartsWled.Configuration.Arguments.Add(areaArgument);
                    }
                }

                // 22. Mig (Update download version)
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.2.3/autodarts-wled.exe";


                // 23. Mig (Update download version)
                var ide = autodartsWled.Configuration.Arguments.Find(a => a.Name == "IDE");
                if (ide == null)
                {
                    autodartsWled.Configuration.Arguments.Add(new(name: "IDE", type: "string", required: false, nameHuman: "idle-effect", section: "WLED"));
                }
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.2.4/autodarts-wled.exe";

                // 24. Mig (Update download version)
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.3.0/autodarts-wled.exe";

                // 28. Mig (Update download version)
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.3.1/autodarts-wled.exe";

                // 31. Mig (Update downloiad version)
                var duration = autodartsWled.Configuration.Arguments.Find(a => a.Name == "DU");
                if (duration == null)
                {
                    autodartsWled.Configuration.Arguments.Add(new(name: "DU", type: "int[0..10]", required: false, nameHuman: "effects-duration", section: "WLED"));
                }
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.3.2/autodarts-wled.exe";

                // 34. Mig (Update download version)
                autodartsWled.Configuration.Arguments.RemoveAll(a => a.Name == "-I");
                autodartsWled.Configuration.Arguments.RemoveAll(a => a.Name == "-P");

                var weps = autodartsWled.Configuration.Arguments.Find(a => a.Name == "WEPS");
                if (weps != null && !String.IsNullOrEmpty(weps.Value)) weps.Value = weps.Value.Replace("http://", "").Replace("https://", "");

                var connection = autodartsWled.Configuration.Arguments.Find(a => a.Name == "CON");
                if (connection == null)
                {
                    autodartsWled.Configuration.Arguments.Add(new(name: "CON", type: "string", required: false, nameHuman: "Connection", section: "Service"));
                }

                var board_start_stop = autodartsWled.Configuration.Arguments.Find(a => a.Name == "BSS");
                if (board_start_stop == null)
                {
                    autodartsWled.Configuration.Arguments.Add(new(name: "BSS", type: "float[0.0..10.0]", required: false, nameHuman: "board-start-stop", section: "Autodarts"));
                }

                var board_start_stop_only_start = autodartsWled.Configuration.Arguments.Find(a => a.Name == "BSSOS");
                if (board_start_stop_only_start == null)
                {
                    autodartsWled.Configuration.Arguments.Add(new(name: "BSSOS", type: "bool", required: false, nameHuman: "board-start-stop-only-start", section: "Autodarts", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }

                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.4.1/autodarts-wled.exe";


                // 45. Mig (Update download version)
                autodartsWled.Configuration.Arguments.RemoveAll(a => a.Name == "BSSOS");
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.4.2/autodarts-wled.exe";

                // 49. Mig (Update download version)
                var deb = autodartsWled.Configuration.Arguments.Find(a => a.Name == "DEB");
                if (deb == null)
                {
                    autodartsWled.Configuration.Arguments.Add(new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                }
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.4.3/autodarts-wled.exe";

                // 52. Mig (Update download version)
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.4.4/autodarts-wled.exe";

                // 56. Mig (Update download version)
                autodartsWled.DownloadUrl = "https://github.com/lbormann/autodarts-wled/releases/download/v1.4.5/autodarts-wled.exe";
            }

            // 55. Mig (Update download version)
            var autodartsClient = AppsDownloadable.Find(a => a.Name == "autodarts-client");
            if (autodartsClient != null)
            {
                autodartsClient.DownloadUrl = "https://github.com/autodarts/releases/releases/download/v0.18.0-rc1/autodarts0.18.0-rc1.windows-amd64.zip";
                autodartsClient.HelpUrl = "https://docs.autodarts.io/";


                // 57. Mig (Update download version)
                autodartsClient.DownloadUrl = "https://github.com/autodarts/releases/releases/download/v0.18.0/autodarts0.18.0.windows-amd64.zip";

                // 58. Mig (Update download version)
                autodartsClient.DownloadUrl = "https://github.com/autodarts/releases/releases/download/v0.18.1/autodarts0.18.1.windows-amd64.zip";
            }


            // Add more migs..


            // Go further here!
            MigrateAppsDownloadableSinceCrossPlatform();
        }

        private void MigrateAppsDownloadableSinceCrossPlatform()
        {
            // Define os-specific download-Maps for each app
            var autodartsClientDownloadMap = new DownloadMap();
            autodartsClientDownloadMap.WindowsX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.windows-amd64.zip";
            autodartsClientDownloadMap.LinuxX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-amd64.tar.gz";
            autodartsClientDownloadMap.LinuxArm64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-arm64.tar.gz";
            autodartsClientDownloadMap.LinuxArm = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-armv7l.tar.gz";
            autodartsClientDownloadMap.MacX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.darwin-amd64.tar.gz";
            autodartsClientDownloadMap.MacArm64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.darwin-arm64.tar.gz";
            var autodartsClientDownloadUrl = autodartsClientDownloadMap.GetDownloadUrlByOs("0.22.0");

            var autodartsCallerDownloadMap = new DownloadMap();
            autodartsCallerDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller.exe";
            autodartsCallerDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller";
            autodartsCallerDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller-arm64";
            //autodartsCallerDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller-arm";
            autodartsCallerDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-caller/releases/download/v***VERSION***/autodarts-caller-mac";
            var autodartsCallerDownloadUrl = autodartsCallerDownloadMap.GetDownloadUrlByOs("2.8.2");

            var autodartsExternDownloadMap = new DownloadMap();
            autodartsExternDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern.exe";
            autodartsExternDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern";
            //autodartsExternDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern-arm64";
            //autodartsExternDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern-arm";
            autodartsExternDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern-mac";
            var autodartsExternDownloadUrl = autodartsExternDownloadMap.GetDownloadUrlByOs("1.5.8");

            var autodartsWledDownloadMap = new DownloadMap();
            autodartsWledDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled.exe";
            autodartsWledDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled";
            autodartsWledDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled-arm64";
            //autodartsWledDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled-arm";
            autodartsWledDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled-mac";
            var autodartsWledDownloadUrl = autodartsWledDownloadMap.GetDownloadUrlByOs("1.4.10");

            var virtualDartsZoomDownloadMap = new DownloadMap();
            virtualDartsZoomDownloadMap.WindowsX64 = "https://www.lehmann-bo.de/Downloads/VDZ/Virtual Darts Zoom.zip";
            var virtualDartsZoomDownloadUrl = virtualDartsZoomDownloadMap.GetDownloadUrlByOs();

            var autodartsGifDownloadMap = new DownloadMap();
            autodartsGifDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif.exe";
            autodartsGifDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif";
            autodartsGifDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif-arm64";
            //autodartsGifDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif-arm";
            autodartsGifDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif-mac";
            var autodartsGifDownloadUrl = autodartsGifDownloadMap.GetDownloadUrlByOs("1.0.6");

            var autodartsVoiceDownloadMap = new DownloadMap();
            autodartsVoiceDownloadMap.WindowsX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice.exe";
            autodartsVoiceDownloadMap.LinuxX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice";
            autodartsVoiceDownloadMap.LinuxArm64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice-arm64";
            //autodartsVoiceDownloadMap.LinuxArm = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice-arm";
            autodartsVoiceDownloadMap.MacX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice-mac";
            var autodartsVoiceDownloadUrl = autodartsVoiceDownloadMap.GetDownloadUrlByOs("1.0.8");

            var camLoaderDownloadMap = new DownloadMap();
            camLoaderDownloadMap.WindowsX86 = "https://github.com/lbormann/cam-loader/releases/download/v***VERSION***/cam-loader.zip";
            camLoaderDownloadMap.WindowsX64 = "https://github.com/lbormann/cam-loader/releases/download/v***VERSION***/cam-loader.zip";
            var camLoaderDownloadUrl = camLoaderDownloadMap.GetDownloadUrlByOs("1.0.0");




            // 1. Mig (Update download version)
            var autodartsClient = AppsDownloadable.Find(a => a.Name == "autodarts-client");
            if (autodartsClient != null)
            {
                if (autodartsClientDownloadUrl != null)
                {
                    autodartsClient.DownloadUrl = autodartsClientDownloadUrl;
                    autodartsClient.DescriptionShort = "Recognizes dart-positions";
                }
                else
                {
                    var autodartsClientIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-client");
                    if (autodartsClientIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(autodartsClientIndex);
                    }
                }
            }

            var autodartsCaller = AppsDownloadable.Find(a => a.Name == "autodarts-caller");
            if (autodartsCaller != null)
            {
                if (autodartsCallerDownloadUrl != null)
                {
                    autodartsCaller.DownloadUrl = autodartsCallerDownloadUrl;

                    var web = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "WEB");
                    if (web == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "WEB", type: "int[0..2]", required: false, nameHuman: "web-caller", section: "Service"));
                    }
                    var webPort = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "WEBP");
                    if (webPort == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "WEBP", type: "int", required: false, nameHuman: "web-caller-port", section: "Service"));
                    }
                    var randomCallerLanguage = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "RL");
                    if (randomCallerLanguage == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "RL", type: "int[0..6]", required: false, nameHuman: "random-caller-language", section: "Random"));
                    }
                    var randomCallerGender = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "RG");
                    if (randomCallerGender == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "RG", type: "int[0..2]", required: false, nameHuman: "random-caller-gender", section: "Random"));
                    }
                    var downloadsLanguage = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DLLA");
                    if (downloadsLanguage == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "DLLA", type: "int[0..6]", required: false, nameHuman: "downloads-language", section: "Downloads"));
                    }

                    var possibleCheckoutCall = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "PCC");
                    if (possibleCheckoutCall != null)
                    {
                        if (possibleCheckoutCall.Value == "True")
                        {
                            possibleCheckoutCall.Value = "1";
                        }
                        else if (possibleCheckoutCall.Value == "False")
                        {
                            possibleCheckoutCall.Value = "0";
                        }
                        possibleCheckoutCall.Type = "int";
                        possibleCheckoutCall.ValueMapping = null;
                        possibleCheckoutCall.ValidateType();
                    }

                    var possibleCheckoutCallOnlyYourself = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "PCCYO");
                    if (possibleCheckoutCallOnlyYourself == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "PCCYO", type: "bool", required: false, nameHuman: "possible-checkout-call-only-yourself", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                    }

                    var webScoreboard = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "WEBSB");
                    if (webScoreboard == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "WEBSB", type: "bool", required: false, nameHuman: "web-scoreboard", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                    }

                    autodartsCaller.Configuration.Arguments.RemoveAll(a => a.Name == "DLP");

                    var callCurrentPlayerAlways = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "CCPA");
                    if (callCurrentPlayerAlways == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "CCPA", type: "bool", required: false, nameHuman: "call-current-player-always", section: "Calls", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }));
                    }

                    var blacklistPath = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "BLP");
                    if (blacklistPath == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "BLP", type: "path", required: false, nameHuman: "-BLP / --blacklist_path", section: "Media"));
                    }

                    var downloadsName = autodartsCaller.Configuration.Arguments.Find(a => a.Name == "DLN");
                    if (downloadsName == null)
                    {
                        autodartsCaller.Configuration.Arguments.Add(new(name: "DLN", type: "string", required: false, nameHuman: "-DLN / --downloads_name", section: "Downloads"));
                    }

                    autodartsCaller.DescriptionShort = "Calls out thrown points";
                }
                else
                {
                    var autodartsCallerIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-caller");
                    if (autodartsCallerIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(autodartsCallerIndex);
                    }
                }
            }

            var autodartsExtern = AppsDownloadable.Find(a => a.Name == "autodarts-extern");
            if (autodartsExtern != null)
            {
                if (autodartsExternDownloadUrl != null)
                {
                    autodartsExtern.DownloadUrl = autodartsExternDownloadUrl;

                    var lidartsCamFullscreen = autodartsExtern.Configuration.Arguments.Find(a => a.Name == "lidarts_cam_fullscreen");
                    if (lidartsCamFullscreen == null)
                    {
                        autodartsExtern.Configuration.Arguments.Add(new(name: "lidarts_cam_fullscreen", type: "bool", required: false, nameHuman: "Camera fullscreen", section: "Lidarts"));
                    }

                    autodartsExtern.DescriptionShort = "Bridges and automates other dart-platforms";
                }
                else
                {
                    var autodartsExternIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-extern");
                    if (autodartsExternIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(autodartsExternIndex);
                    }
                }
            }

            var autodartsWled = AppsDownloadable.Find(a => a.Name == "autodarts-wled");
            if (autodartsWled != null)
            {
                if (autodartsWledDownloadUrl != null)
                {
                    autodartsWled.DownloadUrl = autodartsWledDownloadUrl;
                    autodartsWled.DescriptionShort = "Controls WLED installations by autodarts-events";
                }
                else
                {
                    var autodartsWledIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-wled");
                    if (autodartsWledIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(autodartsWledIndex);
                    }
                }
            }

            var virtualDartsZoom = AppsDownloadable.Find(a => a.Name == "virtual-darts-zoom");
            if (virtualDartsZoom != null)
            {
                if (virtualDartsZoomDownloadUrl != null)
                {
                    virtualDartsZoom.DownloadUrl = virtualDartsZoomDownloadUrl;
                    virtualDartsZoom.DescriptionShort = "Zooms webcam-image onto thrown darts";
                }
                else
                {
                    var virtualDartsZoomIndex = AppsDownloadable.FindIndex(a => a.Name == "virtual-darts-zoom");
                    if (virtualDartsZoomIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(virtualDartsZoomIndex);
                    }
                }
            }

            var autodartsGif = AppsDownloadable.Find(a => a.Name == "autodarts-gif");
            if (autodartsGif != null)
            {
                if (autodartsGifDownloadUrl != null)
                {
                    autodartsGif.DownloadUrl = autodartsGifDownloadUrl;
                    autodartsGif.DescriptionShort = "Displays images according to autodarts-events";
                }
                else
                {
                    var autodartsGifIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-gif");
                    if (autodartsGifIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(autodartsGifIndex);
                    }
                }
            }
            else if (autodartsGifDownloadUrl != null)
            {
                var autodartsGifArguments = new List<Argument> {
                         new(name: "MP", type: "path", required: false, nameHuman: "path-to-image-files", section: "Media"),
                         new(name: "CON", type: "string", required: false, nameHuman: "Connection", section: "Service"),
                         new(name: "HFO", type: "int[2..170]", required: false, nameHuman: "highfinish-on", section: "Autodarts"),
                         new(name: "HF", type: "string", required: false, isMulti: true, nameHuman: "high-finish-images", section: "Images"),
                         new(name: "G", type: "string", required: false, isMulti: true, nameHuman: "game-won-images", section: "Images"),
                         new(name: "M", type: "string", required: false, isMulti : true, nameHuman: "match-won-images", section: "Images"),
                         new(name: "B", type: "string", required: false, isMulti : true, nameHuman: "busted-images", section: "Images"),
                         new(name: "WEB", type: "int[0..2]", required: false, nameHuman: "web-gifs", section: "Service"),
                         new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })
                     };
                for (int i = 0; i <= 180; i++)
                {
                    var score = i.ToString();
                    Argument scoreArgument = new(name: "S" + score, type: "string", required: false, isMulti: true, nameHuman: "score " + score, section: "Images");
                    autodartsGifArguments.Add(scoreArgument);
                }
                for (int i = 1; i <= 12; i++)
                {
                    var areaNumber = i.ToString();
                    Argument areaArgument = new(name: "A" + areaNumber, type: "string", required: false, isMulti: true, nameHuman: "area-" + areaNumber, section: "Images");
                    autodartsGifArguments.Add(areaArgument);
                }
                
                autodartsGif =
                    new(
                        downloadUrl: autodartsGifDownloadUrl,
                        name: "autodarts-gif",
                        helpUrl: "https://github.com/lbormann/autodarts-gif",
                        descriptionShort: "Displays images according to autodarts-events",
                        configuration: new(
                            prefix: "-",
                            delimitter: " ",
                            arguments: autodartsGifArguments)
                        );
                AppsDownloadable.Add(autodartsGif);
            }

            var camLoader = AppsDownloadable.Find(a => a.Name == "cam-loader");
            if (camLoader != null)
            {
                if (camLoaderDownloadUrl != null)
                {
                    camLoader.DownloadUrl = camLoaderDownloadUrl;
                    camLoader.DescriptionShort = "Saves and loads settings for multiple cameras";
                }
                else
                {
                    var camLoaderIndex = AppsDownloadable.FindIndex(a => a.Name == "cam-loader");
                    if (camLoaderIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(camLoaderIndex);
                    }
                }
            }
            else if (camLoaderDownloadUrl != null)
            {
                camLoader =
                        new(
                            downloadUrl: camLoaderDownloadUrl,
                            name: "cam-loader",
                            helpUrl: "https://github.com/lbormann/cam-loader",
                            descriptionShort: "Saves and loads settings for multiple cameras"
                            );
                AppsDownloadable.Add(camLoader);
            }

            var autodartsVoice = AppsDownloadable.Find(a => a.Name == "autodarts-voice");
            if (autodartsVoice != null)
            {
                if (autodartsVoiceDownloadUrl != null)
                {
                    autodartsVoice.DownloadUrl = autodartsVoiceDownloadUrl;

                    var keywordsNextGame = autodartsVoice.Configuration.Arguments.Find(a => a.Name == "KNG");
                    if (keywordsNextGame == null)
                    {
                        autodartsVoice.Configuration.Arguments.Add(new(name: "KNG", type: "string", required: false, isMulti: true, nameHuman: "keywords-next-game", section: "Voice-Recognition"));
                    }

                    var keywordsBanCaller = autodartsVoice.Configuration.Arguments.Find(a => a.Name == "KBC");
                    if (keywordsBanCaller == null)
                    {
                        autodartsVoice.Configuration.Arguments.Add(new(name: "KBC", type: "string", required: false, isMulti: true, nameHuman: "keywords-ban-caller", section: "Voice-Recognition"));
                    }

                    var keywordsChangeCaller = autodartsVoice.Configuration.Arguments.Find(a => a.Name == "KCC");
                    if (keywordsChangeCaller == null)
                    {
                        autodartsVoice.Configuration.Arguments.Add(new(name: "KCC", type: "string", required: false, isMulti: true, nameHuman: "keywords-change-caller", section: "Voice-Recognition"));
                    }

                    var keywordsStartBoard = autodartsVoice.Configuration.Arguments.Find(a => a.Name == "KSB");
                    if (keywordsStartBoard == null)
                    {
                        autodartsVoice.Configuration.Arguments.Add(new(name: "KSB", type: "string", required: false, isMulti: true, nameHuman: "keywords-start-board", section: "Voice-Recognition"));
                    }

                    var keywordsStopBoard = autodartsVoice.Configuration.Arguments.Find(a => a.Name == "KSPB");
                    if (keywordsStopBoard == null)
                    {
                        autodartsVoice.Configuration.Arguments.Add(new(name: "KSPB", type: "string", required: false, isMulti: true, nameHuman: "keywords-stop-board", section: "Voice-Recognition"));
                    }

                    var keywordsResetBoard = autodartsVoice.Configuration.Arguments.Find(a => a.Name == "KRB");
                    if (keywordsResetBoard == null)
                    {
                        autodartsVoice.Configuration.Arguments.Add(new(name: "KRB", type: "string", required: false, isMulti: true, nameHuman: "keywords-reset-board", section: "Voice-Recognition"));
                    }

                    var keywordsCalibrateBoard = autodartsVoice.Configuration.Arguments.Find(a => a.Name == "KCB");
                    if (keywordsCalibrateBoard == null)
                    {
                        autodartsVoice.Configuration.Arguments.Add(new(name: "KCB", type: "string", required: false, isMulti: true, nameHuman: "keywords-calibrate-board", section: "Voice-Recognition"));
                    }

                    autodartsVoice.DescriptionShort = "Controls autodarts by using your voice";

                }
                else
                {
                    var autodartsVoiceIndex = AppsDownloadable.FindIndex(a => a.Name == "autodarts-voice");
                    if (autodartsVoiceIndex != -1)
                    {
                        AppsDownloadable.RemoveAt(autodartsVoiceIndex);
                    }
                }
            }
            else if (autodartsVoiceDownloadUrl != null)
            {
                var autodartsVoiceArguments = new List<Argument> {
                        new(name: "CON", type: "string", required: false, nameHuman: "Connection", section: "Service"),
                        new(name: "MP", type: "path", required: true, nameHuman: "path-to-speech-model", section: "Voice-Recognition"),
                        new(name: "L", type: "int[0..2]", required: false, nameHuman: "language", section: "Voice-Recognition"),
                        new(name: "KN", type: "string", required: false, isMulti: true, nameHuman: "keywords-next", section: "Voice-Recognition"),
                        new(name: "KU", type: "string", required: false, isMulti: true, nameHuman: "keywords-undo", section: "Voice-Recognition"),
                        new(name: "KBC", type: "string", required: false, isMulti: true, nameHuman: "keywords-ban-caller", section: "Voice-Recognition"),
                        new(name: "KCC", type: "string", required: false, isMulti: true, nameHuman: "keywords-change-caller", section: "Voice-Recognition"),
                        new(name: "KFD", type: "string", required: false, isMulti: true, nameHuman: "keywords-first-dart", section: "Voice-Recognition"),
                        new(name: "KSD", type: "string", required: false, isMulti: true, nameHuman: "keywords-second-dart", section: "Voice-Recognition"),
                        new(name: "KTD", type: "string", required: false, isMulti: true, nameHuman: "keywords-third-dart", section: "Voice-Recognition"),
                        new(name: "KS", type: "string", required: false, isMulti: true, nameHuman: "keywords-single", section: "Voice-Recognition"),
                        new(name: "KD", type: "string", required: false, isMulti: true, nameHuman: "keywords-double", section: "Voice-Recognition"),
                        new(name: "KT", type: "string", required: false, isMulti: true, nameHuman: "keywords-triple", section: "Voice-Recognition"),
                        new(name: "KZERO", type: "string", required: false, isMulti: true, nameHuman: "keywords-zero", section: "Voice-Recognition"),
                        new(name: "KONE", type: "string", required: false, isMulti: true, nameHuman: "keywords-one", section: "Voice-Recognition"),
                        new(name: "KTWO", type: "string", required: false, isMulti: true, nameHuman: "keywords-two", section: "Voice-Recognition"),
                        new(name: "KTHREE", type: "string", required: false, isMulti: true, nameHuman: "keywords-three", section: "Voice-Recognition"),
                        new(name: "KFOUR", type: "string", required: false, isMulti: true, nameHuman: "keywords-four", section: "Voice-Recognition"),
                        new(name: "KFIVE", type: "string", required: false, isMulti: true, nameHuman: "keywords-five", section: "Voice-Recognition"),
                        new(name: "KSIX", type: "string", required: false, isMulti: true, nameHuman: "keywords-six", section: "Voice-Recognition"),
                        new(name: "KSEVEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-seven", section: "Voice-Recognition"),
                        new(name: "KEIGHT", type: "string", required: false, isMulti: true, nameHuman: "keywords-eight", section: "Voice-Recognition"),
                        new(name: "KNINE", type: "string", required: false, isMulti: true, nameHuman: "keywords-nine", section: "Voice-Recognition"),
                        new(name: "KTEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-ten", section: "Voice-Recognition"),
                        new(name: "KELEVEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-eleven", section: "Voice-Recognition"),
                        new(name: "KTWELVE", type: "string", required: false, isMulti: true, nameHuman: "keywords-twelve", section: "Voice-Recognition"),
                        new(name: "KTHIRTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-thirteen", section: "Voice-Recognition"),
                        new(name: "KFOURTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-fourteen", section: "Voice-Recognition"),
                        new(name: "KFIFTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-fifteen", section: "Voice-Recognition"),
                        new(name: "KSIXTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-sixteen", section: "Voice-Recognition"),
                        new(name: "KSEVENTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-seventeen", section: "Voice-Recognition"),
                        new(name: "KEIGHTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-eighteen", section: "Voice-Recognition"),
                        new(name: "KNINETEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-nineteen", section: "Voice-Recognition"),
                        new(name: "KTWENTY", type: "string", required: false, isMulti: true, nameHuman: "keywords-twenty", section: "Voice-Recognition"),
                        new(name: "KTWENTYFIVE", type: "string", required: false, isMulti: true, nameHuman: "keywords-twenty-five", section: "Voice-Recognition"),
                        new(name: "KFIFTY", type: "string", required: false, isMulti: true, nameHuman: "keywords-fifty", section: "Voice-Recognition"),
                        new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })
                    };


                autodartsVoice =
                    new(
                        downloadUrl: autodartsVoiceDownloadUrl,
                        name: "autodarts-voice",
                        helpUrl: "https://github.com/lbormann/autodarts-voice",
                        descriptionShort: "Controls autodarts by using your voice",
                        configuration: new(
                            prefix: "-",
                            delimitter: " ",
                            arguments: autodartsVoiceArguments)
                        );
                AppsDownloadable.Add(autodartsVoice);

            }


            // make all apps chmod-able
            foreach(var a in AppsDownloadable) a.Chmod = true;


            // Add more migs..
        }



        private void CreateDummyProfiles()
        {
            var autodartsClient = AppsDownloadable.Find(a => a.Name == "autodarts-client") != null;
            var autodartsCaller = AppsDownloadable.Find(a => a.Name == "autodarts-caller") != null;
            var autodartsExtern = AppsDownloadable.Find(a => a.Name == "autodarts-extern") != null;
            var autodartsWled = AppsDownloadable.Find(a => a.Name == "autodarts-wled") != null;
            var autodartsGif = AppsDownloadable.Find(a => a.Name == "autodarts-gif") != null;
            var autodartsVoice = AppsDownloadable.Find(a => a.Name == "autodarts-voice") != null;
            var virtualDartsZoom = AppsDownloadable.Find(a => a.Name == "virtual-darts-zoom") != null;
            var camLoader = AppsDownloadable.Find(a => a.Name == "cam-loader") != null;
            var droidCam = AppsInstallable.Find(a => a.Name == "droid-cam") != null;
            var epocCam = AppsInstallable.Find(a => a.Name == "epoc-cam") != null;
            var dartboardsClient = AppsInstallable.Find(a => a.Name == "dartboards-client") != null;
            var custom1 = AppsLocal.Find(a => a.Name == "custom-1") != null;
            var custom2 = AppsLocal.Find(a => a.Name == "custom-2") != null;
            var custom3 = AppsLocal.Find(a => a.Name == "custom-3") != null;
            var customUrl1 = AppsOpen.Find(a => a.Name == "custom-url-1") != null;
            var customUrl2 = AppsOpen.Find(a => a.Name == "custom-url-2") != null;
            var customUrl3 = AppsOpen.Find(a => a.Name == "custom-url-3") != null;

            if (autodartsCaller)
            {
                var p1Name = "autodarts-caller";
                var p1Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p1Apps.Add("autodarts-client", new ProfileState());
                p1Apps.Add("autodarts.io", new ProfileState());
                p1Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p1Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p1Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p1Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p1Apps.Add("autodarts-voice", new ProfileState());
                if (camLoader) p1Apps.Add("cam-loader", new ProfileState());
                if (custom1) p1Apps.Add("custom-1", new ProfileState());
                if (custom2) p1Apps.Add("custom-2", new ProfileState());
                if (custom3) p1Apps.Add("custom-3", new ProfileState());
                if (customUrl1) p1Apps.Add("custom-url-1", new ProfileState());
                if (customUrl2) p1Apps.Add("custom-url-2", new ProfileState());
                if (customUrl3) p1Apps.Add("custom-url-3", new ProfileState());
                Profiles.Add(new Profile(p1Name, p1Apps));
            }
            
            if (autodartsCaller && autodartsExtern)
            {
                var p2Name = "autodarts-extern: lidarts.org";
                var p2Args = new Dictionary<string, string> { { "extern_platform", "lidarts" } };
                var p2Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p2Apps.Add("autodarts-client", new ProfileState());
                p2Apps.Add("autodarts.io", new ProfileState());
                p2Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p2Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p2Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p2Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p2Apps.Add("autodarts-voice", new ProfileState());
                if (autodartsExtern) p2Apps.Add("autodarts-extern", new ProfileState(true, runtimeArguments: p2Args));
                if (virtualDartsZoom) p2Apps.Add("virtual-darts-zoom", new ProfileState());
                if (camLoader) p2Apps.Add("cam-loader", new ProfileState());
                if (droidCam) p2Apps.Add("droid-cam", new ProfileState());
                if (epocCam) p2Apps.Add("epoc-cam", new ProfileState());
                if (custom1) p2Apps.Add("custom-1", new ProfileState());
                if (custom2) p2Apps.Add("custom-2", new ProfileState());
                if (custom3) p2Apps.Add("custom-3", new ProfileState());
                if (customUrl1) p2Apps.Add("custom-url-1", new ProfileState());
                if (customUrl2) p2Apps.Add("custom-url-2", new ProfileState());
                if (customUrl3) p2Apps.Add("custom-url-3", new ProfileState());
                Profiles.Add(new Profile(p2Name, p2Apps));
            }

            if (autodartsCaller && autodartsExtern)
            {
                var p3Name = "autodarts-extern: nakka.com/n01/online";
                var p3Args = new Dictionary<string, string> { { "extern_platform", "nakka" } };
                var p3Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p3Apps.Add("autodarts-client", new ProfileState());
                p3Apps.Add("autodarts.io", new ProfileState());
                p3Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p3Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p3Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p3Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p3Apps.Add("autodarts-voice", new ProfileState());
                if (autodartsExtern) p3Apps.Add("autodarts-extern", new ProfileState(true, runtimeArguments: p3Args));
                if (virtualDartsZoom) p3Apps.Add("virtual-darts-zoom", new ProfileState());
                if (camLoader) p3Apps.Add("cam-loader", new ProfileState());
                if (droidCam) p3Apps.Add("droid-cam", new ProfileState());
                if (epocCam) p3Apps.Add("epoc-cam", new ProfileState());
                if (custom1) p3Apps.Add("custom-1", new ProfileState());
                if (custom2) p3Apps.Add("custom-2", new ProfileState());
                if (custom3) p3Apps.Add("custom-3", new ProfileState());
                if (customUrl1) p3Apps.Add("custom-url-1", new ProfileState());
                if (customUrl2) p3Apps.Add("custom-url-2", new ProfileState());
                if (customUrl3) p3Apps.Add("custom-url-3", new ProfileState());
                Profiles.Add(new Profile(p3Name, p3Apps));
            }

            if (autodartsCaller && autodartsExtern)
            {
                var p4Name = "autodarts-extern: dartboards.online";
                var p4Args = new Dictionary<string, string> { { "extern_platform", "dartboards" } };
                var p4Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p4Apps.Add("autodarts-client", new ProfileState());
                p4Apps.Add("autodarts.io", new ProfileState());
                p4Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p4Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p4Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p4Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p4Apps.Add("autodarts-voice", new ProfileState());
                if (autodartsExtern) p4Apps.Add("autodarts-extern", new ProfileState(true, runtimeArguments: p4Args));
                if (virtualDartsZoom) p4Apps.Add("virtual-darts-zoom", new ProfileState());
                if (camLoader) p4Apps.Add("cam-loader", new ProfileState());
                if (dartboardsClient) p4Apps.Add("dartboards-client", new ProfileState());
                if (droidCam) p4Apps.Add("droid-cam", new ProfileState());
                if (epocCam) p4Apps.Add("epoc-cam", new ProfileState());
                if (custom1) p4Apps.Add("custom-1", new ProfileState());
                if (custom2) p4Apps.Add("custom-2", new ProfileState());
                if (custom3) p4Apps.Add("custom-3", new ProfileState());
                if (customUrl1) p4Apps.Add("custom-url-1", new ProfileState());
                if (customUrl2) p4Apps.Add("custom-url-2", new ProfileState());
                if (customUrl3) p4Apps.Add("custom-url-3", new ProfileState());
                Profiles.Add(new Profile(p4Name, p4Apps));
            }

            if (autodartsClient)
            {
                var p5Name = "autodarts-client";
                var p5Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p5Apps.Add("autodarts-client", new ProfileState(true));
                p5Apps.Add("autodarts.io", new ProfileState());
                p5Apps.Add("autodarts-boardmanager", new ProfileState());
                if (camLoader) p5Apps.Add("cam-loader", new ProfileState());
                if (custom1) p5Apps.Add("custom-1", new ProfileState());
                if (custom2) p5Apps.Add("custom-2", new ProfileState());
                if (custom3) p5Apps.Add("custom-3", new ProfileState());
                if (customUrl1) p5Apps.Add("custom-url-1", new ProfileState());
                if (customUrl2) p5Apps.Add("custom-url-2", new ProfileState());
                if (customUrl3) p5Apps.Add("custom-url-3", new ProfileState());
                Profiles.Add(new Profile(p5Name, p5Apps));
            }

            SerializeProfiles(Profiles, profilesFile);
        }

        private void MigrateProfiles()
        {
            // 9. Mig (Remove autodarts-bot)
            foreach (var p in Profiles)
            {
                p.Apps.Remove("autodarts-bot");
            }

            // 15. Mig (Add autodarts-wled)
            foreach (var p in Profiles)
            {
                if (p.Name == "autodarts-client") continue;

                if (!p.Apps.ContainsKey("autodarts-wled"))
                {
                    p.Apps.Add("autodarts-wled", new());
                }
            }

            var autodartsClient = AppsDownloadable.Find(a => a.Name == "autodarts-client") != null;
            var autodartsCaller = AppsDownloadable.Find(a => a.Name == "autodarts-caller") != null;
            var autodartsExtern = AppsDownloadable.Find(a => a.Name == "autodarts-extern") != null;
            var autodartsWled = AppsDownloadable.Find(a => a.Name == "autodarts-wled") != null;
            var virtualDartsZoom = AppsDownloadable.Find(a => a.Name == "virtual-darts-zoom") != null;
            var droidCam = AppsInstallable.Find(a => a.Name == "droid-cam") != null;
            var epocCam = AppsInstallable.Find(a => a.Name == "epoc-cam") != null;
            var dartboardsClient = AppsInstallable.Find(a => a.Name == "dartboards-client") != null;
            var custom = AppsLocal.Find(a => a.Name == "custom") != null;

            if (!autodartsCaller)
            {
                Profiles.RemoveAll(p => p.Name == "autodarts-caller");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: lidarts.org");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: nakka.com/n01/online");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: dartboards.online");
            }
            if (!autodartsExtern)
            {
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: lidarts.org");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: nakka.com/n01/online");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: dartboards.online");
            }

            foreach (var p in Profiles)
            {
                if (!autodartsClient) p.Apps.Remove("autodarts-client");
                if (!autodartsCaller) p.Apps.Remove("autodarts-caller");
                if (!autodartsWled) p.Apps.Remove("autodarts-wled");
                if (!autodartsExtern) p.Apps.Remove("autodarts-extern");
                if (!virtualDartsZoom) p.Apps.Remove("virtual-darts-zoom");
                if (!dartboardsClient) p.Apps.Remove("dartboards-client");
                if (!droidCam) p.Apps.Remove("droid-cam");
                if (!epocCam) p.Apps.Remove("epoc-cam");
                if (!custom) p.Apps.Remove("custom");
            }

            var p5 = Profiles.Find(p => p.Name == "autodarts-client") != null;

            if (autodartsClient)
            {
                if (!p5)
                {
                    var p5Name = "autodarts-client";
                    var p5Apps = new Dictionary<string, ProfileState>();
                    if (autodartsClient) p5Apps.Add("autodarts-client", new ProfileState(true));
                    p5Apps.Add("autodarts.io", new ProfileState());
                    p5Apps.Add("autodarts-boardmanager", new ProfileState());
                    if (virtualDartsZoom) p5Apps.Add("virtual-darts-zoom", new ProfileState());
                    if (droidCam) p5Apps.Add("droid-cam", new ProfileState());
                    if (epocCam) p5Apps.Add("epoc-cam", new ProfileState());
                    if (custom) p5Apps.Add("custom", new ProfileState());
                    Profiles.Add(new Profile(p5Name, p5Apps));
                }
            }

            // Adds boardmanager to all profiles except autodarts-client
            foreach (var p in Profiles)
            {
                if (p.Name == "autodarts-client") continue;

                if (!p.Apps.ContainsKey("autodarts-boardmanager"))
                {
                    p.Apps.Add("autodarts-boardmanager", new());
                }
            }

            // Adds autodarts-gif to all profiles except autodarts-client and removes pointless apps from autodarts-client
            foreach (var p in Profiles)
            {
                if (p.Name == "autodarts-client")
                {
                    p.Apps.Remove("virtual-darts-zoom");
                    p.Apps.Remove("droid-cam");
                    p.Apps.Remove("epoc-cam");
                    continue;
                }

                if (!p.Apps.ContainsKey("autodarts-gif"))
                {
                    p.Apps.Add("autodarts-gif", new());
                }
            }


            // Adds or removes cam-loader for all profiles
            var camLoader = AppsDownloadable.Find(a => a.Name == "cam-loader") != null;
            if (!camLoader)
            {
                foreach (var p in Profiles)
                {
                    p.Apps.Remove("cam-loader");
                }
            }
            else
            {
                foreach (var p in Profiles)
                {
                    if (!p.Apps.ContainsKey("cam-loader"))
                    {
                        p.Apps.Add("cam-loader", new());
                    }
                }
            }

            // Adds or removes autodarts-voice for all profiles except autodarts-client
            var autodartsVoice = AppsDownloadable.Find(a => a.Name == "autodarts-voice") != null;

            
            if (!autodartsVoice)
            {
                foreach (var p in Profiles)
                {
                    p.Apps.Remove("autodarts-voice");
                }
            }
            else
            {
                foreach (var p in Profiles)
                {
                    if (p.Name == "autodarts-client") continue;

                    if (!p.Apps.ContainsKey("autodarts-voice"))
                    {
                        p.Apps.Add("autodarts-voice", new());
                    }
                }
            }

            /*
            foreach (var p in Profiles)
            {
                if (!p.Apps.ContainsKey("autodarts-voice"))
                {
                    p.Apps.Add("autodarts-voice", new());
                }
            }
            */



            // Renames custom to custom-1 in all profiles
            // Adds or removes custom-2+ and custom-url-2+ for all profiles
            var custom1 = AppsLocal.Find(a => a.Name == "custom-1") != null;
            var custom2 = AppsLocal.Find(a => a.Name == "custom-2") != null;
            var custom3 = AppsLocal.Find(a => a.Name == "custom-3") != null;
            var customUrl1 = AppsOpen.Find(a => a.Name == "custom-url-1") != null;
            var customUrl2 = AppsOpen.Find(a => a.Name == "custom-url-2") != null;
            var customUrl3 = AppsOpen.Find(a => a.Name == "custom-url-3") != null;

            foreach (var p in Profiles)
            {
                // Remove old custom
                if (p.Apps.ContainsKey("custom"))
                {
                    p.Apps.Remove("custom");
                }

                // customLocal
                if (custom1 && !p.Apps.ContainsKey("custom-1"))
                {
                    p.Apps.Add("custom-1", new());
                }
                if (custom2 && !p.Apps.ContainsKey("custom-2"))
                {
                    p.Apps.Add("custom-2", new());
                }
                if (custom3 && !p.Apps.ContainsKey("custom-3"))
                {
                    p.Apps.Add("custom-3", new());
                }

                //customOpen (Url)
                if (customUrl1 && !p.Apps.ContainsKey("custom-url-1"))
                {
                    p.Apps.Add("custom-url-1", new());
                }
                if (customUrl2 && !p.Apps.ContainsKey("custom-url-2"))
                {
                    p.Apps.Add("custom-url-2", new());
                }
                if (customUrl3 && !p.Apps.ContainsKey("custom-url-3"))
                {
                    p.Apps.Add("custom-url-3", new());
                }
            }


            // Add more migs..
        }



        private void SerializeApps<AppBase>(List<AppBase> apps, string filename)
        {
            var settings = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            };
            var appsJsonStr = JsonConvert.SerializeObject(apps, Formatting.Indented, settings);
            File.WriteAllText(filename, appsJsonStr);
        }
        
        private void SerializeProfiles(List<Profile> profiles, string filename)
        {
            var settings = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            };
            var profilesJsonStr = JsonConvert.SerializeObject(profiles, Formatting.Indented, settings);
            File.WriteAllText(filename, profilesJsonStr);
        }




        private void AppDownloadable_DownloadStarted(object? sender, AppEventArgs e)
        {
            OnAppDownloadStarted(e);
        }

        private void AppDownloadable_DownloadFinished(object? sender, AppEventArgs e)
        {
            OnAppDownloadFinished(e);
        }

        private void AppDownloadable_DownloadFailed(object? sender, AppEventArgs e)
        {
            OnAppDownloadFailed(e);
        }

        private void AppDownloadable_DownloadProgressed(object? sender, DownloadProgressChangedEventArgs e)
        {
            OnAppDownloadProgressed(e);
        }



        private void AppInstallable_InstallStarted(object? sender, AppEventArgs e)
        {
            OnAppInstallStarted(e);
        }

        private void AppInstallable_InstallFinished(object? sender, AppEventArgs e)
        {
            OnAppInstallFinished(e);
        }

        private void AppInstallable_InstallFailed(object? sender, AppEventArgs e)
        {
            OnAppInstallFailed(e);
        }

        private void App_AppConfigurationRequired(object? sender, AppEventArgs e)
        {
            OnAppConfigurationRequired(e);
        }



        protected virtual void OnAppDownloadStarted(AppEventArgs e)
        {
            AppDownloadStarted?.Invoke(this, e);
        }

        protected virtual void OnAppDownloadFinished(AppEventArgs e)
        {
            AppDownloadFinished?.Invoke(this, e);
        }

        protected virtual void OnAppDownloadFailed(AppEventArgs e)
        {
            AppDownloadFailed?.Invoke(this, e);
        }

        protected virtual void OnAppDownloadProgressed(DownloadProgressChangedEventArgs e)
        {
            AppDownloadProgressed?.Invoke(this, e);
        }



        protected virtual void OnAppInstallStarted(AppEventArgs e)
        {
            AppInstallStarted?.Invoke(this, e);
        }

        protected virtual void OnAppInstallFinished(AppEventArgs e)
        {
            AppInstallFinished?.Invoke(this, e);
        }

        protected virtual void OnAppInstallFailed(AppEventArgs e)
        {
            AppInstallFailed?.Invoke(this, e);
        }

        protected virtual void OnAppConfigurationRequired(AppEventArgs e)
        {
            AppConfigurationRequired?.Invoke(this, e);
        }
        

    }
}
