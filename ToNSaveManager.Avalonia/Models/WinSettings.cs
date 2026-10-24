using Newtonsoft.Json;

namespace ToNSaveManager.Models {
    internal class WinSettings {
        // Must be before Get
        static readonly string Destination = Path.Combine(AppCore.UserFilesDirectory, "window.json");

        internal static readonly WinSettings Get = Import();
        internal static void Export() => Get.TryExport();

        public int LastWindowWidth;
        public int LastWindowHeight;
        public int LastWindowSplit;

        static WinSettings Import() {
            WinSettings? settings;

            try {
                if (!File.Exists(Destination)) return new WinSettings();
                string content = File.ReadAllText(Destination);
                settings = JsonConvert.DeserializeObject<WinSettings>(content);
            } catch {
                settings = null;
            }

            return settings ?? new WinSettings();
        }

        private void TryExport() {
            try {
                string json = JsonConvert.SerializeObject(this);
                File.WriteAllText(Destination, json);
            } catch { } // Not important
        }
    }
}
