using OpenRGB.NET;

namespace ToNSaveManager.Utils.Extensions {
    internal static class OpenRGBExtensions {
        internal static bool TryConnect(this OpenRgbClient client) {
			try {
                client.Connect();
			} catch {
                return false;
			}

            return client.Connected;
        }
    }
}
