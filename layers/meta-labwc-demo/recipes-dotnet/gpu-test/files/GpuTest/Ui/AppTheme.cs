// Colours, styles and small control factories of the app. Touch first: the
// 7" panel is 1024x600, so controls are at least 48 px high.
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace GpuTest.Ui
{
    internal static class AppTheme
    {
        public static readonly Color Background = Color.FromRgb(18, 19, 23);
        public static readonly Color Panel = Color.FromRgb(26, 28, 34);
        public static readonly Color CardColor = Color.FromRgb(34, 37, 45);
        public static readonly Color CardBorder = Color.FromRgb(48, 52, 62);
        public static readonly Color Text = Color.FromRgb(222, 226, 234);
        public static readonly Color Dim = Color.FromRgb(150, 157, 170);
        public static readonly Color Accent = Color.FromRgb(224, 69, 69);   // red of the Alakai logo
        public static readonly Color Good = Color.FromRgb(115, 220, 140);
        public static readonly Color Bad = Color.FromRgb(255, 120, 110);

        public const double FontSize = 18;
        public const double RowHeight = 48;

        public static IBrush Brush(Color c) => new SolidColorBrush(c);

        // Fluent with the accent colour of the app (buttons of class "accent",
        // switches, sliders, focus)
        public static Avalonia.Themes.Fluent.FluentTheme Fluent()
        {
            var fluent = new Avalonia.Themes.Fluent.FluentTheme();
            fluent.Palettes[ThemeVariant.Dark] = new Avalonia.Themes.Fluent.ColorPaletteResources { Accent = Accent };
            return fluent;
        }

        // Larger controls than the Fluent defaults
        public static Styles Styles()
        {
            var s = new Styles();
            void Add(Func<Selector, Selector> sel, params Setter[] setters)
            {
                var style = new Style(sel);
                foreach (Setter setter in setters)
                    style.Setters.Add(setter);
                s.Add(style);
            }
            Add(x => x.OfType<Button>(), S(TemplatedControl.FontSizeProperty, FontSize),
                S(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                S(Layoutable.MinHeightProperty, RowHeight), S(TemplatedControl.PaddingProperty, new Thickness(18, 8)),
                S(TemplatedControl.CornerRadiusProperty, new CornerRadius(8)),
                S(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center));
            Add(x => x.OfType<RepeatButton>(), S(TemplatedControl.FontSizeProperty, FontSize));
            Add(x => x.OfType<TextBox>(), S(TemplatedControl.FontSizeProperty, FontSize),
                S(Layoutable.MinHeightProperty, RowHeight),
                S(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            Add(x => x.OfType<ComboBox>(), S(TemplatedControl.FontSizeProperty, FontSize),
                S(Layoutable.MinHeightProperty, RowHeight), S(Layoutable.MinWidthProperty, 200.0));
            Add(x => x.OfType<ComboBoxItem>(), S(TemplatedControl.FontSizeProperty, FontSize),
                S(Layoutable.MinHeightProperty, RowHeight));
            Add(x => x.OfType<ToggleSwitch>(), S(TemplatedControl.FontSizeProperty, FontSize));
            Add(x => x.OfType<ScrollBar>(), S(Layoutable.MinWidthProperty, 12.0));
            Add(x => x.Is<Button>().Class("danger"),
                S(TemplatedControl.BackgroundProperty, Brush(Color.FromRgb(120, 34, 34))),
                S(TemplatedControl.ForegroundProperty, Brushes.White));
            Add(x => x.Is<Button>().Class("key"),
                S(TemplatedControl.PaddingProperty, new Thickness(0)),
                S(Layoutable.MinHeightProperty, 0.0),
                S(TemplatedControl.FontSizeProperty, 22.0),
                S(TemplatedControl.BackgroundProperty, Brush(Color.FromRgb(56, 60, 71))),
                S(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
                S(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                S(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Stretch),
                S(Layoutable.MarginProperty, new Thickness(3)),
                S(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                S(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            Add(x => x.Is<Button>().Class("key").Class("special"),
                S(TemplatedControl.BackgroundProperty, Brush(Color.FromRgb(40, 43, 52))),
                S(TemplatedControl.FontSizeProperty, 18.0));
            Add(x => x.Is<Button>().Class("category"),
                S(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                S(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                S(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                S(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                S(Layoutable.MinHeightProperty, 56.0),
                S(TemplatedControl.PaddingProperty, new Thickness(20, 8)));
            Add(x => x.Is<Button>().Class("category").Class("selected"),
                S(TemplatedControl.BackgroundProperty, Brush(CardColor)),
                S(TemplatedControl.BorderBrushProperty, Brush(Accent)),
                S(TemplatedControl.BorderThicknessProperty, new Thickness(4, 0, 0, 0)));
            Add(x => x.Is<Button>().Class("nav"),
                S(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                S(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                S(TemplatedControl.PaddingProperty, new Thickness(0)),
                S(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                S(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Stretch),
                S(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                S(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            return s;
        }

        private static Setter S(AvaloniaProperty p, object v) => new Setter(p, v);

        public static TextBlock Title(string text) => new TextBlock
        {
            Text = text,
            FontSize = 26,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(Text),
            Margin = new Thickness(0, 0, 0, 12),
        };

        public static TextBlock Label(string text, Color? color = null, double size = FontSize) => new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = Brush(color ?? Text),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        public static TextBlock Value(string text = "") => new TextBlock
        {
            Text = text,
            FontSize = FontSize,
            Foreground = Brush(Text),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // A card with an optional heading
        public static Border Card(string heading, params Control[] children)
        {
            var stack = new StackPanel { Spacing = 8 };
            if (heading != null)
                stack.Children.Add(new TextBlock
                {
                    Text = heading,
                    FontSize = 20,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(Text),
                    Margin = new Thickness(0, 0, 0, 4),
                });
            foreach (Control c in children)
                stack.Children.Add(c);
            return new Border
            {
                Background = Brush(CardColor),
                BorderBrush = Brush(CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 14),
                Margin = new Thickness(0, 0, 0, 14),
                Child = stack,
            };
        }

        // Label on the left, value/control on the right
        public static Grid Row(string label, Control value, double labelWidth = 200)
        {
            var g = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions($"{labelWidth},*"),
                MinHeight = 34,
            };
            var l = Label(label, Dim);
            l.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(l);
            Grid.SetColumn(value, 1);
            value.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(value);
            return g;
        }

        public static StackPanel Buttons(params Control[] buttons)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
            foreach (Control b in buttons)
                p.Children.Add(b);
            return p;
        }

        public static Button Button(string text, Action onClick, string cls = null)
        {
            var b = new Button { Content = text };
            if (cls != null)
                b.Classes.Add(cls);
            b.Click += (_, _) => onClick();
            return b;
        }

        // A button that runs an async action: disabled while it runs, errors
        // shown in the status text
        public static Button AsyncButton(string text, Func<Task> action, StatusText status, string cls = null)
        {
            var b = new Button { Content = text };
            if (cls != null)
                b.Classes.Add(cls);
            b.Click += async (_, _) =>
            {
                b.IsEnabled = false;
                try
                {
                    await action();
                }
                catch (Exception e)
                {
                    status?.Error(e.Message);
                }
                finally
                {
                    b.IsEnabled = true;
                }
            };
            return b;
        }

        public static TextBox TextField(string placeholder, double width = double.NaN) => new TextBox
        {
            PlaceholderText = placeholder,
            Width = width,
            HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
        };
    }

    // One-line status/error message under a form
    internal sealed class StatusText : TextBlock
    {
        public StatusText()
        {
            FontSize = 16;
            TextWrapping = TextWrapping.Wrap;
            IsVisible = false;
        }

        public void Info(string text) => Show(text, AppTheme.Dim);
        public void Ok(string text) => Show(text, AppTheme.Good);
        public void Error(string text) => Show(text, AppTheme.Bad);
        public void Clear() => IsVisible = false;

        // null = success message, else the error
        public void Result(string error, string success)
        {
            if (error == null)
                Ok(success);
            else
                Error(error);
        }

        private void Show(string text, Color color)
        {
            Text = text;
            Foreground = AppTheme.Brush(color);
            IsVisible = !string.IsNullOrEmpty(text);
        }
    }
}
