// System: host name, live status (uptime, load, temperature, memory,
// storage), the display session (kiosk or desktop) and power.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using GpuTest.Services;
using GpuTest.Ui;

namespace GpuTest.Pages.Settings
{
    internal sealed class SystemPanel : SettingsPanel
    {
        private readonly TextBlock host = AppTheme.Value(), uptime = AppTheme.Value(), load = AppTheme.Value(),
            temp = AppTheme.Value(), memory = AppTheme.Value(), storage = AppTheme.Value(), session = AppTheme.Value();
        private readonly TextBox newHost = AppTheme.TextField("new host name", 300);
        private readonly StatusText hostStatus = new StatusText(), sessionStatus = new StatusText(),
            powerStatus = new StatusText();
        private readonly DispatcherTimer timer;

        public SystemPanel() : base("System")
        {
            newHost.MaxLength = 63;
            Children.Add(AppTheme.Card("Device",
                AppTheme.Row("Host name", host),
                AppTheme.Row("New host name", newHost),
                AppTheme.Buttons(AppTheme.AsyncButton("Rename", Rename, hostStatus, "accent")),
                hostStatus));
            Children.Add(AppTheme.Card("Status",
                AppTheme.Row("Uptime", uptime),
                AppTheme.Row("Load (1/5/15 min)", load),
                AppTheme.Row("Temperature", temp),
                AppTheme.Row("Memory", memory),
                AppTheme.Row("Storage (/)", storage)));
            Children.Add(AppTheme.Card("Display session",
                AppTheme.Row("Current", session),
                AppTheme.Label("Kiosk: this app full screen on the touch display. Desktop: the labwc desktop " +
                    "with panel and launcher. Switching restarts the display session.", AppTheme.Dim, 15),
                AppTheme.Buttons(
                    AppTheme.AsyncButton("Kiosk", () => SetSession("kiosk"), sessionStatus),
                    AppTheme.AsyncButton("Desktop", () => SetSession("desktop"), sessionStatus)),
                sessionStatus));
            Children.Add(AppTheme.Card("Power",
                AppTheme.Buttons(
                    AppTheme.AsyncButton("Restart", () => Power("reboot", "Restart",
                        "Restart the board now?"), powerStatus),
                    AppTheme.AsyncButton("Power off", () => Power("poweroff", "Power off",
                        "Switch the board off now? It has to be powered on again on the board."), powerStatus,
                        "danger")),
                powerStatus));
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => Update();
        }

        public override void Shown()
        {
            host.Text = Sys.Read("/proc/sys/kernel/hostname") ?? "-";
            string mode = Sys.KeyValues("/etc/default/labwc").Get("LABWC_DEMO_SESSION");
            session.Text = string.IsNullOrEmpty(mode) || mode == "kiosk" ? "Kiosk" : mode == "desktop" ? "Desktop" : mode;
            Update();
            timer.Start();
        }

        public override void Hidden() => timer.Stop();

        private void Update()
        {
            uptime.Text = Uptime();
            string[] l = (Sys.Read("/proc/loadavg") ?? "").Split(' ');
            load.Text = l.Length >= 3 ? $"{l[0]}  {l[1]}  {l[2]}" : "-";
            temp.Text = Temperatures();
            memory.Text = Memory();
            storage.Text = Storage();
        }

        private static string Uptime()
        {
            string s = Sys.Read("/proc/uptime");
            if (s == null || !double.TryParse(s.Split(' ')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double sec))
                return "-";
            var t = TimeSpan.FromSeconds(sec);
            return t.Days > 0 ? $"{t.Days} d {t.Hours} h {t.Minutes} min" : $"{t.Hours} h {t.Minutes} min {t.Seconds} s";
        }

        // Thermal zones (CPU, GPU, ...)
        private static string Temperatures()
        {
            var sb = new StringBuilder();
            foreach (string z in Sys.List("/sys/class/thermal", "thermal_zone*"))
            {
                long? milli = Sys.ReadLong($"/sys/class/thermal/{z}/temp");
                if (milli == null)
                    continue;
                string type = Sys.Read($"/sys/class/thermal/{z}/type") ?? z;
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(type.Replace("-thermal", "")).Append(": ")
                  .Append((milli.Value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" °C");
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private static string Memory()
        {
            long total = 0, avail = 0;
            foreach (string line in Sys.Lines("/proc/meminfo"))
            {
                string[] f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (f.Length < 2 || !long.TryParse(f[1], out long kb))
                    continue;
                if (f[0] == "MemTotal:")
                    total = kb;
                else if (f[0] == "MemAvailable:")
                    avail = kb;
            }
            if (total == 0)
                return "-";
            return $"{Mib(total - avail)} used of {Mib(total)} ({100 * (total - avail) / total} %)";
        }

        private static string Mib(long kb) => kb >= 1024 * 1024
            ? (kb / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " GiB"
            : (kb / 1024) + " MiB";

        private static string Storage()
        {
            try
            {
                var d = new DriveInfo(Sys.Root.Length > 0 ? Sys.Root : "/");
                long used = d.TotalSize - d.AvailableFreeSpace;
                return $"{Mib(used / 1024)} used of {Mib(d.TotalSize / 1024)}, {Mib(d.AvailableFreeSpace / 1024)} free";
            }
            catch (IOException)
            {
                return "-";
            }
        }

        private async Task Rename()
        {
            string name = (newHost.Text ?? "").Trim();
            if (!IsHostname(name))
            {
                hostStatus.Error("Use 1-63 letters, digits and hyphens (not at the start or end)");
                return;
            }
            MainWindow.Current?.Keyboard.Close();
            ShellResult r = await Shell.Helper("hostname", name);
            hostStatus.Result(r.Ok ? null : r.Message, "Host name changed to " + name);
            host.Text = Sys.Read("/proc/sys/kernel/hostname") ?? name;
        }

        public static bool IsHostname(string s)
        {
            if (s.Length < 1 || s.Length > 63 || s[0] == '-' || s[s.Length - 1] == '-')
                return false;
            foreach (char c in s)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-'))
                    return false;
            return true;
        }

        private async Task SetSession(string mode)
        {
            string text = mode == "kiosk"
                ? "Switch to the kiosk session (this app full screen)? The display session restarts."
                : "Switch to the labwc desktop session? The display session restarts and this app closes.";
            if (MainWindow.Current != null &&
                !await MainWindow.Current.Dialogs.Confirm("Display session", text, "Switch"))
                return;
            ShellResult r = await Shell.Helper("session-mode", mode);
            sessionStatus.Result(r.Ok ? null : r.Message, "Switching...");
        }

        private async Task Power(string action, string title, string text)
        {
            if (MainWindow.Current != null &&
                !await MainWindow.Current.Dialogs.Confirm(title, text, title, action == "poweroff"))
                return;
            ShellResult r = await Shell.Helper(action);
            powerStatus.Result(r.Ok ? null : r.Message, title + "...");
        }
    }
}
