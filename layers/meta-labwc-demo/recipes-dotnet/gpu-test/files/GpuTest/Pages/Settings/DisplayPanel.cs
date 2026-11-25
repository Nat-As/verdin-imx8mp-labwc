// Display: backlight brightness (sysfs, writable by the video group through
// the argos-settings udev rule), the display outputs (DRM connectors) and the
// screens as the window system reports them.
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform;
using GpuTest.Services;
using GpuTest.Ui;

namespace GpuTest.Pages.Settings
{
    internal sealed class DisplayPanel : SettingsPanel
    {
        private readonly StackPanel brightness = new StackPanel { Spacing = 10 };
        private readonly StackPanel outputs = new StackPanel { Spacing = 6 };
        private readonly StackPanel screens = new StackPanel { Spacing = 6 };

        public DisplayPanel() : base("Display")
        {
            Children.Add(AppTheme.Card("Brightness", brightness));
            Children.Add(AppTheme.Card("Display outputs", outputs));
            Children.Add(AppTheme.Card("Screens (window system)", screens));
        }

        public override void Shown()
        {
            LoadBacklights();
            LoadOutputs();
            LoadScreens();
        }

        private void LoadBacklights()
        {
            brightness.Children.Clear();
            foreach (string name in Sys.List("/sys/class/backlight"))
            {
                string dir = "/sys/class/backlight/" + name;
                long? max = Sys.ReadLong(dir + "/max_brightness");
                long? cur = Sys.ReadLong(dir + "/brightness");
                if (max == null || cur == null || max <= 0)
                    continue;
                var status = new StatusText();
                // Never fully dark: the panel would look switched off
                double min = Math.Max(1, Math.Round(max.Value / 20.0));
                var slider = new Slider
                {
                    Minimum = min,
                    Maximum = max.Value,
                    Value = Math.Max(min, cur.Value),
                    SmallChange = 1,
                    LargeChange = Math.Max(1, max.Value / 10),
                    IsSnapToTickEnabled = max.Value <= 100,
                    TickFrequency = 1,
                    MinHeight = 48,
                };
                var percent = AppTheme.Value(Percent(slider.Value, max.Value));
                percent.Width = 70;
                percent.TextAlignment = Avalonia.Media.TextAlignment.Right;
                slider.ValueChanged += (_, e) =>
                {
                    long v = (long)Math.Round(e.NewValue);
                    percent.Text = Percent(v, max.Value);
                    if (Sys.Write(dir + "/brightness", v.ToString(CultureInfo.InvariantCulture), out string err))
                        status.Clear();
                    else
                        status.Error("Could not set the brightness: " + err +
                            " (the desktop user needs to be in the video group)");
                };
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                row.Children.Add(slider);
                Grid.SetColumn(percent, 1);
                row.Children.Add(percent);
                brightness.Children.Add(AppTheme.Label(Describe(name), AppTheme.Dim, 15));
                brightness.Children.Add(row);
                brightness.Children.Add(status);
            }
            if (brightness.Children.Count == 0)
                brightness.Children.Add(AppTheme.Label(
                    "No display with adjustable backlight. HDMI monitors are adjusted on the monitor.", AppTheme.Dim));
        }

        private static string Percent(double v, long max) =>
            Math.Round(100.0 * v / max).ToString(CultureInfo.InvariantCulture) + " %";

        private static string Describe(string backlight) =>
            backlight.Contains("lvds", StringComparison.OrdinalIgnoreCase) ||
            backlight.Contains("dsi", StringComparison.OrdinalIgnoreCase) || backlight.StartsWith("backlight", StringComparison.Ordinal)
                ? $"Touch display backlight ({backlight})"
                : backlight;

        // DRM connectors: /sys/class/drm/card1-HDMI-A-1/{status,enabled,modes}
        private void LoadOutputs()
        {
            outputs.Children.Clear();
            foreach (string entry in Sys.List("/sys/class/drm", "card*-*"))
            {
                int dash = entry.IndexOf('-');
                string name = entry.Substring(dash + 1);
                if (name.StartsWith("Writeback", StringComparison.Ordinal))
                    continue;
                string dir = "/sys/class/drm/" + entry;
                string status = Sys.Read(dir + "/status") ?? "unknown";
                string enabled = Sys.Read(dir + "/enabled") ?? "";
                string[] modes = Sys.Lines(dir + "/modes");
                string mode = modes.Length > 0 && modes[0].Length > 0 ? modes[0] : "-";
                string text = status + (enabled == "enabled" ? ", on" : enabled == "disabled" ? ", off" : "") +
                    (status == "connected" ? $", preferred mode {mode} ({modes.Length} modes)" : "");
                var value = AppTheme.Value(text);
                value.Foreground = AppTheme.Brush(status == "connected" ? AppTheme.Text : AppTheme.Dim);
                outputs.Children.Add(AppTheme.Row(name, value, 150));
            }
            if (outputs.Children.Count == 0)
                outputs.Children.Add(AppTheme.Label("No display outputs found.", AppTheme.Dim));
            outputs.Children.Add(AppTheme.Label(
                "The 7\" touch display (LVDS) is switched off at login when its touch controller is not detected.",
                AppTheme.Dim, 15));
        }

        private void LoadScreens()
        {
            screens.Children.Clear();
            IReadOnlyList<Screen> all = MainWindow.Current?.Screens?.All;
            if (all == null || all.Count == 0)
            {
                screens.Children.Add(AppTheme.Label("No screen information.", AppTheme.Dim));
                return;
            }
            foreach (Screen s in all)
            {
                string text = $"{s.Bounds.Width}x{s.Bounds.Height}, scale {s.Scaling.ToString("0.##", CultureInfo.InvariantCulture)}" +
                    (s.IsPrimary ? ", primary" : "");
                screens.Children.Add(AppTheme.Row(s.DisplayName ?? "screen", AppTheme.Value(text), 150));
            }
        }
    }
}
