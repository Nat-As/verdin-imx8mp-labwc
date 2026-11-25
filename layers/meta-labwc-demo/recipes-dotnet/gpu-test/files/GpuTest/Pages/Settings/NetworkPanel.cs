// Network: Ethernet and Wi-Fi devices (state, addresses, gateway, DNS), the
// IPv4 settings of their connection (DHCP, static, link-local, disabled) and
// Wi-Fi networks (scan, connect, forget). NetworkManager through nmcli.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GpuTest.Services;
using GpuTest.Ui;

namespace GpuTest.Pages.Settings
{
    internal sealed class NetworkPanel : SettingsPanel
    {
        private static readonly (string method, string label)[] Methods =
        {
            ("auto", "DHCP (automatic)"),
            ("manual", "Static (manual)"),
            ("link-local", "Link-local only"),
            ("disabled", "Disabled"),
        };

        private readonly StackPanel devices = new StackPanel();
        private readonly StackPanel wifiList = new StackPanel { Spacing = 6 };
        private readonly StatusText status = new StatusText();
        private readonly StatusText wifiStatus = new StatusText();
        private readonly ToggleSwitch wifiRadio = new ToggleSwitch { OnContent = "On", OffContent = "Off" };
        private readonly Border wifiCard;
        private bool loading, settingRadio;
        // The Wi-Fi network whose connect form is open
        private string openSsid;

        public NetworkPanel() : base("Network")
        {
            HeaderButton(AppTheme.AsyncButton("Refresh", () => Load(false), status));
            Children.Add(status);
            Children.Add(devices);

            wifiRadio.IsCheckedChanged += async (_, _) =>
            {
                if (settingRadio)
                    return;
                string err = await Network.SetWifiRadio(wifiRadio.IsChecked == true);
                wifiStatus.Result(err, wifiRadio.IsChecked == true ? "Wi-Fi switched on" : "Wi-Fi switched off");
                await Task.Delay(1500);
                await Load(false);
            };
            var radioRow = AppTheme.Row("Wi-Fi radio", wifiRadio);
            var scan = AppTheme.AsyncButton("Scan", () => Load(true), wifiStatus);
            var hidden = AppTheme.Button("Hidden network...", () => ShowConnectForm(null, null, true));
            wifiCard = AppTheme.Card("Wi-Fi networks", radioRow, AppTheme.Buttons(scan, hidden), wifiStatus, wifiList);
            Children.Add(wifiCard);
        }

        public override void Shown() => _ = Load(false);

        private async Task Load(bool rescan)
        {
            if (loading)
                return;
            loading = true;
            try
            {
                if (rescan)
                    wifiStatus.Info("Scanning...");
                (List<NetDevice> list, string error) = await Network.Devices();
                if (error != null)
                    status.Error(error);
                else
                    status.Clear();
                devices.Children.Clear();
                foreach (NetDevice d in list)
                    devices.Children.Add(DeviceCard(d));
                if (list.Count == 0 && error == null)
                    devices.Children.Add(AppTheme.Card(null, AppTheme.Label("No Ethernet or Wi-Fi device found.", AppTheme.Dim)));

                bool hasWifi = list.Exists(d => d.IsWifi);
                wifiCard.IsVisible = hasWifi || error != null;
                if (!hasWifi)
                    return;
                bool? radio = await Network.WifiRadio();
                settingRadio = true;
                wifiRadio.IsChecked = radio == true;
                settingRadio = false;
                wifiList.Children.Clear();
                if (radio != true)
                {
                    wifiList.Children.Add(AppTheme.Label("Switch the Wi-Fi radio on to see networks.", AppTheme.Dim));
                    return;
                }
                (List<WifiNetwork> nets, string werr) = await Network.WifiNetworks(rescan);
                if (werr != null)
                    wifiStatus.Error(werr);
                else if (rescan)
                    wifiStatus.Info(nets.Count + " networks found");
                foreach (WifiNetwork n in nets)
                    wifiList.Children.Add(NetworkRow(n));
                if (nets.Count == 0 && werr == null)
                    wifiList.Children.Add(AppTheme.Label("No networks found (tap Scan).", AppTheme.Dim));
            }
            finally
            {
                loading = false;
            }
        }

