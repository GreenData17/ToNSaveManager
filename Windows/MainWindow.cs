using System.Diagnostics;
using ToNSaveManager.Extensions;
using ToNSaveManager.Models;
using ToNSaveManager.Utils;
using ToNSaveManager.Windows;

using ToNSaveManager.Utils.Discord;
using ToNSaveManager.Localization;
using ToNSaveManager.Utils.LogParser;
using ToNSaveManager.Utils.JSPlugins;

namespace ToNSaveManager {
    public partial class MainWindow : Form {
        #region Initialization
        // internal static readonly AppSettings Settings = AppSettings.Import();
        internal static SaveData SaveData => AppCore.SaveData;
        internal static MainWindow? Instance;

        public MainWindow() {
            InitializeComponent();
            listBoxKeys.FixItemHeight();
            listBoxEntries.FixItemHeight();

            JustCopiedTimer.Interval = 1000;
            JustCopiedTimer.Tick += JustCopiedTimer_Tick;

            Instance = this;
        }
        #endregion

        #region Form Events

        #region Main Window
        private string OriginalTitle = string.Empty;
        public void SetTitle(string? title) {
            this.Text = string.IsNullOrEmpty(title) ? OriginalTitle : OriginalTitle + " | " + title;
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e) {
            Logger.Debug("Main Window is closing.");
            WinSettings.Get.LastWindowWidth = this.Width;
            WinSettings.Get.LastWindowHeight = this.Height;
            WinSettings.Get.LastWindowSplit = splitContainer1.SplitterDistance;
            WinSettings.Export();
        }

        private void MainWindow_Activated(object sender, EventArgs e) {
            if (EditWindow.IsActive) EditWindow.Instance.Focus();
        }

        private void mainWindow_Loaded(object sender, EventArgs e) {
            if (WinSettings.Get.LastWindowWidth > this.Width)
                this.Width = WinSettings.Get.LastWindowWidth;

            if (WinSettings.Get.LastWindowHeight > this.Height)
                this.Height = WinSettings.Get.LastWindowHeight;

            if (WinSettings.Get.LastWindowSplit > 0)
                splitContainer1.SplitterDistance = WinSettings.Get.LastWindowSplit;

            OriginalTitle = this.Text;
            this.Text = "Loading, please wait...";

            SetBackupButton(Settings.Get.DiscordWebhookEnabled && !string.IsNullOrWhiteSpace(Settings.Get.DiscordWebhookURL));
            TooltipUtil.Set(linkSupport, "Buy Me A Coffee ♥");
        }

        private void mainWindow_Shown(object sender, EventArgs e) {
            LocalizeContent();

            if (AppCore.Started) return;
            JSEngine.Initialize();

            FirstImport();

            ToNLogHandler.HistoryAdded += (collection, index) => listBoxKeys.Items.Insert(index, collection);
            ToNLogHandler.EntryAdded += (collection, entry, index) => {
                if (listBoxKeys.SelectedItem == collection)
                    InsertSafe(listBoxEntries, index, entry);
            };
            ToNLogHandler.CopyRequested += (entry, playSound) => {
                entry.CopyToClipboard();
                if (playSound) NotificationManager.PlayCopy();
            };
            ToNLogHandler.SaveStored += entry => PlaySaveNotification();
            ToNLogHandler.Start();

            AppCore.Started = true;
            SetTitle(null);

            if (Settings.Get.AutoCopy && Settings.Get.CopyOnOpen)
                ToNLogHandler.CopyRecentData();

            Integrations.Start();

            if (Args.ContainsArg("--emulator") || Args.ContainsArg("--emu") || Args.ContainsArg("-e"))
                EmulatorWindow.Open(this);

            JSEngine.InvokeOnReady();
        }

