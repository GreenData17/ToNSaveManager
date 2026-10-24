using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ToNSaveManager.Localization;
using ToNSaveManager.Models;
using ToNSaveManager.Models.Stats;
using ToNSaveManager.Utils;
using ToNSaveManager.Utils.JSPlugins.API;
using ToNSaveManager.Utils.LogParser;
using ToNSaveManager.Windows;

namespace ToNSaveManager {
    public partial class App : Application {
        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && !Design.IsDesignMode) {
                AppCore.WriteFailed += (path, message, e) =>
                    MessageBox.ShowSync(message + "\n\n" + e, "Error", AlertButtons.OK, AlertIcon.Error);
                SaveData.ImportFailed += (path, e, backupCreated) => {
                    MessageBox.ShowSync((LANG.S("MESSAGE.IMPORT_SAVE_ERROR") ?? "Error trying to import your save.") + "\n\n" + e, "Import Error", AlertButtons.OK, AlertIcon.Error);
                    if (!backupCreated) desktop.Shutdown();
                };
                ToNLogContext.ConfirmLargeLog = (fileName, fileSize) => {
                    AlertResult result = MessageBox.ShowSync(
                        string.Format(LANG.S("MESSAGE.LARGE_LOG_WARNING") ?? "You are about to parse a very large VRChat log file!\nFile Name: {0}\nFile Size: {1}\n\nParsing this log file will take some extra time.\nWould you like to continue reading this file?", fileName, fileSize),
                        LANG.S("MESSAGE.LARGE_LOG_WARNING.TITLE") ?? "Parsing large log file!", AlertButtons.YesNo, AlertIcon.Warning);
                    return result == AlertResult.Yes || result != AlertResult.No;
                };
                SM.AlertHandler = MessageBox.ShowSync;

                ToNStats.StatChanged += (key, value) => StatsWindow.RefreshTable();
                LANG.LanguageAdded += () => SettingsWindow.Instance?.FillLanguageBox();
                LANG.LanguageChanged += LocalizeAllWindows;

                desktop.MainWindow = new MainWindow();
                desktop.Exit += (sender, e) => OnExit();
            }

            base.OnFrameworkInitializationCompleted();
        }

        static void LocalizeAllWindows() {
            MainWindow.Instance?.LocalizeContent();
            ObjectivesWindow.Instance?.LocalizeContent();
            SettingsWindow.Instance?.LocalizeContent();
            StatsWindow.Instance?.LocalizeContent();
        }

        static void OnExit() {
            Logger.Log("Saving before app exit...");
            AppCore.SaveData.Export();
            ToNLogHandler.WriteChanges();
            Integrations.Stop();
            Logger.Log("Done.");
        }
    }
}
