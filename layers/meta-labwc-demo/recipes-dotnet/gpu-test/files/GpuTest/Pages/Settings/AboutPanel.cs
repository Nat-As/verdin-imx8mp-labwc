// About: build information of the image, the kernel, the hardware and the
// software versions.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using GpuTest.Services;
using GpuTest.Ui;

namespace GpuTest.Pages.Settings
{
    internal sealed class AboutPanel : SettingsPanel
    {
        private readonly StackPanel image = new StackPanel { Spacing = 4 }, kernel = new StackPanel { Spacing = 4 },
            hardware = new StackPanel { Spacing = 4 }, graphics = new StackPanel { Spacing = 4 },
            software = new StackPanel { Spacing = 4 };
        private readonly TextBlock labwc = AppTheme.Value("..."), java = AppTheme.Value("..."), python = AppTheme.Value("...");
        private bool loaded;

        public AboutPanel() : base("About")
        {
            Children.Add(AppTheme.Card("Image", image));
            Children.Add(AppTheme.Card("Kernel", kernel));
            Children.Add(AppTheme.Card("Hardware", hardware));
            Children.Add(AppTheme.Card("Graphics", graphics));
            Children.Add(AppTheme.Card("Software", software));
        }

        public override void Shown()
        {
            Fill();
            if (!loaded)
            {
                loaded = true;
                _ = LoadVersions();
            }
        }

        private static void Rows(StackPanel p, params (string label, string value)[] rows)
        {
            p.Children.Clear();
            foreach ((string label, string value) in rows)
                if (!string.IsNullOrEmpty(value))
                    p.Children.Add(AppTheme.Row(label, AppTheme.Value(value)));
        }

        private void Fill()
        {
            Dictionary<string, string> argos = Sys.KeyValues("/etc/argos-release");
            Dictionary<string, string> os = Sys.KeyValues("/etc/os-release");
            Dictionary<string, string> build = Sys.KeyValues("/etc/buildinfo");
            string layer = null;
            foreach (string line in Sys.Lines("/etc/buildinfo"))
                if (line.StartsWith("meta-labwc-demo", StringComparison.Ordinal))
                    layer = line.Substring(line.IndexOf('=') + 1).Trim();
            Rows(image,
                ("Image", Join(argos.Get("NAME"), argos.Get("VERSION"))),
                ("Distribution", os.Get("PRETTY_NAME") ?? os.Get("NAME")),
                ("Build", build.Get("DISTRO_VERSION")),
                ("Yocto release", os.Get("VERSION_CODENAME")),
                ("Layer revision", layer));

            Rows(kernel,
                ("Release", Sys.Read("/proc/sys/kernel/osrelease")),
                ("Build", Sys.Read("/proc/sys/kernel/version")),
                ("Architecture", RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()),
                ("Command line", Sys.Read("/proc/cmdline")));

            string soc = Join(Sys.Read("/sys/devices/soc0/family"), Sys.Read("/sys/devices/soc0/soc_id"));
            string rev = Sys.Read("/sys/devices/soc0/revision");
            Rows(hardware,
                ("Model", Sys.ReadDt("/proc/device-tree/model")),
                ("Serial number", Sys.ReadDt("/proc/device-tree/serial-number")),
                ("Toradex product", Join(Sys.ReadDt("/proc/device-tree/toradex,product-id"),
                    Sys.ReadDt("/proc/device-tree/toradex,board-rev"))),
                ("SoC", soc + (rev != null ? " rev " + rev : "")),
                ("CPU", Cpu()),
                ("Memory", MemTotal()));

            Rows(graphics,
                ("GPU driver", GalcoreVersion()),
                ("OpenGL ES", MoleculeView.GlDescription ?? "open the Home page once to detect"));
            graphics.Children.Add(AppTheme.Row("Compositor", labwc));

            Rows(software,
                (".NET", RuntimeInformation.FrameworkDescription),
                ("Avalonia", typeof(Application).Assembly.GetName().Version?.ToString(3)),
                ("App", "GPU Test " + typeof(AboutPanel).Assembly.GetName().Version?.ToString(3)));
            software.Children.Add(AppTheme.Row("Java", java));
            software.Children.Add(AppTheme.Row("Python", python));
        }

        private async Task LoadVersions()
        {
            ShellResult r = await Shell.Run("labwc", "--version");
            labwc.Text = r.Ok ? FirstLine(r.Output) : "-";
            r = await Shell.Run("java", "-version");
            // java prints its version on stderr: openjdk version "25.0.5" 2026-10-20 LTS
            java.Text = r.Ok ? FirstLine(r.Error.Length > 0 ? r.Error : r.Output) : "not installed";
            r = await Shell.Run("python3", "--version");
            python.Text = r.Ok ? FirstLine(r.Output.Length > 0 ? r.Output : r.Error) : "not installed";
        }

        private static string FirstLine(string s)
        {
            s = s.Trim();
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s.Substring(0, nl).Trim();
        }

        private static string Join(string a, string b) =>
            a == null ? b : b == null ? a : a + " " + b;

        // "4 x Cortex-A53, 1600 MHz" from /proc/cpuinfo (arm64 has no model name)
        private static string Cpu()
        {
            int cores = 0;
            string part = null;
            foreach (string line in Sys.Lines("/proc/cpuinfo"))
            {
                if (line.StartsWith("processor", StringComparison.Ordinal))
                    cores++;
                else if (line.StartsWith("CPU part", StringComparison.Ordinal))
                    part = line.Substring(line.IndexOf(':') + 1).Trim();
                else if (line.StartsWith("model name", StringComparison.Ordinal) && part == null)
                    part = line.Substring(line.IndexOf(':') + 1).Trim();
            }
            if (cores == 0)
                cores = Environment.ProcessorCount;
            string name = part switch
            {
                "0xd03" => "Cortex-A53",
                "0xd04" => "Cortex-A35",
                "0xd05" => "Cortex-A55",
                "0xd08" => "Cortex-A72",
                "0xd0b" => "Cortex-A76",
                null => RuntimeInformation.ProcessArchitecture.ToString(),
                _ => part,
            };
            long? khz = Sys.ReadLong("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq");
            return $"{cores} x {name}" + (khz != null ? $", up to {khz / 1000} MHz" : "");
        }

        private static string MemTotal()
        {
            foreach (string line in Sys.Lines("/proc/meminfo"))
            {
                string[] f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (f.Length >= 2 && f[0] == "MemTotal:" && long.TryParse(f[1], out long kb))
                    return (kb / 1024) + " MiB";
            }
            return null;
        }

        // Vivante galcore kernel driver
        private static string GalcoreVersion()
        {
            string v = Sys.Read("/sys/module/galcore/version");
            if (v != null)
                return "Vivante galcore " + v;
            return Sys.Exists("/sys/module/galcore") ? "Vivante galcore (loaded)" : "galcore not loaded";
        }
    }
}
