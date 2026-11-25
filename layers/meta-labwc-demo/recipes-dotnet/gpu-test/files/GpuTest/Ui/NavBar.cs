// The navigation bar at the bottom of every page (design rule of the app):
// Home, Menu, Book and Settings, always in this order.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace GpuTest.Ui
{
    internal enum NavItem { Home, Menu, Book, Settings }

    internal sealed class NavBar : Border
    {
        private readonly Dictionary<NavItem, (Button button, Path icon, TextBlock label, Border mark)> items =
            new Dictionary<NavItem, (Button, Path, TextBlock, Border)>();

        public event Action<NavItem> Selected;

        public NavBar()
        {
            Background = AppTheme.Brush(Color.FromArgb(240, 22, 24, 29));
            BorderBrush = AppTheme.Brush(AppTheme.CardBorder);
            BorderThickness = new Thickness(0, 1, 0, 0);
            Height = 68;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            Add(grid, NavItem.Home, "Home", Icons.Home());
            Add(grid, NavItem.Menu, "Menu", Icons.Menu());
            Add(grid, NavItem.Book, "Library", Icons.Book());
            Add(grid, NavItem.Settings, "Settings", Icons.Gear());
            Child = grid;
        }

        private void Add(Grid grid, NavItem item, string text, Geometry geometry)
        {
            var icon = new Path { Data = geometry, Width = 30, Height = 30, Stretch = Stretch.Uniform };
            var label = new TextBlock { Text = text, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center };
            var stack = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(icon);
            stack.Children.Add(label);
            // Selection mark on top of the bar
            var mark = new Border
            {
                Height = 3,
                Width = 56,
                CornerRadius = new CornerRadius(0, 0, 3, 3),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = AppTheme.Brush(AppTheme.Accent),
                IsVisible = false,
            };
            var content = new Grid();
            content.Children.Add(stack);
            var button = new Button { Content = content, Focusable = false };
            button.Classes.Add("nav");
            button.Click += (_, _) => Selected?.Invoke(item);
            var cell = new Grid();
            cell.Children.Add(button);
            cell.Children.Add(mark);
            Grid.SetColumn(cell, (int)item);
            grid.Children.Add(cell);
            items[item] = (button, icon, label, mark);
        }

        // Menu and Book have no function yet: drawn dimmer
        public void SetActive(NavItem active)
        {
            foreach (KeyValuePair<NavItem, (Button button, Path icon, TextBlock label, Border mark)> kv in items)
            {
                bool on = kv.Key == active;
                bool unused = kv.Key == NavItem.Menu || kv.Key == NavItem.Book;
                Color c = on ? AppTheme.Accent : unused ? Color.FromRgb(96, 101, 112) : AppTheme.Text;
                kv.Value.icon.Fill = AppTheme.Brush(c);
                kv.Value.label.Foreground = AppTheme.Brush(c);
                kv.Value.mark.IsVisible = on;
            }
        }
    }
}
