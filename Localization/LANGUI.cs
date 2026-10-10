using ToNSaveManager.Windows;

namespace ToNSaveManager.Localization
{
    internal static class LANGUI
    {
        /// <summary>
        /// Applies the localized text, tooltip and right-to-left setting for <paramref name="key"/> to <paramref name="control"/>.
        /// </summary>
        /// <param name="toolTip">ToolTip component to use; if null, the shared tooltip is used.</param>
        /// <param name="value">Fallback text if the key has no translation.</param>
        public static void C(Control control, string key, ToolTip? toolTip = null, string? value = null) {
            (string? tx, string? tt) = LANG.T(key);
            if (!string.IsNullOrEmpty(tx)) control.Text         = tx;
            else if (!string.IsNullOrEmpty(value)) control.Text = value;
            if (!string.IsNullOrEmpty(tt)) {
                if (toolTip == null) TooltipUtil.Set(control, tt);
                else toolTip.SetToolTip(control, tt);
            }

            control.RightToLeft = LANG.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        }
        /// <summary>
        /// Applies the localized text, tooltip and right-to-left setting for <paramref name="key"/> to a menu item.
        /// </summary>
        public static void C(ToolStripItem item, string key) {
            (string? text, string? tooltip) = LANG.T(key);
            if (!string.IsNullOrEmpty(text)) item.Text           = text;
            if (!string.IsNullOrEmpty(tooltip)) item.ToolTipText = tooltip;

            item.RightToLeft = LANG.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        }
        
        internal static void ReloadAll() {
            MainWindow.Instance?.LocalizeContent();
            EditWindow.Instance?.LocalizeContent();
            ObjectivesWindow.Instance?.LocalizeContent();
            SettingsWindow.Instance?.LocalizeContent();
            StatsWindow.Instance?.LocalizeContent();
        }
        
        internal static void Initialize() {
            LANG.LanguageAdded += () => {
                SettingsWindow.Instance?.FillLanguageBox();
                ReloadAll();
            };
        }
    }
}
