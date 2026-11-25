// Reads (and writes a few) system files: /proc, /sys, /etc. Root is a path
// prefix for all of them (the screenshot harness points it to a sample tree).
using System;
using System.Collections.Generic;
using System.IO;

namespace GpuTest.Services
{
    internal static class Sys
    {
        public static string Root = "";

        public static string Read(string path)
        {
            try
            {
                return File.ReadAllText(Root + path).Trim();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        // Device tree strings end with a NUL
        public static string ReadDt(string path) => Read(path)?.TrimEnd('\0');

        public static long? ReadLong(string path) =>
            long.TryParse(Read(path), out long v) ? v : null;

        public static string[] Lines(string path)
        {
            string text = Read(path);
            return text == null ? Array.Empty<string>() : text.Split('\n');
        }

        public static bool Write(string path, string text, out string error)
        {
            error = null;
            try
            {
                File.WriteAllText(Root + path, text);
                return true;
            }
            catch (IOException e) { error = e.Message; }
            catch (UnauthorizedAccessException) { error = "permission denied"; }
            return false;
        }

        // Directory entries (names only), sorted; empty if the directory is missing
        public static string[] List(string dir, string pattern = "*")
        {
            try
            {
                var names = new List<string>();
                foreach (string p in Directory.GetFileSystemEntries(Root + dir, pattern))
                    names.Add(Path.GetFileName(p));
                names.Sort(StringComparer.Ordinal);
                return names.ToArray();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return Array.Empty<string>();
        }

        public static bool Exists(string path) => File.Exists(Root + path) || Directory.Exists(Root + path);

        // KEY=value files (os-release, /etc/default/*), quotes removed
        public static Dictionary<string, string> KeyValues(string path)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in Lines(path))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.StartsWith("#", StringComparison.Ordinal) || eq <= 0)
                    continue;
                string v = line.Substring(eq + 1).Trim();
                if (v.Length >= 2 && (v[0] == '"' || v[0] == '\'') && v[v.Length - 1] == v[0])
                    v = v.Substring(1, v.Length - 2);
                d[line.Substring(0, eq).Trim()] = v;
            }
            return d;
        }

        public static string Get(this Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out string v) ? v : null;
    }
}
