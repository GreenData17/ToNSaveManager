using static ToNSaveManager.Models.GitHubRelease;

namespace ToNSaveManager.Models {
    internal class GitHubUpdate {
        internal static GitHubRelease? Release;
        internal static Asset? Asset;

        internal static bool Update { get; set; }

        internal static void Set(GitHubRelease release, GitHubRelease.Asset asset) {
            Release = release;
            Asset = asset;
            Update = true;

            List<Form> openForms = new List<Form>();

            foreach (Form f in Application.OpenForms) {
                openForms.Add(f);
            }

            foreach (Form f in openForms) {
                f.Close();
            }
        }

        internal static void Start() {
            if (!Update || Release == null || Asset == null) return;
            Updater.Start(Release, Asset);
        }
    }
}
