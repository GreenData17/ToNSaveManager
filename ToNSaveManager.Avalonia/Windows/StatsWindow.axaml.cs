using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ToNSaveManager.Localization;
using ToNSaveManager.Models.Stats;

namespace ToNSaveManager.Windows {
    public sealed class StatRow : INotifyPropertyChanged {

        internal StatPropertyContainer Property { get; }
        internal int Index { get; }

        internal StatRow(StatPropertyContainer property, int index) {
            Property = property;
            Index = index;
        }

        internal string? DisplayName;
        internal string? DisplayTooltip;

        public string Name => DisplayName ?? Property.Key;
        public string Value => Property.Value?.ToString() ?? "NULL";
        public string? Tooltip => DisplayTooltip;
        // Every other row a bit lighter
        public IBrush Background => WindowExtensions.GetBrush(Index % 2 == 0 ? "ListBack" : "ListAlt");

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void Refresh() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    public partial class StatsWindow : Window {
        internal static StatsWindow? Instance { get; private set; }
        internal static void RefreshTable() => Instance?.UpdateTable();

        static StatPropertyContainer[] TableProperties => ToNStats.PropertyValues;

        readonly List<StatRow> Rows = new();
        readonly ContextMenu contextMenu = new();
        readonly MenuItem ctxTypeInValue = new() { Header = "Type in value" };
        readonly MenuItem ctxCopyStatName = new() { Header = "Copy stat name" };

        public StatsWindow() {
            InitializeComponent();

            ctxTypeInValue.Click += ctxTypeInValue_Click;
            ctxCopyStatName.Click += ctxCopyStatName_Click;
            contextMenu.Items.Add(ctxTypeInValue);
            contextMenu.Items.Add(ctxCopyStatName);

            listBox1.AddHandler(PointerReleasedEvent, listBoxEntries_MouseClick, RoutingStrategies.Bubble, true);

            StatsWindow_Load();
        }

        public static void Open(Window parent) {
            // Can't reopen a closed window
            if (Instance == null || Instance.IsClosed) Instance = new StatsWindow();
            Instance.ShowCentered(parent);
        }

        internal bool IsClosed { get; private set; }
        protected override void OnClosed(EventArgs e) {
            IsClosed = true;
            base.OnClosed(e);
        }

        internal void LocalizeContent() {
            this.Localize("STATS.TITLE");

            UpdateTable();

            foreach (StatRow row in Rows) {
                var property = row.Property;

                (string? tx, string? tt) = LANG.T(property.KeyLang);

                row.DisplayName = string.IsNullOrEmpty(tx) ? property.Key : tx;

                if (string.IsNullOrEmpty(tt)) tt = string.Empty;
                else tt += "\n\n";
                tt += LANG.S("STATS.TEMPLATE_KEY", property.KeyTemplate);

                row.DisplayTooltip = tt;
                row.Refresh();
            }

            ctxTypeInValue.Localize("STATS.CTX_TYPE_IN_VALUE");
            ctxCopyStatName.Localize("STATS.CTX_COPY_TEMPLATE_KEY");

            FlowDirection = LANG.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        internal void UpdateTable() {
            // Stats can change on other threads
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.Post(UpdateTable);
                return;
            }

            foreach (StatRow row in Rows) row.Refresh();
        }

        StatPropertyContainer? ContextField;

        private async void ctxTypeInValue_Click(object? sender, RoutedEventArgs e) {
            if (ContextField == null) return;

            string value = ContextField.GetValue()?.ToString() ?? "";
            EditResult show = await EditWindow.Show(value, LANG.S("STATS.CTX_TYPE_IN_VALUE.TITLE") ?? "Type in custom value", this);

            if (show.Accept && !string.IsNullOrEmpty(show.Text) && int.TryParse(show.Text.Trim(), out int result)) {
                ToNStats.Set(ContextField.Key, result);
                UpdateTable();
            }
        }

        private async void ctxCopyStatName_Click(object? sender, RoutedEventArgs e) {
            if (ContextField == null) return;

            string content = '{' + ContextField.Key + '}';
            if (Clipboard != null) await Clipboard.SetTextAsync(content);
            await MessageBox.Show(this, LANG.S("STATS.CTX_COPY_TEMPLATE_KEY.MESSAGE", content) ?? ("Copied to clipboard: " + content), string.Empty);
        }

        private void StatsWindow_Load() {
            Rows.Clear();
            for (int i = 0; i < TableProperties.Length; i++)
                Rows.Add(new StatRow(TableProperties[i], i));

            listBox1.ItemsSource = Rows;
            LocalizeContent();
            UpdateTable();
        }

        private void listBoxEntries_MouseClick(object? sender, PointerReleasedEventArgs e) {
            if (e.InitialPressMouseButton != MouseButton.Right) return;

            StatRow? row = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ContentPresenter>(true)?.DataContext as StatRow;
            if (row == null) return;

            ContextField = row.Property;
            ctxTypeInValue.IsEnabled = ContextField != null && ContextField.CanWrite;
            contextMenu.Open(listBox1);
        }
    }
}
