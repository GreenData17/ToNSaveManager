using Avalonia.Controls;

namespace ToNSaveManager.Localization {
    internal static class LocalizationExtensions {
        public static void Localize(this ContentControl control, string key, string? value = null) {
            (string? tx, string? tt) = LANG.T(key);
            if (!string.IsNullOrEmpty(tx)) control.Content = tx;
            else if (!string.IsNullOrEmpty(value)) control.Content = value;
            if (!string.IsNullOrEmpty(tt)) ToolTip.SetTip(control, tt);
        }

        public static void Localize(this TextBlock control, string key, string? value = null) {
            (string? tx, string? tt) = LANG.T(key);
            if (!string.IsNullOrEmpty(tx)) control.Text = tx;
            else if (!string.IsNullOrEmpty(value)) control.Text = value;
            if (!string.IsNullOrEmpty(tt)) ToolTip.SetTip(control, tt);
        }

        public static void Localize(this Window window, string key) {
            string? title = LANG.S(key);
            if (!string.IsNullOrEmpty(title)) window.Title = title;
        }

        public static void Localize(this MenuItem item, string key) {
            (string? text, string? tooltip) = LANG.T(key);
            if (!string.IsNullOrEmpty(text)) item.Header = text;
            if (!string.IsNullOrEmpty(tooltip)) ToolTip.SetTip(item, tooltip);
        }
    }
}
