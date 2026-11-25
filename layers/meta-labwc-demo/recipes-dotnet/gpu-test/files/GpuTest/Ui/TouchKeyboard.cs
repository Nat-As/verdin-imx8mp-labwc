// On-screen keyboard: the touch panel has no keyboard and Avalonia brings
// none on Linux. It types into the focused TextBox; its keys can't take the
// focus, so the text box keeps it (and its caret) while typing.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace GpuTest.Ui
{
    internal sealed class TouchKeyboard : Border
    {
        private const string Backspace = "⌫", Enter = "Enter", Shift = "⇧",
            Symbols = "?123", Letters = "ABC", Hide = "hide", Space = "space";

        // Rows of keys; width weights: special keys are wider
        private static readonly string[][] LetterRows =
        {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", Backspace },
            new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "-" },
            new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l", ":", Enter },
            new[] { Shift, "z", "x", "c", "v", "b", "n", "m", ",", ".", "/" },
            new[] { Symbols, "@", Space, "_", Hide },
        };
        private static readonly string[][] SymbolRows =
        {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", Backspace },
            new[] { "!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "=" },
            new[] { "~", "`", "|", "\\", "{", "}", "[", "]", "<", ">", Enter },
            new[] { "+", "-", "_", "'", "\"", ";", ":", "?", ",", ".", "/" },
            new[] { Letters, "€", Space, "°", Hide },
        };

        private readonly Grid rowsHost = new Grid();
        private bool shift, symbols;
        private TextBox target;

        public event Action Hidden;

        public TouchKeyboard()
        {
            Background = AppTheme.Brush(Color.FromRgb(24, 26, 31));
            BorderBrush = AppTheme.Brush(AppTheme.CardBorder);
            BorderThickness = new Thickness(0, 1, 0, 0);
            Padding = new Thickness(6, 6);
            Height = 5 * 50 + 12;
            Child = rowsHost;
            IsVisible = false;
            Build();
        }

        public TextBox Target => target;

        public void Show(TextBox box)
        {
            target = box;
            if (!IsVisible)
            {
                IsVisible = true;
                // keep the text box in view above the keyboard
                Dispatcher.UIThread.Post(() => box.BringIntoView(), DispatcherPriority.Background);
            }
        }

        public void Close()
        {
            if (!IsVisible)
                return;
            IsVisible = false;
            target = null;
            Hidden?.Invoke();
        }

        private void Build()
        {
            rowsHost.Children.Clear();
            rowsHost.RowDefinitions.Clear();
            string[][] rows = symbols ? SymbolRows : LetterRows;
            for (int r = 0; r < rows.Length; r++)
            {
                rowsHost.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                var row = new Grid();
                foreach (string key in rows[r])
                    row.ColumnDefinitions.Add(new ColumnDefinition(Weight(key), GridUnitType.Star));
                for (int c = 0; c < rows[r].Length; c++)
                {
                    Control k = MakeKey(rows[r][c]);
                    Grid.SetColumn(k, c);
                    row.Children.Add(k);
                }
                Grid.SetRow(row, r);
                rowsHost.Children.Add(row);
            }
        }

        private static double Weight(string key) => key switch
        {
            Space => 5.0,
            Enter or Symbols or Letters or Hide => 1.6,
            _ => 1.0,
        };

        private Control MakeKey(string key)
        {
            Control content;
            bool special = key.Length > 1 || key == Shift || key == Backspace;
            if (key == Hide)
            {
                content = new Path
                {
                    Data = Icons.Keyboard(),
                    Fill = AppTheme.Brush(AppTheme.Text),
                    Width = 30,
                    Height = 22,
                    Stretch = Stretch.Uniform,
                };
            }
            else
            {
                string text = key;
                if (key == Space)
                    text = "";
                else if (!special && shift && !symbols)
                    text = key.ToUpperInvariant();
                content = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
            }

            Button b = key == Backspace ? new RepeatButton { Delay = 400, Interval = 70 } : new Button();
            b.Content = content;
            b.Focusable = false;
            b.IsTabStop = false;
            b.Classes.Add("key");
            if (special)
                b.Classes.Add("special");
            if (key == Shift && shift)
                b.Classes.Add("accent");
            b.Click += (_, _) => Press(key);
            return b;
        }

        private void Press(string key)
        {
            switch (key)
            {
                case Hide:
                    Close();
                    return;
                case Shift:
                    shift = !shift;
                    Build();
                    return;
                case Symbols:
                case Letters:
                    symbols = key == Symbols;
                    Build();
                    return;
            }
            if (target == null)
                return;
            switch (key)
            {
                case Backspace:
                    Delete();
                    break;
                case Enter:
                    Close();
                    break;
                case Space:
                    Insert(" ");
                    break;
                default:
                    Insert(shift && !symbols ? key.ToUpperInvariant() : key);
                    if (shift)
                    {
                        shift = false;
                        Build();
                    }
                    break;
            }
        }

        private (int start, int end) Selection()
        {
            int len = target.Text?.Length ?? 0;
            int s = Math.Clamp(Math.Min(target.SelectionStart, target.SelectionEnd), 0, len);
            int e = Math.Clamp(Math.Max(target.SelectionStart, target.SelectionEnd), 0, len);
            if (s == e)
                s = e = Math.Clamp(target.CaretIndex, 0, len);
            return (s, e);
        }

        private void Insert(string s)
        {
            if (target.IsReadOnly)
                return;
            string text = target.Text ?? "";
            (int start, int end) = Selection();
            if (target.MaxLength > 0 && text.Length - (end - start) + s.Length > target.MaxLength)
                return;
            SetText(text.Substring(0, start) + s + text.Substring(end), start + s.Length);
        }

        private void Delete()
        {
            if (target.IsReadOnly)
                return;
            string text = target.Text ?? "";
            (int start, int end) = Selection();
            if (start == end)
            {
                if (start == 0)
                    return;
                start--;
            }
            SetText(text.Substring(0, start) + text.Substring(end), start);
        }

        private void SetText(string text, int caret)
        {
            target.Text = text;
            target.CaretIndex = caret;
            target.SelectionStart = caret;
            target.SelectionEnd = caret;
        }
    }
}
