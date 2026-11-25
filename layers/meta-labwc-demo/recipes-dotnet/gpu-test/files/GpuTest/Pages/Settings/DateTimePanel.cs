// Date & Time: the clock, the real-time clock (RTC), automatic time (NTP), the
// time zone and setting the date and time. Reading uses timedatectl; changes
// go through the settings helper (root).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using GpuTest.Services;
using GpuTest.Ui;

namespace GpuTest.Pages.Settings
{
    internal sealed class DateTimePanel : SettingsPanel
    {
        private readonly TextBlock local = AppTheme.Value(), utc = AppTheme.Value(), zone = AppTheme.Value(),
            synced = AppTheme.Value(), rtc = AppTheme.Value();
        private readonly ToggleSwitch ntp = new ToggleSwitch { OnContent = "On", OffContent = "Off" };
        private readonly StatusText clockStatus = new StatusText(), zoneStatus = new StatusText(),
            setStatus = new StatusText();
        private readonly ComboBox region = new ComboBox { Width = 260 }, city = new ComboBox { Width = 300 };
        private readonly TextBox date = AppTheme.TextField("YYYY-MM-DD", 200), time = AppTheme.TextField("HH:MM:SS", 200);
        private readonly SortedDictionary<string, List<string>> zones =
            new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly DispatcherTimer timer;
        private string currentZone = "";
        private bool settingNtp;

        public DateTimePanel() : base("Date & Time")
        {
            Children.Add(AppTheme.Card("Clock",
                AppTheme.Row("Local time", local),
                AppTheme.Row("UTC", utc),
                AppTheme.Row("Time zone", zone),
                AppTheme.Row("Automatic time", ntp),
                AppTheme.Row("Synchronized", synced),
                AppTheme.Row("Hardware clock", rtc),
                AppTheme.Buttons(
                    AppTheme.AsyncButton("Read RTC", ReadRtc, clockStatus),
                    AppTheme.AsyncButton("Save time to RTC", SyncRtc, clockStatus)),
                clockStatus));

            region.SelectionChanged += (_, _) => FillCities(null);
            Children.Add(AppTheme.Card("Time zone",
                AppTheme.Row("Region", region),
                AppTheme.Row("City", city),
                AppTheme.Buttons(AppTheme.AsyncButton("Apply", SetZone, zoneStatus, "accent")),
                zoneStatus));

            Children.Add(AppTheme.Card("Set date and time",
                AppTheme.Label("Setting the time switches automatic time (NTP) off. The hardware clock is set too.",
                    AppTheme.Dim, 15),
                AppTheme.Row("Date", date),
                AppTheme.Row("Time", time),
                AppTheme.Buttons(
                    AppTheme.Button("Now", FillNow),
                    AppTheme.AsyncButton("Set", SetTime, setStatus, "accent")),
                setStatus));

            ntp.IsCheckedChanged += async (_, _) =>
            {
                if (settingNtp)
                    return;
                ShellResult r = await Shell.Helper("ntp", ntp.IsChecked == true ? "on" : "off");
                clockStatus.Result(r.Ok ? null : r.Message, ntp.IsChecked == true
                    ? "Automatic time switched on" : "Automatic time switched off");
                await Load();
            };
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => Tick();
            LoadZones();
        }

        public override void Shown()
        {
            FillNow();
            Tick();
            timer.Start();
            _ = Load();
        }

        public override void Hidden() => timer.Stop();

