using Jint.Native.Function;

namespace ToNSaveManager.Utils.JSPlugins.API {
    [JSEngineAPI("HTTP")]
    internal class HTTP {
        public static string? GetString(string url) {
            using (HttpClient client = new HttpClient()) {
                try {
                    string result = client.GetStringAsync(url).Result;
                    return result;
                } catch (Exception) {
                    return null;
                }
            }
        }
    }
}
