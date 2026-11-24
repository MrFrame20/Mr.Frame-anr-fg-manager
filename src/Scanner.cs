using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AnrFgManager
{
    public class GameInfo
    {
        public string Name = "";
        public string Store = "";
        public string Dir = "";
        public string ExePath = "";
        public string ExeDir = "";
        public string Api = "";
        public string Engine = "";
        public string Reason = "";
        public string Status = "";
        public string Place = "";
        public string AppId = "";
        public bool IsGame;
        public bool Manual;
        public bool Removed;
        public long ExeSize;
        public long ExeTicks;
        public long DirTicks;

        public string Key
        {
            get { return Manual ? "exe:" + ExePath.ToLowerInvariant() : Dir.ToLowerInvariant(); }
        }
    }

    public class Place
    {
        public string Folder = "";
        public string Store = "";
        public int Found;
        public long Checked;
        public bool Scan = true;
    }

    public static class Scanner
    {
        public static readonly ConcurrentQueue<GameInfo> Out = new ConcurrentQueue<GameInfo>();
        public static volatile bool Running;
        public static volatile bool Cancelled;
        public static volatile string Status = "";

        private static readonly object LockObj = new object();
        private static readonly Dictionary<string, GameInfo> cache = new Dictionary<string, GameInfo>();
        private static List<Place> places = new List<Place>();
        private static string cachePath = "";

        private static readonly string[] ApiNames = { "d3d12.dll", "vulkan-1.dll", "d3d11.dll", "d3d9.dll", "opengl32.dll" };
        private static readonly string[] ApiLabels = { "D3D12", "Vulkan", "D3D11", "D3D9", "OpenGL" };
        private static readonly byte[][] pats;
        private static readonly bool[] firstByte;
        private static readonly int maxLen;

        private static readonly string[] Probes = {
            "XboxGames", "Xbox",
            @"Program Files\EA Games", "EA",
            @"Program Files (x86)\Origin Games", "EA",
            @"Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games", "Ubisoft",
            @"Program Files\Ubisoft\Ubisoft Game Launcher\games", "Ubisoft",
            @"Program Files (x86)\GOG Galaxy\Games", "GOG"
        };
        private static readonly string[] CommonProbes = { "Games", "Pasta", "Jogos", "Pasta" };

        private static readonly Regex BadExe = new Regex(@"unins|crash|setup|redist|dxsetup|dotnet|prereq|installer|easyanticheat|beservice|cefsub|webhelper|vc_redist|vcredist|dxwebsetup|oalinst", RegexOptions.IgnoreCase);
        private static readonly Regex SkipDir = new Regex(@"^(_?commonredist|redist.*|directx|vcredist.*|dotnet.*|__installer|installer|support|easyanticheat.*|battleye|crashreport.*|node_modules|mods?|plugins?|backup.*|logs?|saves?|screenshots?)$", RegexOptions.IgnoreCase);
        private static readonly Regex NotGameName = new Regex(@"^(tools?|redist|_?commonredist|backup|temp|cache|saves?|mods?|screenshots?|logs?|downloads?|installers?|steam|steamlibrary|launcher)$", RegexOptions.IgnoreCase);
        private static readonly Regex AppIdName = new Regex(@"^appid_\d+$", RegexOptions.IgnoreCase);
        private static readonly Regex SteamSkip = new Regex(@"Steamworks|Steam Linux Runtime|Proton|Redistributable|SteamVR", RegexOptions.IgnoreCase);
        private static readonly Regex ProgSkip = new Regex(@"Microsoft|NVIDIA|Advanced Micro|AMD|Intel|Google|Adobe|Mozilla|Realtek", RegexOptions.IgnoreCase);

        private static string[] exPaths = new string[0];
        private static string[] exNames = new string[0];
        private static bool scanCommon = true;

        static Scanner()
        {
            pats = new byte[ApiNames.Length * 2][];
            firstByte = new bool[256];
            int ml = 0;
            for (int i = 0; i < ApiNames.Length; i++)
            {
                pats[i * 2] = Encoding.ASCII.GetBytes(ApiNames[i]);
                pats[i * 2 + 1] = Encoding.Unicode.GetBytes(ApiNames[i]);
            }
            for (int k = 0; k < pats.Length; k++)
            {
                if (pats[k].Length > ml) ml = pats[k].Length;
                firstByte[pats[k][0]] = true;
            }
            maxLen = ml;
        }

        public static int PlaceCount
        {
            get { lock (LockObj) { return places.Count; } }
        }

        public static bool[] ScanSpecificDlls(string path, string[] dllNames)
        {
            bool[] found = new bool[dllNames.Length];
            byte[][] localPats = new byte[dllNames.Length * 2][];
            bool[] localFirst = new bool[256];
            int localMax = 0;
            for (int i = 0; i < dllNames.Length; i++)
            {
                string low = dllNames[i].ToLowerInvariant();
                localPats[i * 2] = Encoding.ASCII.GetBytes(low);
                localPats[i * 2 + 1] = Encoding.Unicode.GetBytes(low);
            }
            for (int k = 0; k < localPats.Length; k++)
            {
                if (localPats[k].Length > localMax) localMax = localPats[k].Length;
                localFirst[localPats[k][0]] = true;
            }
            ScanFilePatterns(path, found, localPats, localFirst, localMax, false);
            return found;
        }

        private static string S(string s)
        {
            return (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        private static string CleanName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            n = n.Trim().Replace('_', ' ');
            if (n.IndexOf(' ') < 0 && n.Split('.').Length > 2) n = n.Replace('.', ' ');
            return Regex.Replace(n, @"\s+", " ").Trim();
        }

        private static string[] SplitList(string text)
        {
            List<string> r = new List<string>();
            if (text != null)
            {
                string[] parts = text.Split(new char[] { ',', ';', '\n' });
                for (int i = 0; i < parts.Length; i++)
                {
                    string t = parts[i].Trim().ToLowerInvariant();
                    if (t.Length > 0) r.Add(t);
                }
            }
            return r.ToArray();
        }

        private static string[] NormPaths(string[] paths)
        {
            List<string> r = new List<string>();
            if (paths != null)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    if (string.IsNullOrEmpty(paths[i])) continue;
                    string t = paths[i].Trim().Trim('"').TrimEnd('\\');
                    if (t.Length > 0) r.Add(t);
                }
            }
            return r.ToArray();
        }

        private static bool IsExcludedPath(string dir)
        {
            for (int i = 0; i < exPaths.Length; i++)
            {
                string e = exPaths[i];
                if (dir.Equals(e, StringComparison.OrdinalIgnoreCase) ||
                    dir.StartsWith(e + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsExcludedName(string store, string dir)
        {
            if (store != "Pasta" && store != "Xbox" && store != "Programa") return false;
            string fn = Path.GetFileName(dir).ToLowerInvariant();
            for (int i = 0; i < exNames.Length; i++)
            {
                if (fn.Contains(exNames[i])) return true;
            }
            return false;
        }

        public static void Load(string path)
        {
            cachePath = path;
            try
            {
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length == 0 || lines[0] != "V\t4") return;
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] f = lines[i].Split('\t');
                    if (f[0] == "G" && f.Length >= 15)
                    {
                        GameInfo g = new GameInfo();
                        g.Name = f[1]; g.Store = f[2]; g.Dir = f[3]; g.ExePath = f[4]; g.ExeDir = f[5];
                        g.Api = f[6]; g.Engine = f[7]; g.IsGame = (f[8] == "1"); g.Manual = (f[9] == "1");
                        long.TryParse(f[10], out g.ExeSize);
                        long.TryParse(f[11], out g.ExeTicks);
                        long.TryParse(f[12], out g.DirTicks);
                        g.Reason = f[13];
                        g.AppId = f[14];
                        cache[g.Key] = g;
                    }
                    else if (f[0] == "P" && f.Length >= 6)
                    {
                        Place p = new Place();
                        p.Folder = f[1]; p.Store = f[2];
                        int.TryParse(f[3], out p.Found);
                        long.TryParse(f[4], out p.Checked);
                        p.Scan = (f[5] == "1");
                        places.Add(p);
                    }
                }
            }
            catch { }
            foreach (GameInfo g in cache.Values)
            {
                if (g.IsGame) Out.Enqueue(g);
            }
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(cachePath)) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("V\t4\n");
                lock (LockObj)
                {
                    foreach (GameInfo g in cache.Values)
                    {
                        sb.Append("G\t").Append(S(g.Name)).Append('\t').Append(S(g.Store)).Append('\t').Append(S(g.Dir)).Append('\t')
                          .Append(S(g.ExePath)).Append('\t').Append(S(g.ExeDir)).Append('\t').Append(S(g.Api)).Append('\t').Append(S(g.Engine)).Append('\t')
                          .Append(g.IsGame ? "1" : "0").Append('\t').Append(g.Manual ? "1" : "0").Append('\t')
                          .Append(g.ExeSize).Append('\t').Append(g.ExeTicks).Append('\t').Append(g.DirTicks).Append('\t').Append(S(g.Reason)).Append('\t').Append(S(g.AppId)).Append('\n');
                    }
                    foreach (Place p in places)
                    {
                        sb.Append("P\t").Append(S(p.Folder)).Append('\t').Append(S(p.Store)).Append('\t').Append(p.Found).Append('\t')
                          .Append(p.Checked).Append('\t').Append(p.Scan ? "1" : "0").Append('\n');
                    }
                }
                string dir = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = cachePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                File.Copy(tmp, cachePath, true);
                File.Delete(tmp);
            }
            catch { }
        }

        public static void Start(bool full, string[] extraLibs, string[] manualExes, string[] hidden, bool includePrograms, string[] excludePaths, string excludeNames, bool probeCommon)
        {
            if (Running) return;
            exPaths = NormPaths(excludePaths);
            exNames = SplitList(excludeNames);
            scanCommon = probeCommon;
            Running = true;
            Cancelled = false;
            Status = "Procurando...";
            Thread t = new Thread(delegate()
            {
                try { Run(full, extraLibs, manualExes, hidden, includePrograms); }
                catch (Exception ex) { Status = "Erro na busca: " + ex.Message; }
                finally { Running = false; }
            });
            t.IsBackground = true;
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();
        }

        public static void Cancel() { Cancelled = true; }

        private static void AddCand(List<GameInfo> list, HashSet<string> seen, HashSet<string> hid, string name, string store, string dir, string pinnedExe, string place, string appId = "")
        {
            if (string.IsNullOrEmpty(dir)) return;
            dir = dir.Trim().Trim('"').TrimEnd('\\');
            try { if (!Directory.Exists(dir)) return; } catch { return; }
            if (string.IsNullOrEmpty(pinnedExe))
            {
                if (IsExcludedPath(dir) || IsExcludedName(store, dir)) return;
            }
            string nm = name;
            if (string.IsNullOrEmpty(nm) || AppIdName.IsMatch(nm.Trim())) nm = Path.GetFileName(dir);
            GameInfo g = new GameInfo();
            g.Name = CleanName(S(nm));
            g.Store = store;
            g.Dir = dir;
            g.Place = place ?? "";
            g.AppId = appId ?? "";
            if (!string.IsNullOrEmpty(pinnedExe)) { g.Manual = true; g.ExePath = pinnedExe; }
            string key = g.Key;
            if (hid.Contains(key)) return;
            if (!seen.Add(key)) return;
            list.Add(g);
        }

        private static void Run(bool full, string[] extraLibs, string[] manualExes, string[] hidden, bool includePrograms)
        {
            HashSet<string> hid = new HashSet<string>();
            if (hidden != null)
            {
                for (int i = 0; i < hidden.Length; i++)
                    hid.Add(hidden[i].Trim().ToLowerInvariant());
            }
            List<GameInfo> cands = new List<GameInfo>();
            HashSet<string> seen = new HashSet<string>();
            List<Place> infoPlaces = new List<Place>();

            Status = "Procurando nos launchers (Steam, Epic, GOG)...";
            FindSteam(cands, seen, hid, infoPlaces);
            FindEpic(cands, seen, hid, infoPlaces);
            FindGog(cands, seen, hid, infoPlaces);

            bool probe = full;
            List<Place> keep = new List<Place>();
            lock (LockObj)
            {
                if (places.Count == 0) probe = true;
                keep.AddRange(places);
            }
            List<Place> toScan = new List<Place>();
            HashSet<string> placeSeen = new HashSet<string>();

            if (probe)
            {
                Status = "Procurando nos discos...";
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int di = 0; di < drives.Length; di++)
                {
                    DriveInfo d = drives[di];
                    try { if (d.DriveType != DriveType.Fixed || !d.IsReady) continue; } catch { continue; }
                    string root = d.RootDirectory.FullName;
                    List<string> probeList = new List<string>(Probes);
                    if (scanCommon) probeList.AddRange(CommonProbes);
                    for (int i = 0; i + 1 < probeList.Count; i += 2)
                    {
                        string p = Path.Combine(root, probeList[i]);
                        try
                        {
                            if (Directory.Exists(p) && placeSeen.Add(p.ToLowerInvariant()))
                            {
                                Place pl = new Place(); pl.Folder = p; pl.Store = probeList[i + 1]; toScan.Add(pl);
                            }
                        }
                        catch { }
                    }
                }
            }
            else
            {
                for (int i = 0; i < keep.Count; i++)
                {
                    Place p = keep[i];
                    if (p.Scan && p.Found > 0 && Directory.Exists(p.Folder) && placeSeen.Add(p.Folder.ToLowerInvariant()))
                        toScan.Add(p);
                }
            }

            if (extraLibs != null)
            {
                for (int i = 0; i < extraLibs.Length; i++)
                {
                    string lib = extraLibs[i];
                    try
                    {
                        if (Directory.Exists(lib) && placeSeen.Add(lib.ToLowerInvariant()))
                        {
                            Place pl = new Place(); pl.Folder = lib; pl.Store = "Pasta"; toScan.Add(pl);
                        }
                    }
                    catch { }
                }
            }

            for (int pi = 0; pi < toScan.Count; pi++)
            {
                if (Cancelled) return;
                Place p = toScan[pi];
                try
                {
                    string[] subs = Directory.GetDirectories(p.Folder);
                    for (int si = 0; si < subs.Length; si++)
                    {
                        string sub = subs[si];
                        FileAttributes at = File.GetAttributes(sub);
                        if ((at & FileAttributes.Hidden) != 0 || (at & FileAttributes.System) != 0) continue;
                        AddCand(cands, seen, hid, Path.GetFileName(sub), p.Store, sub, null, p.Folder);
                    }
                }
                catch { }
            }

            if (includePrograms)
            {
                Status = "Procurando programas instalados...";
                FindPrograms(cands, seen, hid);
            }

            HashSet<string> manualSet = new HashSet<string>();
            if (manualExes != null)
            {
                for (int i = 0; i < manualExes.Length; i++)
                {
                    string exe = manualExes[i];
                    try
                    {
                        if (!File.Exists(exe)) continue;
                        manualSet.Add("exe:" + exe.ToLowerInvariant());
                        AddCand(cands, seen, hid, Path.GetFileNameWithoutExtension(exe), "Manual", Path.GetDirectoryName(exe), exe, "");
                    }
                    catch { }
                }
            }

            List<GameInfo> work = new List<GameInfo>();
            for (int i = 0; i < cands.Count; i++)
            {
                GameInfo c = cands[i];
                GameInfo old;
                bool have;
                lock (LockObj) { have = cache.TryGetValue(c.Key, out old); }
                if (have)
                {
                    if (old.IsGame)
                    {
                        bool ok = false;
                        try
                        {
                            FileInfo fi = new FileInfo(old.ExePath);
                            ok = fi.Exists && fi.Length == old.ExeSize && fi.LastWriteTimeUtc.Ticks == old.ExeTicks;
                        }
                        catch { }
                        if (ok)
                        {
                            if ((c.Store == "Steam" || c.Store == "Epic" || c.Store == "GOG") && c.Name != old.Name)
                            {
                                old.Name = c.Name;
                                Out.Enqueue(old);
                            }
                            continue;
                        }
                    }
                    else
                    {
                        long dt = 0;
                        try { dt = Directory.GetLastWriteTimeUtc(c.Dir).Ticks; } catch { }
                        if (dt == old.DirTicks) continue;
                    }
                }
                work.Add(c);
            }

            int total = work.Count;
            int done = 0;
            ParallelOptions opts = new ParallelOptions();
            opts.MaxDegreeOfParallelism = 3;
            Parallel.ForEach(work, opts, delegate(GameInfo c)
            {
                if (Cancelled) return;
                Analyze(c);
                lock (LockObj) { cache[c.Key] = c; }
                if (c.IsGame) Out.Enqueue(c);
                int n = Interlocked.Increment(ref done);
                Status = "Analisando " + n + " de " + total + ": " + c.Name;
            });
            if (Cancelled) return;

            lock (LockObj)
            {
                List<string> drop = new List<string>();
                foreach (KeyValuePair<string, GameInfo> kv in cache)
                {
                    GameInfo g = kv.Value;
                    bool gone = false;
                    if (hid.Contains(kv.Key)) gone = true;
                    else if (g.Manual) gone = !manualSet.Contains(kv.Key);
                    else if (IsExcludedPath(g.Dir) || IsExcludedName(g.Store, g.Dir)) gone = true;
                    else if (g.IsGame) { try { gone = !Directory.Exists(g.Dir); } catch { } }
                    if (gone) drop.Add(kv.Key);
                }
                for (int i = 0; i < drop.Count; i++)
                {
                    string k = drop[i];
                    GameInfo g = cache[k];
                    cache.Remove(k);
                    if (g.IsGame) { g.Removed = true; Out.Enqueue(g); }
                }

                Dictionary<string, Place> byFolder = new Dictionary<string, Place>();
                for (int i = 0; i < toScan.Count; i++)
                {
                    Place p = toScan[i];
                    p.Found = 0; p.Checked = DateTime.UtcNow.Ticks; p.Scan = true;
                    byFolder[p.Folder.ToLowerInvariant()] = p;
                }
                for (int i = 0; i < cands.Count; i++)
                {
                    GameInfo c = cands[i];
                    GameInfo fin;
                    if (c.Place.Length > 0 && cache.TryGetValue(c.Key, out fin) && fin.IsGame)
                    {
                        Place pl;
                        if (byFolder.TryGetValue(c.Place.ToLowerInvariant(), out pl)) pl.Found++;
                    }
                }
                List<Place> merged = new List<Place>();
                merged.AddRange(infoPlaces);
                merged.AddRange(toScan);
                if (!probe)
                {
                    for (int i = 0; i < keep.Count; i++)
                    {
                        Place p = keep[i];
                        if (!p.Scan) continue;
                        if (byFolder.ContainsKey(p.Folder.ToLowerInvariant())) continue;
                        try { if (Directory.Exists(p.Folder)) merged.Add(p); } catch { }
                    }
                }
                places = merged;
            }
            Save();
            int games = 0;
            lock (LockObj)
            {
                foreach (GameInfo g in cache.Values)
                    if (g.IsGame) games++;
            }
            Status = "Concluído: " + games + " jogo(s).";
        }

        private static void FindSteam(List<GameInfo> cands, HashSet<string> seen, HashSet<string> hid, List<Place> info)
        {
            List<string> roots = new List<string>();
            string[] keys = {
                @"HKEY_CURRENT_USER\Software\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
            };
            string[] vals = { "SteamPath", "InstallPath" };
            for (int ki = 0; ki < keys.Length; ki++)
            {
                for (int vi = 0; vi < vals.Length; vi++)
                {
                    try
                    {
                        object v = Registry.GetValue(keys[ki], vals[vi], null);
                        if (v is string) roots.Add(((string)v).Replace('/', '\\'));
                    }
                    catch { }
                }
            }
            HashSet<string> libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int ri = 0; ri < roots.Count; ri++)
            {
                string r = roots[ri];
                libs.Add(r.TrimEnd('\\'));
                try
                {
                    string vdf = Path.Combine(r, @"steamapps\libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        MatchCollection ms = Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"");
                        for (int mi = 0; mi < ms.Count; mi++)
                            libs.Add(ms[mi].Groups[1].Value.Replace(@"\\", @"\").TrimEnd('\\'));
                    }
                }
                catch { }
            }
            foreach (string lib in libs)
            {
                if (Cancelled) return;
                string sa = Path.Combine(lib, "steamapps");
                try
                {
                    if (!Directory.Exists(sa)) continue;
                    int count = 0;
                    string[] acfs = Directory.GetFiles(sa, "appmanifest_*.acf");
                    for (int ai = 0; ai < acfs.Length; ai++)
                    {
                        string t = File.ReadAllText(acfs[ai]);
                        Match mn = Regex.Match(t, "\"name\"\\s+\"([^\"]+)\"");
                        Match mi = Regex.Match(t, "\"installdir\"\\s+\"([^\"]+)\"");
                        if (!mi.Success) continue;
                        string name = mn.Groups[1].Value;
                        if (SteamSkip.IsMatch(name)) continue;
                        int before = cands.Count;
                        string fileId = Path.GetFileNameWithoutExtension(acfs[ai]);
                        int us = fileId.IndexOf('_');
                        string steamAppId = us >= 0 ? fileId.Substring(us + 1) : "";
                        AddCand(cands, seen, hid, name, "Steam", Path.Combine(Path.Combine(sa, "common"), mi.Groups[1].Value), null, "", steamAppId);
                        if (cands.Count > before) count++;
                    }
                    Place pl = new Place(); pl.Folder = sa; pl.Store = "Steam"; pl.Found = count; pl.Scan = false; pl.Checked = DateTime.UtcNow.Ticks;
                    info.Add(pl);
                }
                catch { }
            }
        }

        private static string JsonField(string text, string field)
        {
            Match m = Regex.Match(text, "\"" + field + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return "";
            try { return Regex.Unescape(m.Groups[1].Value); } catch { return m.Groups[1].Value; }
        }

        private static void FindEpic(List<GameInfo> cands, HashSet<string> seen, HashSet<string> hid, List<Place> info)
        {
            try
            {
                string pd = Environment.GetEnvironmentVariable("ProgramData");
                if (string.IsNullOrEmpty(pd)) return;
                string dir = Path.Combine(pd, @"Epic\EpicGamesLauncher\Data\Manifests");
                if (!Directory.Exists(dir)) return;
                int count = 0;
                string[] files = Directory.GetFiles(dir, "*.item");
                for (int i = 0; i < files.Length; i++)
                {
                    string t = File.ReadAllText(files[i]);
                    string name = JsonField(t, "DisplayName");
                    string loc = JsonField(t, "InstallLocation");
                    string app = JsonField(t, "AppName");
                    if (name.Length == 0 || loc.Length == 0 || app.StartsWith("UE_")) continue;
                    int before = cands.Count;
                    string launchId = JsonField(t, "CatalogNamespace") + ":" + JsonField(t, "CatalogItemId") + ":" + app;
                    AddCand(cands, seen, hid, name, "Epic", loc, null, "", launchId);
                    if (cands.Count > before) count++;
                }
                Place pl = new Place(); pl.Folder = dir; pl.Store = "Epic"; pl.Found = count; pl.Scan = false; pl.Checked = DateTime.UtcNow.Ticks;
                info.Add(pl);
            }
            catch { }
        }

        private static void FindGog(List<GameInfo> cands, HashSet<string> seen, HashSet<string> hid, List<Place> info)
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games"))
                {
                    if (k == null) return;
                    int count = 0;
                    string[] subs = k.GetSubKeyNames();
                    for (int i = 0; i < subs.Length; i++)
                    {
                        using (RegistryKey g = k.OpenSubKey(subs[i]))
                        {
                            if (g == null) continue;
                            string name = g.GetValue("gameName") as string;
                            string path = g.GetValue("path") as string;
                            int before = cands.Count;
                            AddCand(cands, seen, hid, name, "GOG", path, null, "");
                            if (cands.Count > before) count++;
                        }
                    }
                    Place pl = new Place(); pl.Folder = "Registro do GOG Galaxy"; pl.Store = "GOG"; pl.Found = count; pl.Scan = false; pl.Checked = DateTime.UtcNow.Ticks;
                    info.Add(pl);
                }
            }
            catch { }
        }

        private static void FindPrograms(List<GameInfo> cands, HashSet<string> seen, HashSet<string> hid)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string[] hives = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            for (int h = 0; h < 3; h++)
            {
                RegistryKey baseKey = (h == 2) ? Registry.CurrentUser : Registry.LocalMachine;
                string hive = (h == 2) ? hives[0] : hives[h];
                try
                {
                    using (RegistryKey k = baseKey.OpenSubKey(hive))
                    {
                        if (k == null) continue;
                        string[] subs = k.GetSubKeyNames();
                        for (int i = 0; i < subs.Length; i++)
                        {
                            using (RegistryKey g = k.OpenSubKey(subs[i]))
                            {
                                if (g == null) continue;
                                string name = g.GetValue("DisplayName") as string;
                                string loc = g.GetValue("InstallLocation") as string;
                                string pub = g.GetValue("Publisher") as string;
                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(loc)) continue;
                                if (!string.IsNullOrEmpty(pub) && ProgSkip.IsMatch(pub)) continue;
                                if (loc.StartsWith(win, StringComparison.OrdinalIgnoreCase)) continue;
                                AddCand(cands, seen, hid, name, "Programa", loc, null, "");
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static void Analyze(GameInfo g)
        {
            try
            {
                try { g.DirTicks = Directory.GetLastWriteTimeUtc(g.Dir).Ticks; } catch { }
                string exe = g.Manual ? g.ExePath : FindMainExe(g.Dir);
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) { g.IsGame = false; g.Reason = "sem .exe"; return; }
                FileInfo fi = new FileInfo(exe);
                g.ExePath = exe;
                g.ExeDir = fi.DirectoryName;
                g.ExeSize = fi.Length;
                g.ExeTicks = fi.LastWriteTimeUtc.Ticks;

                bool[] found = new bool[ApiNames.Length];
                ScanFilePatterns(exe, found, pats, firstByte, maxLen, true);
                string unity = Path.Combine(g.ExeDir, "UnityPlayer.dll");
                if (File.Exists(unity)) ScanFilePatterns(unity, found, pats, firstByte, maxLen, true);

                StringBuilder api = new StringBuilder();
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i])
                    {
                        if (api.Length > 0) api.Append('/');
                        api.Append(ApiLabels[i]);
                    }
                }
                g.Api = api.Length > 0 ? api.ToString() : "?";
                g.Engine = DetectEngine(g.Dir, g.ExeDir);

                bool trusted = g.Manual || g.Store == "Steam" || g.Store == "Epic" || g.Store == "GOG" || g.Store == "EA" || g.Store == "Ubisoft";
                if (trusted) { g.IsGame = true; return; }

                bool strongApi = g.Api.Contains("D3D12") || g.Api.Contains("Vulkan");
                bool hasGfx = g.Engine.Length > 0 || strongApi || (g.Api != "?" && g.ExeSize >= 30L * 1024 * 1024);
                if (!hasGfx) { g.IsGame = false; g.Reason = "sem API gráfica nem engine de jogo"; return; }
                if (NotGameName.IsMatch(Path.GetFileName(g.Dir)) || IsAppShell(g)) { g.IsGame = false; g.Reason = "aplicativo, não jogo"; return; }
                g.IsGame = true;
            }
            catch (Exception ex)
            {
                g.IsGame = false;
                g.Reason = "erro: " + ex.Message;
            }
        }

        private static bool IsAppShell(GameInfo g)
        {
            try
            {
                if (File.Exists(Path.Combine(g.Dir, @"resources\app.asar"))) return true;
                if (File.Exists(Path.Combine(g.ExeDir, @"resources\app.asar"))) return true;
                if (File.Exists(Path.Combine(g.ExeDir, "libcef.dll"))) return true;
                if (File.Exists(Path.Combine(g.ExeDir, "chrome_100_percent.pak"))) return true;
            }
            catch { }
            return false;
        }

        private static string DetectEngine(string dir, string exeDir)
        {
            try
            {
                if (File.Exists(Path.Combine(exeDir, "UnityPlayer.dll"))) return "Unity";
                string[] dirs = Directory.GetDirectories(exeDir, "*_Data");
                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i];
                    if (File.Exists(Path.Combine(d, "globalgamemanagers")) ||
                        File.Exists(Path.Combine(d, "data.unity3d")) ||
                        Directory.Exists(Path.Combine(d, "Managed")))
                        return "Unity";
                }
                if (exeDir.IndexOf(@"\Binaries\Win", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Directory.Exists(Path.Combine(dir, "Engine")))
                    return "Unreal";
                if (Directory.GetFiles(exeDir, "*.pck").Length > 0) return "Godot";
            }
            catch { }
            return "";
        }

        private static string FindMainExe(string dir)
        {
            string best = null;
            long bestSize = -1;
            Walk(dir, 0, ref best, ref bestSize);
            return best;
        }

        private static void Walk(string d, int depth, ref string best, ref long bestSize)
        {
            if (Cancelled) return;
            try
            {
                string[] files = Directory.GetFiles(d, "*.exe");
                for (int i = 0; i < files.Length; i++)
                {
                    if (BadExe.IsMatch(Path.GetFileName(files[i]))) continue;
                    long s = new FileInfo(files[i]).Length;
                    if (s > bestSize) { best = files[i]; bestSize = s; }
                }
                if (depth >= 4) return;
                string[] subs = Directory.GetDirectories(d);
                for (int i = 0; i < subs.Length; i++)
                {
                    if (SkipDir.IsMatch(Path.GetFileName(subs[i]))) continue;
                    Walk(subs[i], depth + 1, ref best, ref bestSize);
                }
            }
            catch { }
        }

        private static void ScanFilePatterns(string path, bool[] found, byte[][] patterns, bool[] first, int maxPatLen, bool stopWhenFirstTwoFound)
        {
            const int chunk = 4 << 20;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20))
                {
                    byte[] buf = new byte[chunk + maxPatLen];
                    int keep = 0;
                    long total = 0;
                    while (!Cancelled)
                    {
                        int n = fs.Read(buf, keep, chunk);
                        if (n <= 0) break;
                        total += n;
                        int len = keep + n;
                        for (int i = 0; i < len; i++)
                        {
                            byte b = buf[i];
                            if (b >= 65 && b <= 90) b = (byte)(b + 32);
                            if (!first[b]) continue;
                            for (int k = 0; k < patterns.Length; k++)
                            {
                                if (found[k >> 1]) continue;
                                byte[] p = patterns[k];
                                if (p[0] != b || i + p.Length > len) continue;
                                bool ok = true;
                                for (int j = 1; j < p.Length; j++)
                                {
                                    byte c = buf[i + j];
                                    if (c >= 65 && c <= 90) c = (byte)(c + 32);
                                    if (c != p[j]) { ok = false; break; }
                                }
                                if (ok) found[k >> 1] = true;
                            }
                        }
                        if (stopWhenFirstTwoFound && found.Length >= 2 && found[0] && found[1]) break;
                        if (total > 400L * 1024 * 1024) break;
                        keep = maxPatLen - 1;
                        if (keep > len) keep = len;
                        Array.Copy(buf, len - keep, buf, 0, keep);
                    }
                }
            }
            catch { }
        }
    }
}