        internal void LocalizeContent() {
            btnSettings.Localize("MAIN.SETTINGS");
            btnObjectives.Localize("MAIN.OBJECTIVES");
            linkWiki.Localize("MAIN.WIKI");
            linkSupport.Localize("MAIN.SUPPORT");
            btnStats.Localize("MAIN.STATS");

            importToolStripMenuItem.Localize("MAIN.CTX_IMPORT"); // .TITLE
            renameToolStripMenuItem.Localize("MAIN.CTX_RENAME"); // .TITLE
            deleteToolStripMenuItem.Localize("MAIN.CTX_DELETE"); // .TITLE

            ctxMenuEntriesCopyTo.Localize("MAIN.CTX_ADD_TO");
            ctxMenuEntriesNew.Localize("MAIN.CTX_ADD_TO.NEW");
            ctxMenuEntriesNote.Localize("MAIN.CTX_EDIT_NOTE");
            ctxMenuEntriesBackup.Localize("MAIN.CTX_BACKUP");
            ctxMenuEntriesDelete.Localize("MAIN.CTX_DELETE");

            Entry.LocalizeContent();

            RightToLeft = LANG.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        }
        #endregion

        #region ListBox Keys
        private void listBoxKeys_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) {
                int index = listBoxKeys.IndexFromPoint(e.Location);
                if (index < 0 || index >= SaveData.Count) return;
                listBoxKeys.SelectedIndex = index;
            }
        }

        private void listBoxKeys_MouseUp(object sender, MouseEventArgs e) {
            bool isRight = e.Button == MouseButtons.Right;
            if (e.Button == MouseButtons.Left || isRight) {
                int index = listBoxKeys.SelectedIndex;
                if (index < 0)
                    return;

                if (isRight && index == listBoxKeys.IndexFromPoint(e.Location)) {
                    ctxMenuKeys_Show((ListBox)sender, e.Location);
                }

                UpdateEntries();
            }
        }

        private void listBoxKeys_KeyDown(object sender, KeyEventArgs e) {
            int count = listBoxKeys.Items.Count, index;

            if (count > 0) {
                switch (e.KeyCode) {
                    case Keys.Enter:
                        if (listBoxKeys.SelectedItem != null) {
                            if (e.Shift) ctxMenuKeys_Show((ListBox)sender, listBoxKeys.GetItemRectangle(listBoxKeys.SelectedIndex).Location);
                            else UpdateEntries();
                        }
                        break;

                    case Keys.Down:
                        index = listBoxKeys.SelectedIndex + 1;
                        if (index >= listBoxKeys.Items.Count) index = 0;
                        listBoxKeys.SelectedIndex = Math.Max(index, 0);
                        break;

                    case Keys.Up:
                        index = listBoxKeys.SelectedIndex - 1;
                        if (index < 0) index = listBoxKeys.Items.Count - 1;
                        listBoxKeys.SelectedIndex = Math.Min(index, listBoxKeys.Items.Count - 1);
                        break;

                    default:
                        break;
                }
            }

            e.Handled = true;
        }

        #region Context Menu | Keys
        private void ctxMenuKeys_Show(Control control, Point position) {
            importToolStripMenuItem.Enabled = renameToolStripMenuItem.Enabled =
                listBoxKeys.SelectedItem != null && ((History)listBoxKeys.SelectedItem).IsCustom;

            ctxMenuKeys.Show(control, position);
        }

        private void ctxMenuKeysImport_Click(object sender, EventArgs e) {
            if (listBoxKeys.SelectedItem == null) return;
            History h = (History)listBoxKeys.SelectedItem;
            if (!h.IsCustom) return;

            EditResult edit = EditWindow.Show(string.Empty, LANG.S("MAIN.CTX_IMPORT.TITLE") ?? "Import Title", this);
            if (edit.Accept && !string.IsNullOrWhiteSpace(edit.Text)) {
                string content = edit.Text.Trim();
                AddCustomEntry(new Entry(content, DateTime.Now) { Note = "Imported" }, h);
                Export();
            }
        }

        private void ctxMenuKeysRename_Click(object sender, EventArgs e) {
            if (listBoxKeys.SelectedItem == null) return;
            History h = (History)listBoxKeys.SelectedItem;
            if (!h.IsCustom) return;

            EditResult edit = EditWindow.Show(h.Name, LANG.S("MAIN.CTX_RENAME.TITLE") ?? "Set Collection Name", this);
            if (edit.Accept && !string.IsNullOrWhiteSpace(edit.Text)) {
                string title = edit.Text.Trim();
                if (title == h.Name) return;

                h.Name = title;
                listBoxKeys.Refresh();
                SetTitle(title);

                Export(h);
            }
        }

        private void ctxMenuKeysDelete_Click(object sender, EventArgs e) {
            int selectedIndex = listBoxKeys.SelectedIndex;
            if (selectedIndex != -1 && listBoxKeys.SelectedItem != null) {
                History h = (History)listBoxKeys.SelectedItem;
                DialogResult result = MessageBox.Show(
                    LANG.S("MAIN.CTX_DELETE_ALL.SUBTITLE", h.ToString()) ?? $"Are you SURE that you want to delete this entry?\n\nEvery code from '{h}' will be permanently deleted.\nThis operation is not reversible!",
                    LANG.S("MAIN.CTX_DELETE_ALL.TITLE", h.ToString()) ?? "Deleting Entry: " + h.ToString(),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                if (result == DialogResult.OK) {
                    listBoxKeys.SelectedIndex = -1;
                    listBoxKeys.Items.Remove(h);
                    SaveData.Remove(h);
                    UpdateEntries();
                    SetTitle(null);
                    Export(null, true);
                }
            }
        }
        #endregion
        #endregion

        #region ListBox Entries
        private void listBoxEntries_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) {
                int index = listBoxEntries.IndexFromPoint(e.Location);
                if (index < 0) return;
                listBoxEntries.SelectedIndex = index;
            }
        }

        int JustCopiedIndex = -1;
        System.Windows.Forms.Timer JustCopiedTimer = new System.Windows.Forms.Timer();
        private void listBoxEntries_MouseUp(object sender, MouseEventArgs e) {
            bool isRight = e.Button == MouseButtons.Right;
            if (e.Button == MouseButtons.Left || isRight) {
                int index = listBoxEntries.SelectedIndex;
                if (index < 0) return;

                if (isRight && index == listBoxEntries.IndexFromPoint(e.Location)) {
                    ctxMenuEntries.Show((ListBox)sender, e.Location);
                    return;
                }

                if (listBoxEntries.SelectedItem != null) {
                    Entry entry = (Entry)listBoxEntries.SelectedItem;
                    entry.CopyToClipboard();

                    SetJustCopied(index);

                    if (Settings.Get.PlayAudioCopy)
                        NotificationManager.PlayCopy();
                }

                listBoxEntries.SelectedIndex = -1;
            }
        }

        private void SetJustCopied(int index) {
            JustCopiedTimer.Stop();
            JustCopiedIndex = index;

            if (JustCopiedIndex > -1) {
                listBoxEntries.Refresh();
                JustCopiedTimer.Start();
            }
        }

        private void JustCopiedTimer_Tick(object? sender, EventArgs e) {
            JustCopiedTimer.Stop();

            if (JustCopiedIndex > -1) {
                JustCopiedIndex = -1;
                listBoxEntries.Refresh();
            }
        }

        private void listBoxEntries_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0)
                return;

            e.DrawBackground();

            ListBox listBox = (ListBox)sender;
            string itemText = listBox.Items[e.Index].ToString() ?? string.Empty;

            if (e.Index == JustCopiedIndex && listBox == listBoxEntries) {
                itemText = Entry.TextCopied;
            }

            int maxWidth = e.Bounds.Width;
            TextRenderer.DrawText(e.Graphics, GetTruncatedText(itemText, listBox.Font, maxWidth), listBox.Font, e.Bounds, e.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            e.DrawFocusRectangle();
        }

        private void listBoxEntries_Resize(object sender, EventArgs e) {
            listBoxEntries.Refresh();
        }

        // Tooltips
        int PreviousTooltipIndex = -1;
        private void listBoxEntries_MouseMove(object sender, MouseEventArgs e) {
            // Get the index of the item under the mouse pointer
            int index = listBoxEntries.IndexFromPoint(e.Location);

            if (PreviousTooltipIndex != index) {
                PreviousTooltipIndex = index;

                if (index < 0) {
                    TooltipUtil.Set(listBoxEntries, null);
                    return;
                }

                Entry entry = (Entry)listBoxEntries.Items[index];
                TooltipUtil.Set(listBoxEntries, entry.GetTooltip(Settings.Get.SaveNames, Settings.Get.SaveRoundInfo));
            }
        }

        // Reset tooltip when mouse leaves the control.
        // This prevents accidental tooltip display when doing ALT+TAB.
        private void listBoxEntries_MouseLeave(object sender, EventArgs e) {
            if (PreviousTooltipIndex < 0) return;
            PreviousTooltipIndex = -1;
            TooltipUtil.Set(listBoxEntries, null);
        }

        #region Context Menu | Entries
        private Entry? ContextEntry;

        private void ctxMenuEntries_Closed(object sender, ToolStripDropDownClosedEventArgs e) {
            listBoxEntries.Enabled = true;
            if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked)
                listBoxEntries.SelectedIndex = -1;
        }

        private void ctxMenuEntries_Opened(object sender, EventArgs e) {
            listBoxEntries.Enabled = false;
            ctxMenuEntriesCopyTo.DropDownItems.Clear();

            // Might not be the most efficient way of doing this
            foreach (History h in SaveData.Collection) {
                if (!h.IsCustom) continue;

                ToolStripMenuItem item = new ToolStripMenuItem(h.Name);
                ctxMenuEntriesCopyTo.DropDownItems.Insert(0, item);
                item.Click += (o, e) => {
                    if (ContextEntry != null)
                        AddCustomEntry(ContextEntry, h);
                };
            }

            ctxMenuEntriesCopyTo.DropDownItems.Add(ctxMenuEntriesNew);

            if (listBoxEntries.SelectedItem == null) ctxMenuEntries.Close();
            else {
                ContextEntry = (Entry)listBoxEntries.SelectedItem;
                if (ContextEntry.Parent == null)
                    ContextEntry.Parent = (History?)listBoxKeys.SelectedItem;
            }
        }

        private void ctxMenuEntriesNew_Click(object sender, EventArgs e) {
            if (ContextEntry != null)
                AddCustomEntry(ContextEntry, null);

            listBoxEntries.SelectedIndex = -1;
        }

        private void ctxMenuEntriesNote_Click(object sender, EventArgs e) {
            if (ContextEntry != null) {
                EditResult edit = EditWindow.Show(ContextEntry.Note, LANG.S("MAIN.CTX_EDIT_NOTE.TITLE") ?? "Note Editor", this);
                if (edit.Accept && !edit.Text.Equals(ContextEntry.Note, StringComparison.Ordinal)) {
                    ContextEntry.Note = edit.Text.Trim();
                    listBoxEntries.Refresh();
                    Export(ContextEntry.Parent);
                }
            }

            listBoxEntries.SelectedIndex = -1;
        }

        private void ctxMenuEntriesBackup_Click(object sender, EventArgs e) {
            if (ContextEntry != null)
                DSWebHook.Send(ContextEntry, true);

            listBoxEntries.SelectedIndex = -1;
        }

        private void ctxMenuEntriesDelete_Click(object sender, EventArgs e) {
            if (listBoxKeys.SelectedItem == null) {
                listBoxEntries.SelectedIndex = -1;
                return;
            }

            History h = (History)listBoxKeys.SelectedItem;
            if (ContextEntry != null) {
                DialogResult result = MessageBox.Show(
                    LANG.S("MAIN.CTX_DELETE_ENTRY.SUBTITLE", ContextEntry.ToString()) ?? $"Are you SURE that you want to delete this entry?\n\nDate: {ContextEntry.Timestamp}\nNote: {ContextEntry.Note}\n\nThis operation is not reversible!",
                    LANG.S("MAIN.CTX_DELETE_ENTRY.TITLE", ContextEntry.ToString()) ?? "Deleting Entry: " + ContextEntry.ToString(),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                if (result == DialogResult.OK) {
                    h.Database.Remove(ContextEntry);
                    listBoxEntries.Items.Remove(ContextEntry);
                    Export(ContextEntry.Parent);
                }
            }

            listBoxEntries.SelectedIndex = -1;
        }
        #endregion
        #endregion

        #region Settings & Info
        private void btnSettings_Click(object? sender, EventArgs e) {
            SettingsWindow.Open(this);
        }

        private void linkSource_Clicked(object sender, EventArgs ev) {
            const string link = "https://github.com/ChrisFeline/ToNSaveManager/";
            OpenExternalLink(link);
        }

        private void linkWiki_Clicked(object sender, EventArgs e) {
            const string wiki = "https://terror.moe/";
            const string wikiJP = "https://www.ton-jp.com/";
            OpenExternalLink(LANG.SelectedKey == "ja-JP" ? wikiJP : wiki);
        }

        private void linkSupport_Click(object sender, EventArgs e) {
            const string support = "https://ko-fi.com/kittenji";
            OpenExternalLink(support);
        }

        private void btnObjectives_Click(object sender, EventArgs e) {
            ObjectivesWindow.Open(this);
        }

        private void btnStats_Click(object sender, EventArgs e) {
            StatsWindow.Open(this);
        }

        internal static void OpenExternalLink(string? url) {
            Logger.Debug("Opening: " + url);
            if (string.IsNullOrEmpty(url)) return;

            ProcessStartInfo psInfo = new ProcessStartInfo { FileName = url, UseShellExecute = true };
            using (Process.Start(psInfo)) {
                Logger.Info("Opening external link: " + url);
            }
        }
        #endregion

        #region Split Container
        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e) {
            if (splitContainer1.CanFocus)
                splitContainer1.ActiveControl = listBoxEntries;

            listBoxKeys.Refresh();
            listBoxEntries.Refresh();
        }
        #endregion
        #endregion

        #region Form Methods
        #region Notifications
        internal static void ResetNotification() {
            NotificationManager.Reset();
        }

        internal static void PlayCopyNotification() {
            if (!AppCore.Started || !Settings.Get.PlayAudioCopy) return;

            try {
                NotificationManager.PlayCopy();
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        internal static void PlaySaveNotification() {
            if (!AppCore.Started || !Settings.Get.PlayAudioSave) return;

            try {
                NotificationManager.PlaySave();
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }
        #endregion

        internal static void RefreshLists() {
            Instance?.listBoxKeys.Refresh();
            Instance?.listBoxEntries.Refresh();
        }

        private void UpdateEntries() {
            listBoxEntries.Items.Clear();
            if (JustCopiedIndex > -1) SetJustCopied(-1);

            if (listBoxKeys.SelectedItem == null)
                return;

            History selected = (History)listBoxKeys.SelectedItem;
            SetTitle(selected.Name);

            foreach (Entry entry in selected.Database)
                listBoxEntries.Items.Add(entry);
        }

        private static void InsertSafe(ListBox list, int i, object value) =>
            list.Items.Insert(Math.Min(Math.Max(i, 0), list.Items.Count), value);

        internal static string GetTruncatedText(string text, Font font, int maxWidth) {
            Size textSize = TextRenderer.MeasureText(text, font);
            if (textSize.Width <= maxWidth) return text;

            int ellipsisWidth = TextRenderer.MeasureText("...", font).Width;
            while (textSize.Width + ellipsisWidth > maxWidth && text.Length > 0) {
                text = text.Substring(0, text.Length - 1);
                textSize = TextRenderer.MeasureText(text, font);
            }

            return text + "...";
        }

        internal void SetBackupButton(bool enabled) {
            ctxMenuEntriesBackup.Enabled = enabled;
        }
        #endregion

        #region Data
        private void Export(History? h = null, bool force = false) {
            if (h != null) h.SetDirty();
            SaveData.Export(force);
        }

        private void FirstImport() {
            History? temp = null;

            for (int i = 0; i < SaveData.Count; i++) {
                History h = SaveData[i];
                AddKey(h, i);
                if (h.IsCustom) continue;

                if (temp == null || temp.UniversalTime < h.UniversalTime) temp = h;
            }
        }

        private void AddCustomEntry(Entry entry, History? collection) {
            if (collection == null) {
                EditResult edit = EditWindow.Show(string.Empty, LANG.S("MAIN.CTX_RENAME.TITLE") ?? "Set Collection Name", this);
                if (edit.Accept && !string.IsNullOrWhiteSpace(edit.Text)) {
                    string title = edit.Text.Trim();
                    collection = new History(title, DateTime.Now);
                    AddKey(collection);
                } else return;
            } else collection.Timestamp = DateTime.Now; // Update edited timestamp

            int ind = collection.Add(entry);
            if (listBoxKeys.SelectedItem == collection)
                InsertSafe(listBoxEntries, ind, entry);

            Export(collection, true);
        }
        private void AddKey(History collection, int i = -1) {
            if (i == -1) i = SaveData.Add(collection);
            listBoxKeys.Items.Insert(i, collection);
        }
        #endregion

    }
}