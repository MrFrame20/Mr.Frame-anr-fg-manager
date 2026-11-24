using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AnrFgManager
{
    public struct IniChange
    {
        public string Section;
        public string Key;
        public string Value;

        public IniChange(string section, string key, string value)
        {
            Section = section ?? "";
            Key = key ?? "";
            Value = value ?? "";
        }
    }

    public static class IniHelper
    {
        public static string ReadValue(string path, string section, string key)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string cur = "";
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Length >= 2 && t[0] == '[' && t[t.Length - 1] == ']')
                {
                    cur = t.Substring(1, t.Length - 2);
                    continue;
                }
                if (t.Length == 0 || t[0] == ';') continue;
                if (string.IsNullOrEmpty(section) || string.Equals(cur, section, StringComparison.OrdinalIgnoreCase))
                {
                    int eq = t.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = t.Substring(0, eq).Trim();
                        if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                            return t.Substring(eq + 1).Trim();
                    }
                }
            }
            return null;
        }

        private static string[] SetValue(string[] lines, string section, string key, string value)
        {
            string cur = "";
            bool done = false;
            List<string> output = new List<string>(lines.Length + 1);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string t = line.Trim();
                if (t.Length >= 2 && t[0] == '[' && t[t.Length - 1] == ']')
                {
                    cur = t.Substring(1, t.Length - 2);
                }
                else if (!done && t.Length > 0 && t[0] != ';' &&
                    (string.IsNullOrEmpty(section) || string.Equals(cur, section, StringComparison.OrdinalIgnoreCase)))
                {
                    int eq = t.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = t.Substring(0, eq).Trim();
                        if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                        {
                            line = key + "=" + value;
                            done = true;
                        }
                    }
                }
                output.Add(line);
            }
            if (!done)
                output.Add(key + "=" + value);
            return output.ToArray();
        }

        public static void SaveChanges(string path, IniChange[] changes)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Arquivo não encontrado: " + path);

            string bak = path + ".anrbak";
            if (!File.Exists(bak))
                File.Copy(path, bak);

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < changes.Length; i++)
                lines = SetValue(lines, changes[i].Section, changes[i].Key, changes[i].Value);

            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }
    }
}
