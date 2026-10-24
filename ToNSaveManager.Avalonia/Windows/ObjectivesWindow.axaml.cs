using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ToNSaveManager.Localization;
using ToNSaveManager.Models;

namespace ToNSaveManager.Windows {
    public sealed class ObjectiveRow : INotifyPropertyChanged {

        internal Objective Objective { get; }
        internal bool IsPressed;
        internal bool IsRightPressed;

        internal ObjectiveRow(Objective objective) {
            Objective = objective;
        }

        public string Text => Objective.ToString();
        // Categories have no description
        public string? Tooltip => Objective.IsSeparator ? null : Objective.Tooltip ?? Objective.Description;
        public IBrush? Background => Objective.IsSeparator ? WindowExtensions.GetBrush("WindowBack") : null;
        public TextDecorationCollection? Decorations => Objective.IsCompleted ? TextDecorations.Strikethrough : null;

        public IBrush Foreground {
            get {
                if (Objective.IsSeparator) return WindowExtensions.GetBrush("ListFore");
                if (IsPressed) return WindowExtensions.GetBrush(IsRightPressed ? "Link" : "Danger");
                if (Objective.IsCompleted) return WindowExtensions.GetBrush("Muted");
                return Settings.Get.ColorfulObjectives ? new SolidColorBrush(Objective.DisplayColor.ToAvalonia()) : WindowExtensions.GetBrush("ListFore");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void Refresh() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    public partial class ObjectivesWindow : Window {
        internal static ObjectivesWindow? Instance;
        static List<Objective> Objectives = Objective.ImportFromMemory();

        readonly List<ObjectiveRow> Rows = new();

        public ObjectivesWindow() {
            InitializeComponent();

            listBox1.AddHandler(PointerPressedEvent, listBox1_MouseDown, RoutingStrategies.Bubble, true);
            listBox1.AddHandler(PointerReleasedEvent, listBox1_MouseUp, RoutingStrategies.Bubble, true);

            ObjectivesWindow_Load();
        }

        public static void Open(Window parent) {
            // Can't reopen a closed window
            if (Instance == null || Instance.IsClosed) Instance = new ObjectivesWindow();
            Instance.ShowCentered(parent);
        }

        internal bool IsClosed { get; private set; }
        protected override void OnClosed(EventArgs e) {
            IsClosed = true;
            base.OnClosed(e);
        }

        internal static void RefreshLists() {
            if (Instance == null) return;
            foreach (ObjectiveRow row in Instance.Rows) row.Refresh();
        }

        internal void LocalizeContent() {
            this.Localize("OBJECTIVES.TITLE");

            foreach (ObjectiveRow row in Rows) {
                Objective obj = row.Objective;
                (string? tx, string? tt) = LANG.T("OBJECTIVES." + obj.Name.ToUpperInvariant().Replace(' ', '_'));
                if (!string.IsNullOrEmpty(tx)) obj.DisplayName = tx;
                if (!string.IsNullOrEmpty(tt)) obj.Tooltip = tt;
                row.Refresh();
            }

            FlowDirection = LANG.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        private void ObjectivesWindow_Load() {
            Rows.Clear();

            foreach (Objective ob in Objectives)
                Rows.Add(new ObjectiveRow(ob));

            listBox1.ItemsSource = Rows;
            LocalizeContent();
        }

        private static ObjectiveRow? RowFromPoint(RoutedEventArgs e) =>
            (e.Source as Avalonia.Visual)?.FindAncestorOfType<ContentPresenter>(true)?.DataContext as ObjectiveRow;

        private ObjectiveRow? MouseDownRow;
        private void listBox1_MouseDown(object? sender, PointerPressedEventArgs e) {
            PointerPointProperties point = e.GetCurrentPoint(listBox1).Properties;
            if (!point.IsLeftButtonPressed && !point.IsRightButtonPressed) return;

            MouseDownRow = RowFromPoint(e);
            // First row can't be pressed
            if (MouseDownRow == null || Rows.IndexOf(MouseDownRow) <= 0) {
                MouseDownRow = null;
                return;
            }

            MouseDownRow.IsPressed = true;
            MouseDownRow.IsRightPressed = point.IsRightButtonPressed;
            MouseDownRow.Refresh();
        }

        private void listBox1_MouseUp(object? sender, PointerReleasedEventArgs e) {
            ObjectiveRow? row = RowFromPoint(e);
            ObjectiveRow? pressed = MouseDownRow;
            MouseDownRow = null;
            if (pressed != null) {
                pressed.IsPressed = false;
                pressed.Refresh();
            }

            if (row == null || row != pressed) return;

            Objective objective = row.Objective;
            if (objective.IsSeparator) return;

            switch (e.InitialPressMouseButton) {
                case MouseButton.Right:
                    MainWindow.OpenExternalLink(objective.Reference);
                    break;
                case MouseButton.Left:
                    objective.Toggle();
                    break;

                default: break;
            }

            row.Refresh();
        }
    }
}
