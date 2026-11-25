// Remote Desktop: how to reach the board with RustDesk (direct IP access):
// the touch screen mirror and the unattended admin desktop, from argos-remote.
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using GpuTest.Services;
using GpuTest.Ui;

namespace GpuTest.Pages.Settings
{
    internal sealed class RemotePanel : SettingsPanel
    {
        private readonly StackPanel sessions = new StackPanel();
        private readonly StatusText status = new StatusText();

        public RemotePanel() : base("Remote Desktop")
        {
            HeaderButton(AppTheme.AsyncButton("Refresh", Load, status));
            Children.Add(AppTheme.Label(
                "Connect with RustDesk: enter <address>:<port> as the remote ID (direct IP access) and the password.",
                AppTheme.Dim, 15));
            Children.Add(new Border { Height = 10 });
            Children.Add(status);
            Children.Add(sessions);
        }

        public override void Shown() => _ = Load();

        private async Task Load()
        {
            (RemoteInfo info, string error) = await Remote.Info();
            sessions.Children.Clear();
            if (info == null)
            {
                status.Error("Remote desktop service not available" + (error != null ? ": " + error : ""));
                return;
            }
            status.Clear();
            foreach (RemoteSession s in info.Sessions)
                sessions.Children.Add(SessionCard(s, info));
            if (info.Sessions.Count == 0)
                sessions.Children.Add(AppTheme.Card(null, AppTheme.Label("No remote desktop sessions configured.", AppTheme.Dim)));
        }

        private Control SessionCard(RemoteSession s, RemoteInfo info)
        {
            var running = AppTheme.Value(s.Running ? "running" : "not running");
            running.Foreground = AppTheme.Brush(s.Running ? AppTheme.Good : AppTheme.Bad);
            string addresses = info.Addresses.Count == 0
                ? "no network address"
                : string.Join("\n", info.Addresses.ConvertAll(a => a + ":" + s.Port));
            var cardStatus = new StatusText();
            var card = new System.Collections.Generic.List<Control>
            {
                AppTheme.Row("Status", running),
                AppTheme.Row("Connect to", AppTheme.Value(addresses)),
                AppTheme.Row("RustDesk ID", AppTheme.Value(string.IsNullOrEmpty(s.Id) ? "-" : s.Id)),
                AppTheme.Row("User", AppTheme.Value(s.User ?? "-")),
            };
            if (s.Password != null)
            {
                var pw = AppTheme.Value("••••••••");
                bool shown = false;
                string password = s.Password;
                Button toggle = null;
                toggle = AppTheme.Button("Show", () =>
                {
                    shown = !shown;
                    pw.Text = shown ? password : "••••••••";
                    toggle.Content = shown ? "Hide" : "Show";
                });
                var newPw = AppTheme.AsyncButton("New password", async () =>
                {
                    if (MainWindow.Current != null && !await MainWindow.Current.Dialogs.Confirm("New password",
                            "Generate a new remote desktop password? Connected viewers keep their session; " +
                            "new connections need the new password.", "Generate"))
                        return;
                    (RemoteSession updated, string err) = await Remote.NewPassword(s.Name);
                    if (err != null || updated?.Password == null)
                    {
                        cardStatus.Error(err ?? "no password returned");
                        return;
                    }
                    password = updated.Password;
                    shown = true;
                    pw.Text = password;
                    toggle.Content = "Hide";
                    cardStatus.Ok("New password set");
                }, cardStatus);
                card.Add(AppTheme.Row("Password", pw));
                card.Add(AppTheme.Buttons(toggle, newPw));
            }
            else
            {
                card.Add(AppTheme.Row("Password", AppTheme.Label("ask the administrator", AppTheme.Dim)));
            }
            card.Add(cardStatus);
            string title = s.Description ?? s.Name;
            return AppTheme.Card(title, card.ToArray());
        }
    }
}
