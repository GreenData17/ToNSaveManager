using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ToNSaveManager.Windows.Controls {
    public class LinkLabel : TextBlock {
        public static readonly StyledProperty<IBrush?> LinkColorProperty =
            AvaloniaProperty.Register<LinkLabel, IBrush?>(nameof(LinkColor));

        public IBrush? LinkColor {
            get => GetValue(LinkColorProperty);
            set => SetValue(LinkColorProperty, value);
        }

        public event Action<LinkLabel, MouseButton>? LinkClicked;

        protected override Type StyleKeyOverride => typeof(TextBlock);

        public LinkLabel() {
            Cursor = new Cursor(StandardCursorType.Hand);
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            LinkColor = WindowExtensions.GetBrush("Link");
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (change.Property == LinkColorProperty) Foreground = LinkColor;
        }

        protected override void OnPointerEntered(PointerEventArgs e) {
            base.OnPointerEntered(e);
            TextDecorations = Avalonia.Media.TextDecorations.Underline;
        }

        protected override void OnPointerExited(PointerEventArgs e) {
            base.OnPointerExited(e);
            TextDecorations = null;
            Foreground = LinkColor;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e) {
            base.OnPointerPressed(e);
            Foreground = WindowExtensions.GetBrush("ListFore");
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e) {
            base.OnPointerReleased(e);
            Foreground = LinkColor;

            // Only if released over the link
            Point point = e.GetPosition(this);
            if (point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height)
                LinkClicked?.Invoke(this, e.InitialPressMouseButton);
            e.Handled = true;
        }
    }
}