        private void Tick()
        {
            DateTime now = DateTime.Now;
            local.Text = now.ToString("yyyy-MM-dd  HH:mm:ss", CultureInfo.InvariantCulture);
            utc.Text = now.ToUniversalTime().ToString("yyyy-MM-dd  HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private async Task Load()
        {
            ShellResult r = await Shell.Run("timedatectl", "show");
            var v = new Dictionary<string, string>(StringComparer.Ordinal);
            if (r.Ok)
            {
                foreach (string line in r.Output.Split('\n'))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                        v[line.Substring(0, eq)] = line.Substring(eq + 1).Trim();
                }
            }
            else
            {
                clockStatus.Error("timedatectl: " + r.Message);
            }
            currentZone = v.Get("Timezone") ?? ZoneFromLocaltime();
            zone.Text = currentZone.Length > 0 ? currentZone : "-";
            settingNtp = true;
            ntp.IsChecked = v.Get("NTP") == "yes";
            ntp.IsEnabled = v.Get("CanNTP") != "no";
            settingNtp = false;
            synced.Text = v.Get("NTPSynchronized") == "yes" ? "yes" : "no";
            string rtcText = v.Get("RTCTimeUSec");
            rtc.Text = string.IsNullOrEmpty(rtcText) || rtcText == "n/a" ? "not available (tap Read RTC)" :
                rtcText + (v.Get("LocalRTC") == "yes" ? " (RTC in local time)" : "");
            SelectZone(currentZone);
        }

        // /etc/localtime -> /usr/share/zoneinfo/Europe/Berlin
        private static string ZoneFromLocaltime()
        {
            try
            {
                var fi = new System.IO.FileInfo(Sys.Root + "/etc/localtime");
                string target = fi.LinkTarget ?? "";
                int i = target.IndexOf("zoneinfo/", StringComparison.Ordinal);
                return i >= 0 ? target.Substring(i + 9) : "";
            }
            catch (System.IO.IOException)
            {
                return "";
            }
        }

        // Zones from zone1970.tab (one per country region), plus UTC
        private void LoadZones()
        {
            var all = new SortedSet<string>(StringComparer.Ordinal) { "UTC" };
            foreach (string tab in new[] { "/usr/share/zoneinfo/zone1970.tab", "/usr/share/zoneinfo/zone.tab" })
            {
                foreach (string line in Sys.Lines(tab))
                {
                    if (line.StartsWith("#", StringComparison.Ordinal))
                        continue;
                    string[] f = line.Split('\t');
                    if (f.Length >= 3 && f[2].Contains('/'))
                        all.Add(f[2].Trim());
                }
                if (all.Count > 1)
                    break;
            }
            foreach (string z in all)
            {
                int slash = z.IndexOf('/');
                string r = slash < 0 ? "UTC" : z.Substring(0, slash);
                if (!zones.TryGetValue(r, out List<string> list))
                    zones[r] = list = new List<string>();
                list.Add(slash < 0 ? z : z.Substring(slash + 1));
            }
            foreach (string r in zones.Keys)
                region.Items.Add(r);
        }

        private void FillCities(string select)
        {
            city.Items.Clear();
            if (region.SelectedItem is not string r || !zones.TryGetValue(r, out List<string> list))
                return;
            int index = 0;
            for (int i = 0; i < list.Count; i++)
            {
                city.Items.Add(list[i].Replace('_', ' '));
                if (list[i] == select)
                    index = i;
            }
            city.SelectedIndex = list.Count > 0 ? index : -1;
        }

        private void SelectZone(string z)
        {
            if (z == "Etc/UTC" || z == "Universal" || z == "Etc/Universal" || z.Length == 0)
                z = "UTC";
            int slash = z.IndexOf('/');
            string r = slash < 0 ? "UTC" : z.Substring(0, slash);
            region.SelectedItem = zones.ContainsKey(r) ? r : null;
            FillCities(slash < 0 ? z : z.Substring(slash + 1));
        }

        private string SelectedZone()
        {
            if (region.SelectedItem is not string r || !zones.TryGetValue(r, out List<string> list) ||
                city.SelectedIndex < 0 || city.SelectedIndex >= list.Count)
                return null;
            return r == "UTC" ? list[city.SelectedIndex] : r + "/" + list[city.SelectedIndex];
        }

        private async Task SetZone()
        {
            string z = SelectedZone();
            if (z == null)
            {
                zoneStatus.Error("Select a region and a city");
                return;
            }
            ShellResult r = await Shell.Helper("timezone", z);
            zoneStatus.Result(r.Ok ? null : r.Message, "Time zone set to " + z);
            // The app keeps the zone it started with: reload it
            TimeZoneInfo.ClearCachedData();
            await Load();
            Tick();
        }

        private void FillNow()
        {
            DateTime now = DateTime.Now;
            date.Text = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            time.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private async Task SetTime()
        {
            string d = (date.Text ?? "").Trim(), t = (time.Text ?? "").Trim();
            if (t.Length == 5)
                t += ":00";
            if (!DateTime.TryParseExact(d + " " + t, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
            {
                setStatus.Error("Enter the date as YYYY-MM-DD and the time as HH:MM:SS");
                return;
            }
            MainWindow.Current?.Keyboard.Close();
            ShellResult r = await Shell.Helper("time-set", d + " " + t);
            setStatus.Result(r.Ok ? null : r.Message, "Date and time set");
            await Load();
            Tick();
        }

        private async Task ReadRtc()
        {
            ShellResult r = await Shell.Helper("rtc-read");
            if (r.Ok)
            {
                rtc.Text = r.Output.Trim();
                clockStatus.Clear();
            }
            else
            {
                clockStatus.Error("Read RTC: " + r.Message);
            }
        }

        private async Task SyncRtc()
        {
            ShellResult r = await Shell.Helper("rtc-sync");
            clockStatus.Result(r.Ok ? null : r.Message, "System time saved to the hardware clock");
            await Load();
        }
    }
}
