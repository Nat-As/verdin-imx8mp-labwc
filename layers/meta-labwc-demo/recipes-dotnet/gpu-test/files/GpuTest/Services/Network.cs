// NetworkManager through nmcli (terse output). The image runs NetworkManager
// with auth-polkit=false, so the desktop user may change the network without
// sudo.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GpuTest.Services
{
    internal sealed class NetDevice
    {
        public string Name, Type, State, Connection, Uuid;
        public string Mac = "", Gateway = "", Speed = "";
        public List<string> Addresses = new List<string>();
        public List<string> Dns = new List<string>();

        public bool IsWifi => Type == "wifi";
        public bool IsEthernet => Type == "ethernet";
        public bool IsConnected => State.StartsWith("connected", StringComparison.Ordinal);
    }

    // IPv4 settings of a connection profile
    internal sealed class Ipv4Config
    {
        public string Method = "auto";   // auto, manual, link-local, disabled
        public string Address = "";      // first address, CIDR (192.168.1.10/24)
        public string Gateway = "";
        public string Dns = "";          // comma separated
        public bool IgnoreAutoDns;
    }

    internal sealed class WifiNetwork
    {
        public string Ssid, Security;
        public int Signal;
        public bool InUse;
        public string SavedUuid;          // profile for this SSID, if any

        public bool Secured => Security.Length > 0 && Security != "--";
    }

    internal static class Network
    {
        // Splits a terse nmcli line at unescaped ':' and removes the escapes
        // (nmcli -t writes ':' as '\:' and '\' as '\\' inside fields)
        public static List<string> SplitTerse(string line, int maxFields = int.MaxValue)
        {
            var fields = new List<string>();
            var cur = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length)
                {
                    cur.Append(line[++i]);
                }
                else if (c == ':' && fields.Count < maxFields - 1)
                {
                    fields.Add(cur.ToString());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
            fields.Add(cur.ToString());
            return fields;
        }

        // "KEY:value" lines of nmcli -t ... show; keys of multi-value fields
        // (IP4.ADDRESS[1]) lose the index
        private static List<KeyValuePair<string, string>> ShowLines(string output)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (string line in output.Split('\n'))
            {
                if (line.Length == 0)
                    continue;
                List<string> kv = SplitTerse(line, 2);
                if (kv.Count < 2)
                    continue;
                string key = kv[0];
                int bracket = key.IndexOf('[');
                if (bracket > 0)
                    key = key.Substring(0, bracket);
                list.Add(new KeyValuePair<string, string>(key, kv[1].Trim()));
            }
            return list;
        }

        private static async Task<ShellResult> Nmcli(params string[] args)
        {
            ShellResult r = await Shell.Run("nmcli", args);
            if (r.ExitCode == 127 && r.Error.Contains("nmcli"))
                r.Error = "NetworkManager (nmcli) is not available";
            return r;
        }

        public static async Task<(List<NetDevice> devices, string error)> Devices()
        {
            ShellResult r = await Nmcli("-t", "-f", "DEVICE,TYPE,STATE,CONNECTION,CON-UUID", "device", "status");
            if (!r.Ok)
                return (new List<NetDevice>(), r.Message);
            var devices = new List<NetDevice>();
            foreach (string line in r.Output.Split('\n'))
            {
                List<string> f = SplitTerse(line);
                if (f.Count < 5 || (f[1] != "ethernet" && f[1] != "wifi"))
                    continue;
                devices.Add(new NetDevice
                {
                    Name = f[0], Type = f[1], State = f[2],
                    Connection = f[3] == "--" ? "" : f[3],
                    Uuid = f[4] == "--" ? "" : f[4],
                });
            }
            foreach (NetDevice d in devices)
            {
                ShellResult s = await Nmcli("-t", "-f", "GENERAL.HWADDR,IP4.ADDRESS,IP4.GATEWAY,IP4.DNS",
                    "device", "show", d.Name);
                if (!s.Ok)
                    continue;
                foreach (KeyValuePair<string, string> kv in ShowLines(s.Output))
                {
                    if (kv.Value.Length == 0 || kv.Value == "--")
                        continue;
                    switch (kv.Key)
                    {
                        case "GENERAL.HWADDR": d.Mac = kv.Value; break;
                        case "IP4.ADDRESS": d.Addresses.Add(kv.Value); break;
                        case "IP4.GATEWAY": d.Gateway = kv.Value; break;
                        case "IP4.DNS": d.Dns.Add(kv.Value); break;
                    }
                }
                if (d.IsEthernet && d.IsConnected && long.TryParse(Sys.Read($"/sys/class/net/{d.Name}/speed"),
                        out long mbit) && mbit > 0)
                    d.Speed = mbit >= 1000 ? (mbit / 1000) + " Gbit/s" : mbit + " Mbit/s";
            }
            return (devices, null);
        }

        public static async Task<(Ipv4Config config, string error)> GetIpv4(string uuid)
        {
            ShellResult r = await Nmcli("-t", "-f",
                "ipv4.method,ipv4.addresses,ipv4.gateway,ipv4.dns,ipv4.ignore-auto-dns",
                "connection", "show", "uuid", uuid);
            if (!r.Ok)
                return (null, r.Message);
            var c = new Ipv4Config();
            foreach (KeyValuePair<string, string> kv in ShowLines(r.Output))
            {
                string v = kv.Value == "--" ? "" : kv.Value;
                switch (kv.Key)
                {
                    case "ipv4.method": c.Method = v; break;
                    case "ipv4.addresses": c.Address = v.Split(',')[0].Trim(); break;
                    case "ipv4.gateway": c.Gateway = v; break;
                    case "ipv4.dns": c.Dns = v.Replace(" ", ""); break;
                    case "ipv4.ignore-auto-dns": c.IgnoreAutoDns = v == "yes"; break;
                }
            }
            return (c, null);
        }

        // Saves the IPv4 settings and re-activates the connection
        public static async Task<string> SetIpv4(string uuid, Ipv4Config c)
        {
            bool manual = c.Method == "manual";
            var args = new List<string>
            {
                "connection", "modify", "uuid", uuid,
                "ipv4.method", c.Method,
                "ipv4.addresses", manual ? c.Address : "",
                "ipv4.gateway", manual ? c.Gateway : "",
                "ipv4.dns", c.Method == "auto" || manual ? c.Dns : "",
                "ipv4.ignore-auto-dns", c.Method == "auto" && c.IgnoreAutoDns ? "yes" : "no",
            };
            ShellResult r = await Nmcli(args.ToArray());
            if (!r.Ok)
                return r.Message;
            r = await Shell.RunLong(TimeSpan.FromSeconds(60), "nmcli", "connection", "up", "uuid", uuid);
            return r.Ok ? null : r.Message;
        }

        public static async Task<bool?> WifiRadio()
        {
            ShellResult r = await Nmcli("radio", "wifi");
            if (!r.Ok)
                return null;
            return r.Output.Trim() == "enabled";
        }

        public static async Task<string> SetWifiRadio(bool on)
        {
            ShellResult r = await Nmcli("radio", "wifi", on ? "on" : "off");
            return r.Ok ? null : r.Message;
        }

        public static async Task<(List<WifiNetwork> networks, string error)> WifiNetworks(bool rescan)
        {
            var saved = new Dictionary<string, string>(StringComparer.Ordinal);
            ShellResult c = await Nmcli("-t", "-f", "NAME,UUID,TYPE", "connection", "show");
            if (c.Ok)
            {
                foreach (string line in c.Output.Split('\n'))
                {
                    List<string> f = SplitTerse(line);
                    if (f.Count >= 3 && f[2] == "802-11-wireless")
                        saved[f[0]] = f[1];
                }
            }
            ShellResult r = await Shell.RunLong(TimeSpan.FromSeconds(rescan ? 30 : 15), "nmcli", "-t", "-f",
                "IN-USE,SSID,SIGNAL,SECURITY", "device", "wifi", "list", "--rescan", rescan ? "yes" : "auto");
            if (!r.Ok)
                return (new List<WifiNetwork>(), r.Message);
            // One entry per SSID (strongest access point)
            var bySsid = new Dictionary<string, WifiNetwork>(StringComparer.Ordinal);
            foreach (string line in r.Output.Split('\n'))
            {
                List<string> f = SplitTerse(line);
                if (f.Count < 4 || f[1].Length == 0)
                    continue;
                int.TryParse(f[2], out int signal);
                var n = new WifiNetwork { InUse = f[0] == "*", Ssid = f[1], Signal = signal, Security = f[3].Trim() };
                if (bySsid.TryGetValue(n.Ssid, out WifiNetwork old))
                {
                    old.InUse |= n.InUse;
                    if (n.Signal > old.Signal)
                        old.Signal = n.Signal;
                    continue;
                }
                saved.TryGetValue(n.Ssid, out n.SavedUuid);
                bySsid[n.Ssid] = n;
            }
            var list = new List<WifiNetwork>(bySsid.Values);
            list.Sort((a, b) => a.InUse != b.InUse ? (a.InUse ? -1 : 1) : b.Signal.CompareTo(a.Signal));
            return (list, null);
        }

        public static async Task<string> ConnectWifi(string ssid, string password, bool hidden)
        {
            var args = new List<string> { "device", "wifi", "connect", ssid };
            if (!string.IsNullOrEmpty(password))
            {
                args.Add("password");
                args.Add(password);
            }
            if (hidden)
            {
                args.Add("hidden");
                args.Add("yes");
            }
            ShellResult r = await Shell.Runner("nmcli", args, TimeSpan.FromSeconds(60));
            return r.Ok ? null : r.Message;
        }

        public static async Task<string> ConnectionUp(string uuid)
        {
            ShellResult r = await Shell.RunLong(TimeSpan.FromSeconds(60), "nmcli", "connection", "up", "uuid", uuid);
            return r.Ok ? null : r.Message;
        }

        public static async Task<string> DeleteConnection(string uuid)
        {
            ShellResult r = await Nmcli("connection", "delete", "uuid", uuid);
            return r.Ok ? null : r.Message;
        }

        public static async Task<string> DeviceConnect(string device, bool connect)
        {
            ShellResult r = await Shell.RunLong(TimeSpan.FromSeconds(60), "nmcli", "device",
                connect ? "connect" : "disconnect", device);
            return r.Ok ? null : r.Message;
        }

        // Input checks for the IPv4 form
        public static bool IsIpv4(string s)
        {
            string[] parts = s.Split('.');
            if (parts.Length != 4)
                return false;
            foreach (string p in parts)
            {
                if (p.Length == 0 || p.Length > 3 || !int.TryParse(p, out int v) || v > 255 ||
                    (p.Length > 1 && p[0] == '0'))
                    return false;
            }
            return true;
        }

        public static bool IsCidr(string s)
        {
            int slash = s.IndexOf('/');
            if (slash < 0)
                return IsIpv4(s);
            return IsIpv4(s.Substring(0, slash)) && int.TryParse(s.Substring(slash + 1), out int prefix) &&
                prefix >= 1 && prefix <= 32;
        }
    }
}
