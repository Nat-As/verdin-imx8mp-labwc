// Remote desktop (RustDesk) state from argos-remote: the touch screen mirror
// (kiosk) and the unattended admin desktop.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace GpuTest.Services
{
    internal sealed class RemoteSession
    {
        public string Name, Description, User, Id, Password;
        public int Port;
        public bool Running;
    }

    internal sealed class RemoteInfo
    {
        public List<string> Addresses = new List<string>();
        public List<RemoteSession> Sessions = new List<RemoteSession>();
    }

    internal static class Remote
    {
        public const string Command = "argos-remote";

        public static async Task<(RemoteInfo info, string error)> Info()
        {
            ShellResult r = await Shell.Run(Command, "info", "--json");
            if (!r.Ok)
                return (null, r.Message);
            try
            {
                using JsonDocument doc = JsonDocument.Parse(r.Output);
                var info = new RemoteInfo();
                if (doc.RootElement.TryGetProperty("addresses", out JsonElement addrs) &&
                    addrs.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement a in addrs.EnumerateArray())
                        if (a.ValueKind == JsonValueKind.String)
                            info.Addresses.Add(a.GetString());
                }
                if (doc.RootElement.TryGetProperty("sessions", out JsonElement sessions) &&
                    sessions.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement s in sessions.EnumerateArray())
                        info.Sessions.Add(ParseSession(s));
                }
                return (info, null);
            }
            catch (JsonException e)
            {
                return (null, "unexpected output of argos-remote: " + e.Message);
            }
        }

        // New random password for a session (only the caller's own one)
        public static async Task<(RemoteSession session, string error)> NewPassword(string name)
        {
            ShellResult r = await Shell.Run(Command, "password", name, "--random");
            if (!r.Ok)
                return (null, r.Message);
            try
            {
                using JsonDocument doc = JsonDocument.Parse(r.Output);
                return (ParseSession(doc.RootElement), null);
            }
            catch (JsonException e)
            {
                return (null, "unexpected output of argos-remote: " + e.Message);
            }
        }

        private static RemoteSession ParseSession(JsonElement e)
        {
            return new RemoteSession
            {
                Name = Str(e, "name"),
                Description = Str(e, "description"),
                User = Str(e, "user"),
                Id = Str(e, "id"),
                Password = Str(e, "password"),
                Port = e.TryGetProperty("port", out JsonElement p) && p.ValueKind == JsonValueKind.Number &&
                    p.TryGetInt32(out int port) ? port : 0,
                Running = e.TryGetProperty("running", out JsonElement run) && run.ValueKind == JsonValueKind.True,
            };
        }

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
