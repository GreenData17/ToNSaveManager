using Avalonia.Controls;
using Avalonia.Interactivity;
using ToNSaveManager.Localization;
using ToNSaveManager.Models.Stats;

namespace ToNSaveManager.Windows {
    public struct EditResult {
        public string Text;
        public bool Accept;
    }

    public partial class EditWindow : Window {
        bool ShowDelete { get; set; }
        bool HideEmpty { get; set; }
        bool Accepted;

        string EditText {
            get => textBox1.Text ?? string.Empty;
            set => textBox1.Text = value;
        }

        public EditWindow() {
            InitializeComponent();
        }

        internal static async Task<EditResult> Show(string content, string title, Window parent, bool showDelete = false, bool hideEmpty = false, bool handleNewLine = false, bool insertKeyTemplate = false) {
            EditWindow window = new EditWindow();
            if (insertKeyTemplate) window.FillContextMenuItems();
            window.buttonInsert.IsVisible = insertKeyTemplate;

            if (handleNewLine) content = content.Replace("\n", "\\n");

            window.ShowDelete = showDelete;
            window.HideEmpty = hideEmpty;
            window.Title = title;
            window.EditText = content;
            window.FlowDirection = LANG.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;

            window.LocalizeContent();
            window.UpdateButtons();
            window.Opened += (sender, e) => {
                window.textBox1.Focus();
                window.textBox1.SelectAll();
            };

            await window.ShowDialog(parent);

            string text = window.EditText;
            if (handleNewLine) text = text.Replace("\\n", "\n");

            return new EditResult() { Text = text, Accept = window.Accepted };
        }

        // Save Button
        private void button1_Click(object? sender, RoutedEventArgs e) {
            Accepted = true;
            Close();
        }

        // Cancel Button
        private void button2_Click(object? sender, RoutedEventArgs e) {
            Close();
        }

        internal void LocalizeContent() {
            button1.Content = LANG.S("EDIT.SAVE") ?? "Save";
            button2.Content = LANG.S("EDIT.CANCEL") ?? "Cancel";
            buttonInsert.Content = LANG.S("EDIT.INSERT_TEMPLATE") ?? "Insert Template Key";
        }

        private void textBox1_TextChanged(object? sender, TextChangedEventArgs e) {
            UpdateButtons();
        }

        private bool IsEmpty;
        private void UpdateButtons() {
            if (HideEmpty) button1.IsVisible = !string.IsNullOrEmpty(EditText);

            if (ShowDelete) {
                bool isEmpty = string.IsNullOrEmpty(EditText);

                if (IsEmpty != isEmpty) {
                    IsEmpty = isEmpty;

                    button1.Content = (IsEmpty ? (LANG.S("EDIT.DELETE") ?? "Delete") : (LANG.S("EDIT.SAVE") ?? "Save"));
                }
            }
        }

        private void FillContextMenuItems() {
            contextMenuStrip1.Items.Clear();

            foreach (var kvp in ToNStats.PropertyGroups) {
                MenuItem group = new MenuItem { Header = kvp.Key };

                foreach (var v in kvp.Value) {
                    (string? tx, string? tt) = LANG.T(v.KeyLang);

                    if (string.IsNullOrEmpty(tt)) tt = string.Empty;
                    else tt += "\n\n";
                    tt += LANG.S("STATS.TEMPLATE_KEY", v.KeyTemplate);

                    MenuItem item = new MenuItem { Header = tx ?? v.Key };
                    ToolTip.SetTip(item, tt);
                    string key = v.KeyTemplate;
                    item.Click += (sender, e) => InsertText(key);
                    group.Items.Add(item);
                }

                contextMenuStrip1.Items.Add(group);
            }

            contextMenuStrip1.Items.Add(new MenuItem { Header = "Close" });
        }

        // Replaces the selection
        private void InsertText(string text) {
            int start = Math.Min(textBox1.SelectionStart, textBox1.SelectionEnd);
            int end = Math.Max(textBox1.SelectionStart, textBox1.SelectionEnd);
            EditText = EditText.Remove(start, end - start).Insert(start, text);
            textBox1.CaretIndex = start + text.Length;
        }

        private void buttonInsert_Click(object? sender, RoutedEventArgs e) {
            contextMenuStrip1.Open(buttonInsert);
        }
    }
}
