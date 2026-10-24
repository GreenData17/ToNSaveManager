using Avalonia;

namespace ToNSaveManager {
    internal static class Program {
        [STAThread]
        static void Main(string[] args) {
            AppCore.Initialize(args);
            Logger.Log("Starting Avalonia frontend.");

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Also used by the designer
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