        private Control DeviceCard(NetDevice d)
        {
            string kind = d.IsWifi ? "Wi-Fi" : "Ethernet";
            var state = AppTheme.Value(d.State + (d.Connection.Length > 0 ? " (" + d.Connection + ")" : ""));
            state.Foreground = AppTheme.Brush(d.IsConnected ? AppTheme.Good : AppTheme.Dim);
            var rows = new List<Control>
            {
                AppTheme.Row("State", state),
                AppTheme.Row("IPv4 address", AppTheme.Value(d.Addresses.Count > 0 ? string.Join("\n", d.Addresses) : "-")),
                AppTheme.Row("Gateway", AppTheme.Value(d.Gateway.Length > 0 ? d.Gateway : "-")),
                AppTheme.Row("DNS servers", AppTheme.Value(d.Dns.Count > 0 ? string.Join(", ", d.Dns) : "-")),
                AppTheme.Row("MAC address", AppTheme.Value(d.Mac.Length > 0 ? d.Mac : "-")),
            };
            if (d.Speed.Length > 0)
                rows.Add(AppTheme.Row("Link speed", AppTheme.Value(d.Speed)));

            var cardStatus = new StatusText();
            var editorHost = new StackPanel();
            var buttons = new List<Control>();
            if (d.Uuid.Length > 0)
            {
                Button edit = null;
                edit = AppTheme.Button("IPv4 settings", async () =>
                {
                    if (editorHost.Children.Count > 0)
                    {
                        editorHost.Children.Clear();
                        return;
                    }
                    edit.IsEnabled = false;
                    (Ipv4Config c, string err) = await Network.GetIpv4(d.Uuid);
                    edit.IsEnabled = true;
                    if (err != null)
                        cardStatus.Error(err);
                    else
                        editorHost.Children.Add(Ipv4Editor(d, c, cardStatus));
                });
                buttons.Add(edit);
            }
            if (d.IsConnected)
                buttons.Add(AppTheme.AsyncButton("Disconnect", async () =>
                {
                    cardStatus.Info("Disconnecting...");
                    cardStatus.Result(await Network.DeviceConnect(d.Name, false), "Disconnected");
                    await Load(false);
                }, cardStatus));
            else if (!d.State.StartsWith("unavailable", StringComparison.Ordinal) && !d.State.StartsWith("unmanaged", StringComparison.Ordinal))
                buttons.Add(AppTheme.AsyncButton("Connect", async () =>
                {
                    cardStatus.Info("Connecting...");
                    cardStatus.Result(await Network.DeviceConnect(d.Name, true), "Connected");
                    await Load(false);
                }, cardStatus));
            if (buttons.Count > 0)
                rows.Add(AppTheme.Buttons(buttons.ToArray()));
            rows.Add(cardStatus);
            rows.Add(editorHost);
            return AppTheme.Card($"{kind} ({d.Name})", rows.ToArray());
        }

        // IPv4 settings form of a connection
        private Control Ipv4Editor(NetDevice d, Ipv4Config c, StatusText cardStatus)
        {
            var method = new ComboBox { Width = 280 };
            int sel = 0;
            for (int i = 0; i < Methods.Length; i++)
            {
                method.Items.Add(new ComboBoxItem { Content = Methods[i].label });
                if (Methods[i].method == c.Method)
                    sel = i;
            }
            method.SelectedIndex = sel;
            var address = AppTheme.TextField("192.168.1.50/24", 280);
            address.Text = c.Address;
            var gateway = AppTheme.TextField("192.168.1.1", 280);
            gateway.Text = c.Gateway;
            var dns = AppTheme.TextField("8.8.8.8, 1.1.1.1", 280);
            dns.Text = c.Dns;
            var dnsOnly = new CheckBox { Content = "Use only these DNS servers", IsChecked = c.IgnoreAutoDns };
            var addressRow = AppTheme.Row("Address/prefix", address);
            var gatewayRow = AppTheme.Row("Gateway", gateway);
            var dnsRow = AppTheme.Row("DNS servers", dns);
            void Update()
            {
                string m = Methods[Math.Max(0, method.SelectedIndex)].method;
                addressRow.IsVisible = gatewayRow.IsVisible = m == "manual";
                dnsRow.IsVisible = m == "auto" || m == "manual";
                dnsOnly.IsVisible = m == "auto";
            }
            method.SelectionChanged += (_, _) => Update();
            Update();

            var apply = AppTheme.AsyncButton("Apply", async () =>
            {
                var cfg = new Ipv4Config
                {
                    Method = Methods[Math.Max(0, method.SelectedIndex)].method,
                    Address = (address.Text ?? "").Trim(),
                    Gateway = (gateway.Text ?? "").Trim(),
                    Dns = (dns.Text ?? "").Replace(" ", "").Trim(','),
                    IgnoreAutoDns = dnsOnly.IsChecked == true,
                };
                string invalid = Validate(cfg);
                if (invalid != null)
                {
                    cardStatus.Error(invalid);
                    return;
                }
                MainWindow.Current?.Keyboard.Close();
                cardStatus.Info("Applying, the connection restarts...");
                string err = await Network.SetIpv4(d.Uuid, cfg);
                cardStatus.Result(err, "IPv4 settings applied");
                if (err == null)
                    await Load(false);
            }, cardStatus, "accent");

            var box = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
            box.Children.Add(new Border { Height = 1, Background = AppTheme.Brush(AppTheme.CardBorder) });
            box.Children.Add(AppTheme.Row("IPv4 method", method));
            box.Children.Add(addressRow);
            box.Children.Add(gatewayRow);
            box.Children.Add(dnsRow);
            box.Children.Add(dnsOnly);
            box.Children.Add(AppTheme.Buttons(apply));
            return box;
        }

