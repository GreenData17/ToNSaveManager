using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ToNSaveManager.Utils.JSPlugins.API;

namespace ToNSaveManager.Windows {
    internal static class MessageBox {
        static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        internal static async Task<AlertResult> Show(Window? owner, string text, string title, AlertButtons buttons = AlertButtons.OK, AlertIcon icon = AlertIcon.None) {
            Window dialog = Create(text, title, buttons, icon, out Func<AlertResult> getResult);
            owner ??= MainWindow;

            if (owner != null && owner.IsVisible) await dialog.ShowDialog(owner);
            else {
                TaskCompletionSource closed = new TaskCompletionSource();
                dialog.Closed += (sender, e) => closed.SetResult();
                dialog.Show();
                await closed.Task;
            }

            return getResult();
        }

        // Blocking, for Core callbacks
        internal static AlertResult ShowSync(string text, string title, AlertButtons buttons, AlertIcon icon) {
            if (!Dispatcher.UIThread.CheckAccess())
                return Dispatcher.UIThread.Invoke(() => ShowSync(text, title, buttons, icon));

            // Nested loop until the box closes
            CancellationTokenSource closed = new CancellationTokenSource();
            Task<AlertResult> task = Show(null, text, title, buttons, icon);
            task.ContinueWith(t => closed.Cancel(), TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.MainLoop(closed.Token);
            return task.Result;
        }

        static Window Create(string text, string title, AlertButtons buttons, AlertIcon icon, out Func<AlertResult> getResult) {
            AlertResult result = AlertResult.None;
            getResult = () => result;

            Window dialog = new Window {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                MinWidth = 180,
                MaxWidth = 500
            };

            StackPanel buttonPanel = new StackPanel {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 10)
            };

            AlertResult[] choices = GetChoices(buttons);
            foreach (AlertResult choice in choices) {
                Button button = new Button {
                    Content = GetText(choice),
                    MinWidth = 75,
                    Height = 23,
                    IsDefault = choice == choices[0],
                    IsCancel = choice == AlertResult.Cancel || choice == AlertResult.No || choices.Length == 1
                };
                button.Click += (sender, e) => {
                    result = choice;
                    dialog.Close();
                };
                buttonPanel.Children.Add(button);
            }

            DockPanel body = new DockPanel { Margin = new Thickness(24, 24, 24, 22) };
            Control? iconControl = CreateIcon(icon);
            if (iconControl != null) {
                DockPanel.SetDock(iconControl, Dock.Left);
                body.Children.Add(iconControl);
            }
            body.Children.Add(new TextBlock {
                Text = text,
                Foreground = WindowExtensions.GetBrush("ListFore"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });

            Border footer = new Border {
                Background = WindowExtensions.GetBrush("ListBack"),
                Child = buttonPanel
            };
            DockPanel.SetDock(footer, Dock.Bottom);

            DockPanel root = new DockPanel();
            root.Children.Add(footer);
            root.Children.Add(body);
            dialog.Content = root;

            return dialog;
        }

        static Control? CreateIcon(AlertIcon icon) {
            (string glyph, string color) = icon switch {
                AlertIcon.Error => ("✕", "Danger"),
                AlertIcon.Question => ("?", "Highlight"),
                AlertIcon.Warning => ("!", "Warning"),
                AlertIcon.Information => ("i", "Highlight"),
                _ => (string.Empty, string.Empty)
            };
            if (glyph.Length == 0) return null;

            return new Border {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = WindowExtensions.GetBrush(color),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock {
                    Text = glyph,
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Foreground = icon == AlertIcon.Warning ? WindowExtensions.GetBrush("WindowBack") : WindowExtensions.GetBrush("ListFore"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        static AlertResult[] GetChoices(AlertButtons buttons) => buttons switch {
            AlertButtons.OKCancel => [AlertResult.OK, AlertResult.Cancel],
            AlertButtons.AbortRetryIgnore => [AlertResult.Abort, AlertResult.Retry, AlertResult.Ignore],
            AlertButtons.YesNoCancel => [AlertResult.Yes, AlertResult.No, AlertResult.Cancel],
            AlertButtons.YesNo => [AlertResult.Yes, AlertResult.No],
            AlertButtons.RetryCancel => [AlertResult.Retry, AlertResult.Cancel],
            AlertButtons.CancelTryContinue => [AlertResult.Cancel, AlertResult.TryAgain, AlertResult.Continue],
            _ => [AlertResult.OK]
        };

        static string GetText(AlertResult result) => result switch {
            AlertResult.TryAgain => "Try Again",
            _ => result.ToString()
        };
    }
}
