// Runs system commands (nmcli, timedatectl, the settings helper, ...) without
// blocking the UI thread: asynchronous, with a timeout, output captured.
// Runner can be replaced (the screenshot harness feeds sample output).
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GpuTest.Services
{
    internal sealed class ShellResult
    {
        public int ExitCode;
        public string Output = "";
        public string Error = "";

        public bool Ok => ExitCode == 0;

        // What to show the user when the command failed
        public string Message
        {
            get
            {
                string text = Error.Trim().Length > 0 ? Error.Trim() : Output.Trim();
                if (text.StartsWith("Error: ", StringComparison.Ordinal))
                    text = text.Substring(7);
                return text.Length > 0 ? text : "failed (exit code " + ExitCode + ")";
            }
        }

        public static ShellResult Failed(string message) =>
            new ShellResult { ExitCode = 127, Error = message };
    }

    internal static class Shell
    {
        public const string HelperPath = "/usr/libexec/argos/argos-settings-helper";

        public static Func<string, IReadOnlyList<string>, TimeSpan, Task<ShellResult>> Runner = RunProcess;

        public static Task<ShellResult> Run(string file, params string[] args) =>
            Runner(file, args, TimeSpan.FromSeconds(20));

        public static Task<ShellResult> RunLong(TimeSpan timeout, string file, params string[] args) =>
            Runner(file, args, timeout);

        // The privileged settings helper (sudo rule in /etc/sudoers.d/argos-settings)
        public static Task<ShellResult> Helper(params string[] args)
        {
            var all = new List<string> { "-n", HelperPath };
            all.AddRange(args);
            return Runner("sudo", all, TimeSpan.FromSeconds(30));
        }

        private static async Task<ShellResult> RunProcess(string file, IReadOnlyList<string> args, TimeSpan timeout)
        {
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            foreach (string a in args)
                psi.ArgumentList.Add(a);
            // Untranslated, parseable output
            psi.Environment["LC_ALL"] = "C.UTF-8";
            Process p;
            try
            {
                p = Process.Start(psi);
            }
            catch (Win32Exception e)
            {
                return ShellResult.Failed(file + ": " + e.Message);
            }
            if (p == null)
                return ShellResult.Failed(file + ": could not be started");
            using (p)
            {
                p.StandardInput.Close();
                Task<string> stdout = p.StandardOutput.ReadToEndAsync();
                Task<string> stderr = p.StandardError.ReadToEndAsync();
                using var cts = new CancellationTokenSource(timeout);
                try
                {
                    await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { p.Kill(true); } catch (InvalidOperationException) { }
                    return ShellResult.Failed(file + ": no answer within " + (int)timeout.TotalSeconds + " s");
                }
                return new ShellResult
                {
                    ExitCode = p.ExitCode,
                    Output = await stdout.ConfigureAwait(false),
                    Error = await stderr.ConfigureAwait(false),
                };
            }
        }
    }
}