        private static string Validate(Ipv4Config c)
        {
            if (c.Method == "manual")
            {
                if (!c.Address.Contains('/'))
                    c.Address += "/24";
                if (!Network.IsCidr(c.Address))
                    return "Address: enter an IPv4 address with prefix, e.g. 192.168.1.50/24";
                if (c.Gateway.Length > 0 && !Network.IsIpv4(c.Gateway))
                    return "Gateway: enter an IPv4 address, e.g. 192.168.1.1";
            }
            if (c.Dns.Length > 0)
            {
                foreach (string s in c.Dns.Split(','))
                    if (!Network.IsIpv4(s))
                        return "DNS: enter IPv4 addresses separated by commas";
            }
            return null;
        }

        // A Wi-Fi network: tap to open the connect form below it
        private Control NetworkRow(WifiNetwork n)
        {
            var name = AppTheme.Label(n.Ssid);
            name.FontWeight = n.InUse ? FontWeight.SemiBold : FontWeight.Normal;
            string detail = n.Signal + " %  " + (n.Secured ? n.Security : "open") +
                (n.InUse ? "  connected" : n.SavedUuid != null ? "  saved" : "");
            var info = AppTheme.Label(detail, n.InUse ? AppTheme.Good : AppTheme.Dim, 15);
            var bars = new SignalBars(n.Signal);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var text = new StackPanel { Spacing = 0, Margin = new Thickness(14, 0, 0, 0) };
            text.Children.Add(name);
            text.Children.Add(info);
            grid.Children.Add(bars);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            var row = new Button
            {
                Content = grid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Focusable = false,
                MinHeight = 56,
            };
            var form = new StackPanel();
            var wrap = new StackPanel { Spacing = 4 };
            wrap.Children.Add(row);
            wrap.Children.Add(form);
            row.Click += (_, _) =>
            {
                if (openSsid == n.Ssid && form.Children.Count > 0)
                {
                    form.Children.Clear();
                    openSsid = null;
                    return;
                }
                CloseForms();
                openSsid = n.Ssid;
                Control f = ConnectForm(n, false);
                form.Children.Add(f);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => f.BringIntoView(),
                    Avalonia.Threading.DispatcherPriority.Background);
            };
            if (openSsid == n.Ssid)
                form.Children.Add(ConnectForm(n, false));
            return wrap;
        }

        private void CloseForms()
        {
            foreach (Control c in wifiList.Children)
                if (c is StackPanel wrap && wrap.Children.Count > 1 && wrap.Children[1] is StackPanel form)
                    form.Children.Clear();
            if (wifiList.Children.Count > 0 && wifiList.Children[0] is Border b && b.Tag as string == "hidden")
                wifiList.Children.RemoveAt(0);
        }

