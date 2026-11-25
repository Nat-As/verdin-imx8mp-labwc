// Settings: categories on the left, the selected category's panel on the
// right (scrollable). Panels are created on first use and refreshed each time
// they are shown.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GpuTest.Pages.Settings;
using GpuTest.Ui;

namespace GpuTest.Pages
{
    // Base of the settings panels
    internal abstract class SettingsPanel : StackPanel
    {
        protected SettingsPanel(string title)
        {
            Margin = new Thickness(24, 18, 24, 24);
            Header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 4) };
            Header.Children.Add(AppTheme.Title(title));
            Children.Add(Header);
        }

        protected Grid Header { get; }

        // A button on the right of the title (Refresh, Scan, ...)
        protected void HeaderButton(Button b)
        {
            Grid.SetColumn(b, 1);
            b.VerticalAlignment = VerticalAlignment.Top;
            Header.Children.Add(b);
        }

        // Shown (again): reload the system state
        public virtual void Shown() { }

        // Replaced by another panel or page
        public virtual void Hidden() { }
    }

    internal sealed class SettingsPage : Grid
    {
        private readonly List<(string name, Func<SettingsPanel> create, Button button)> categories =
            new List<(string, Func<SettingsPanel>, Button)>();
        private readonly Dictionary<string, SettingsPanel> panels = new Dictionary<string, SettingsPanel>();
        private readonly ScrollViewer detail = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        private SettingsPanel current;
        private string selected;

        public SettingsPage()
        {
            ColumnDefinitions = new ColumnDefinitions("220,*");
            Background = AppTheme.Brush(AppTheme.Background);

            var list = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            var heading = AppTheme.Label("Settings", AppTheme.Dim, 15);
            heading.Margin = new Thickness(20, 4, 0, 8);
            list.Children.Add(heading);
            Add(list, "Network", () => new NetworkPanel());
            Add(list, "Display", () => new DisplayPanel());
            Add(list, "Date & Time", () => new DateTimePanel());
            Add(list, "Remote Desktop", () => new RemotePanel());
            Add(list, "System", () => new SystemPanel());
            Add(list, "About", () => new AboutPanel());
            var side = new Border
            {
                Background = AppTheme.Brush(AppTheme.Panel),
                BorderBrush = AppTheme.Brush(AppTheme.CardBorder),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new ScrollViewer { Content = list },
            };
            Children.Add(side);
            Grid.SetColumn(detail, 1);
            Children.Add(detail);
            Select("Network");
        }

        private void Add(StackPanel list, string name, Func<SettingsPanel> create)
        {
            var b = new Button { Content = name, Focusable = false };
            b.Classes.Add("category");
            b.Click += (_, _) => Select(name);
            list.Children.Add(b);
            categories.Add((name, create, b));
        }

        // Settings page shown or left (navigation bar)
        public void Shown() => current?.Shown();
        public void Left() => current?.Hidden();

        public void Select(string name)
        {
            if (name == selected && current != null)
                return;
            MainWindow.Current?.Keyboard.Close();
            current?.Hidden();
            foreach ((string n, Func<SettingsPanel> create, Button button) in categories)
            {
                button.Classes.Set("selected", n == name);
                if (n != name)
                    continue;
                if (!panels.TryGetValue(n, out SettingsPanel panel))
                    panels[n] = panel = create();
                current = panel;
            }
            selected = name;
            detail.Content = current;
            detail.Offset = default;
            current.Shown();
        }
    }
}
