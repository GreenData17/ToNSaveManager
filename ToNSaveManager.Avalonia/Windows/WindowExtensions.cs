using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ToNSaveManager.Windows {
    internal static class WindowExtensions {
        internal static void ShowCentered(this Window window, Window parent, bool owned = false) {
            if (window.IsVisible) {
                window.Activate();
                return;
            }

            double scaling = parent.RenderScaling;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(
                parent.Position.X + (int)((parent.Bounds.Width - window.Width) / 2 * scaling),
                Math.Max(parent.Position.Y + (int)((parent.Bounds.Height - window.Height) / 2 * scaling), 0)
            );
            if (owned) window.Show(parent);
            else window.Show(); // Don't parent
        }

        internal static IBrush GetBrush(string key) =>
            Application.Current != null && Application.Current.TryGetResource(key, null, out object? value) && value is IBrush brush ? brush : Brushes.Magenta;

        internal static Color ToAvalonia(this System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