        private void ShowConnectForm(WifiNetwork n, string ssid, bool hidden)
        {
            CloseForms();
            openSsid = null;
            var form = ConnectForm(n ?? new WifiNetwork { Ssid = ssid ?? "", Security = "WPA2" }, hidden);
            var holder = new Border { Tag = "hidden", Child = form };
            wifiList.Children.Insert(0, holder);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => holder.BringIntoView(),
                Avalonia.Threading.DispatcherPriority.Background);
        }

        // Password and Connect (and Forget for saved networks)
        private Control ConnectForm(WifiNetwork n, bool hidden)
        {
            var box = new StackPanel { Spacing = 8, Margin = new Thickness(8, 4, 8, 10) };
            var formStatus = new StatusText();
            TextBox ssid = null;
            if (hidden)
            {
                ssid = AppTheme.TextField("Network name (SSID)", 320);
                box.Children.Add(AppTheme.Row("Network name", ssid, 160));
            }
            TextBox password = null;
            bool needsPassword = hidden || (n.Secured && n.SavedUuid == null);
            if (needsPassword)
            {
                password = AppTheme.TextField(hidden ? "Password (empty if open)" : "Password", 320);
                password.PasswordChar = '•';
                var show = new CheckBox { Content = "Show" };
                show.IsCheckedChanged += (_, _) => password.PasswordChar = show.IsChecked == true ? '\0' : '•';
                var pw = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                pw.Children.Add(password);
                pw.Children.Add(show);
                box.Children.Add(AppTheme.Row("Password", pw, 160));
            }
            var buttons = new List<Control>();
            if (!n.InUse || hidden)
            {
                buttons.Add(AppTheme.AsyncButton("Connect", async () =>
                {
                    string name = hidden ? (ssid.Text ?? "").Trim() : n.Ssid;
                    if (name.Length == 0)
                    {
                        formStatus.Error("Enter the network name");
                        return;
                    }
                    string pass = password?.Text ?? "";
                    if (needsPassword && !hidden && pass.Length < 8)
                    {
                        formStatus.Error("The password has at least 8 characters");
                        return;
                    }
                    MainWindow.Current?.Keyboard.Close();
                    formStatus.Info("Connecting to " + name + "...");
                    string err = n.SavedUuid != null && !hidden
                        ? await Network.ConnectionUp(n.SavedUuid)
                        : await Network.ConnectWifi(name, pass, hidden);
                    formStatus.Result(err, "Connected to " + name);
                    if (err == null)
                    {
                        openSsid = null;
                        await Load(false);
                    }
                }, formStatus, "accent"));
            }
            if (n.InUse && !hidden)
                buttons.Add(AppTheme.AsyncButton("Disconnect", async () =>
                {
                    string err = n.SavedUuid != null
                        ? (await Shell.Run("nmcli", "connection", "down", "uuid", n.SavedUuid)).Ok ? null : "could not disconnect"
                        : "no saved connection";
                    formStatus.Result(err, "Disconnected");
                    if (err == null)
                        await Load(false);
                }, formStatus));
            if (n.SavedUuid != null && !hidden)
                buttons.Add(AppTheme.AsyncButton("Forget", async () =>
                {
                    if (MainWindow.Current != null && !await MainWindow.Current.Dialogs.Confirm("Forget network",
                            $"Delete the saved connection for \"{n.Ssid}\" (with its password)?", "Forget", true))
                        return;
                    string err = await Network.DeleteConnection(n.SavedUuid);
                    formStatus.Result(err, "Forgotten");
                    if (err == null)
                        await Load(false);
                }, formStatus, "danger"));
            if (hidden)
                buttons.Add(AppTheme.Button("Cancel", () => CloseForms()));
            box.Children.Add(AppTheme.Buttons(buttons.ToArray()));
            box.Children.Add(formStatus);
            return new Border
            {
                Background = AppTheme.Brush(Color.FromRgb(28, 31, 38)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8),
                Child = box,
            };
        }
    }

    // Four bars, filled by signal strength
    internal sealed class SignalBars : StackPanel
    {
        public SignalBars(int signal)
        {
            Orientation = Orientation.Horizontal;
            Spacing = 3;
            VerticalAlignment = VerticalAlignment.Center;
            int filled = signal >= 75 ? 4 : signal >= 50 ? 3 : signal >= 25 ? 2 : 1;
            for (int i = 0; i < 4; i++)
                Children.Add(new Border
                {
                    Width = 6,
                    Height = 8 + i * 6,
                    CornerRadius = new CornerRadius(1),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = AppTheme.Brush(i < filled ? AppTheme.Text : Color.FromRgb(70, 74, 84)),
                });
        }
    }
}
