using ToNSaveManager.Models;

namespace ToNSaveManager.Utils {
    internal enum UpdateStatus {
        None,
        UpToDate,
        Available
    }

    internal static class UpdateChecker {
        internal const string ReleasesUrl = "https://github.com/ChrisFeline/ToNSaveManager/releases";

        const string log_start = "[changelog]: <> (START)";
        const string log_end = "[changelog]: <> (END)";

        /// <summary>
        /// Checks the latest GitHub release.
        /// </summary>
        /// <param name="showUpToDate">Also check releases the user ignored.</param>
        /// <param name="assetName">The zip the release needs to have, or null if nothing gets downloaded.</param>
        internal static UpdateStatus Check(bool showUpToDate, string? assetName, out GitHubRelease? release, out GitHubRelease.Asset? asset, out string changelog) {
            release = null;
            asset = null;
            changelog = string.Empty;

            Version? currentVersion = AppCore.Version;
            if (currentVersion == null) return UpdateStatus.None; // No current version?

            Logger.Log("Checking for updates...");
            release = GitHubRelease.GetLatest();
            if (release == null || release.assets.Length == 0 || (!showUpToDate && release.tag_name == Settings.Get.IgnoreRelease)) return UpdateStatus.None;
            if (assetName != null) {
                asset = release.assets.FirstOrDefault(v => v.name == assetName && v.content_type == "application/zip" && v.state == "uploaded");
                if (asset == null) return UpdateStatus.None;
            }

            Logger.Log("Latest release: " + release.tag_name);
            if (!Version.TryParse(release.tag_name, out Version? releaseVersion) || releaseVersion <= currentVersion)
                return UpdateStatus.UpToDate;

            int start = release.body.IndexOf(log_start, StringComparison.InvariantCulture);
            int end = release.body.IndexOf(log_end, StringComparison.InvariantCulture);

            if (start > -1 && end > (start + log_start.Length) && end > start) {
                start += log_start.Length;
                changelog = "\n\n" + release.body.Substring(start, end - start).Trim();
            }

            return UpdateStatus.Available;
        }

        // Don't ask again on startup
        internal static void Ignore(GitHubRelease release) {
            Settings.Get.IgnoreRelease = release.tag_name;
            Settings.Export();
        }
    }
}
