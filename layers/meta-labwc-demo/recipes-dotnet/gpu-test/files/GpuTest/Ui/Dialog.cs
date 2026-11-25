// Confirmation dialog drawn over the window (no second window: the kiosk
// runs full screen).
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GpuTest.Ui
{
    internal sealed class DialogLayer : Grid
    {
        public DialogLayer()
        {
            Background = AppTheme.Brush(Color.FromArgb(170, 0, 0, 0));
            IsVisible = false;
        }

        public Task<bool> Confirm(string title, string text, string ok, bool danger = false)
        {
            var done = new TaskCompletionSource<bool>();
            void Finish(bool result)
            {
                IsVisible = false;
                Children.Clear();
                done.TrySetResult(result);
            }

            var okButton = AppTheme.Button(ok, () => Finish(true), danger ? "danger" : "accent");
            var cancel = AppTheme.Button("Cancel", () => Finish(false));
            var buttons = AppTheme.Buttons(cancel, okButton);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 18, 0, 0);
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(AppTheme.Title(title));
            stack.Children.Add(AppTheme.Label(text));
            stack.Children.Add(buttons);
            Children.Clear();
            Children.Add(new Border
            {
                Background = AppTheme.Brush(AppTheme.CardColor),
                BorderBrush = AppTheme.Brush(AppTheme.CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(26, 22),
                Width = 560,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = stack,
            });
            IsVisible = true;
            return done.Task;
        }
    }
}
