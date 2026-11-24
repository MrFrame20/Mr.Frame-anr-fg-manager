using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AnrFgManager
{
    public class GameState
    {
        public bool Valid;
        public bool Installed;
        public bool Foreign;
        public string Proxy;
        public string InstalledAt;
    }

    public class AppSettings
    {
        public List<string> SearchFolders = new List<string>();
        public List<string> ExcludePaths = new List<string>();
        public string ExcludeNames = "ferramentas, tools, mods, config, backup, saves";
        public bool ScanCommon = true;
        public int Sort = 0;
        public bool Compat = false;
        public bool Installed = false;
        public bool Other = false;
        public string UpdateUrl = "";
        public bool CheckOnStart = true;
        public string GithubRepo = AppUpdater.DefaultRepo;
        public string PackageRepo = AppUpdater.DefaultPackageRepo;
        public bool TrayOnPlay = true;
        public bool DownloadBanners = true;

        public static string GetDataDir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ANR-FG-Manager");
            Directory.CreateDirectory(d);
            return d;
        }

        public static string GetLocalDataDir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANR-FG-Manager");
            Directory.CreateDirectory(d);
            return d;
        }

        public static string GetSettingsFile() { return Path.Combine(GetDataDir(), "settings.json"); }
        public static string GetManualFile() { return Path.Combine(GetDataDir(), "manual.txt"); }
        public static string GetHiddenFile() { return Path.Combine(GetDataDir(), "hidden.txt"); }
        public static string GetCacheFile() { return Path.Combine(GetDataDir(), "cache.tsv"); }

        public static string[] ReadExistingListFile(string f)
        {
            List<string> r = new List<string>();
            if (File.Exists(f))
            {
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    string s = (lines[i] ?? "").Trim();
                    if (s.Length > 0 && (File.Exists(s) || Directory.Exists(s))) r.Add(s);
                }
            }
            return r.ToArray();
        }

        public static string[] ReadRawListFile(string f)
        {
            List<string> r = new List<string>();
            if (File.Exists(f))
            {
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    string s = (lines[i] ?? "").Trim();
                    if (s.Length > 0) r.Add(s);
                }
            }
            return r.ToArray();
        }

        public static AppSettings Load()
        {
            AppSettings st = new AppSettings();
            string f = GetSettingsFile();
            if (File.Exists(f))
            {
                try
                {
                    Dictionary<string, object> j = JsonHelper.ParseObject(File.ReadAllText(f, Encoding.UTF8));
                    if (j != null)
                    {
                        st.SearchFolders = JsonHelper.GetStringList(j, "searchFolders");
                        st.ExcludePaths = JsonHelper.GetStringList(j, "excludePaths");
                        st.ExcludeNames = JsonHelper.GetString(j, "excludeNames", st.ExcludeNames);
                        st.ScanCommon = JsonHelper.GetBool(j, "scanCommon", true);
                        st.Sort = JsonHelper.GetInt(j, "sort", 0);
                        st.Compat = JsonHelper.GetBool(j, "compat", false);
                        st.Installed = JsonHelper.GetBool(j, "installed", false);
                        st.Other = JsonHelper.GetBool(j, "other", false);
                        st.UpdateUrl = JsonHelper.GetString(j, "updateUrl", "");
                        st.CheckOnStart = JsonHelper.GetBool(j, "checkOnStart", true);
                        st.GithubRepo = JsonHelper.GetString(j, "githubRepo", AppUpdater.DefaultRepo);
                        st.PackageRepo = JsonHelper.GetString(j, "packageRepo", AppUpdater.DefaultPackageRepo);
                        // versoes anteriores aceitavam o repositorio do pacote no campo do programa: corrige
                        if (string.Equals(st.GithubRepo, st.PackageRepo, StringComparison.OrdinalIgnoreCase)) st.GithubRepo = "";
                        st.TrayOnPlay = JsonHelper.GetBool(j, "trayOnPlay", true);
                        st.DownloadBanners = JsonHelper.GetBool(j, "downloadBanners", true);
                    }
                }
                catch { }
            }

            string old = Path.Combine(GetDataDir(), "libraries.txt");
            if (File.Exists(old))
            {
                try
                {
                    HashSet<string> merged = new HashSet<string>(st.SearchFolders, StringComparer.OrdinalIgnoreCase);
                    string[] lines = File.ReadAllLines(old);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string s = (lines[i] ?? "").Trim();
                        if (s.Length > 0) merged.Add(s);
                    }
                    st.SearchFolders = new List<string>(merged);
                    st.SearchFolders.Sort(StringComparer.OrdinalIgnoreCase);
                    File.Delete(old);
                    st.Save();
                }
                catch { }
            }
            return st;
        }

        public void Save()
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["searchFolders"] = SearchFolders;
                d["excludePaths"] = ExcludePaths;
                d["excludeNames"] = ExcludeNames ?? "";
                d["scanCommon"] = ScanCommon;
                d["sort"] = Sort;
                d["compat"] = Compat;
                d["installed"] = Installed;
                d["other"] = Other;
                d["updateUrl"] = UpdateUrl ?? "";
                d["checkOnStart"] = CheckOnStart;
                d["githubRepo"] = GithubRepo ?? "";
                d["packageRepo"] = PackageRepo ?? "";
                d["trayOnPlay"] = TrayOnPlay;
                d["downloadBanners"] = DownloadBanners;
                File.WriteAllText(GetSettingsFile(), JsonHelper.Serialize(d), new UTF8Encoding(true));
            }
            catch { }
        }
    }

    public static class PackageStore
    {
        public const string AppVersion = "2.2.3";
        public const string ProductDir = "Apex Neural Rendering FG";
        private const string EmbeddedResourceName = "package.zip";
        public static readonly string RelFgIni = "game/OptiScaler/custom/" + ProductDir + "/dlssg/nr_fg.ini";
        public static readonly string RelOptiIni = "game/OptiScaler.ini";
        public static readonly string RelSizeTable = "game/OptiScaler/custom/" + ProductDir + "/dlssg/size_table.json";

        public static string OverrideZipPath
        {
            get { return Path.Combine(AppSettings.GetLocalDataDir(), "package.zip"); }
        }

        public static bool UsingOverride
        {
            get { return File.Exists(OverrideZipPath); }
        }

        public static string PackageVersion { get; private set; }

        static PackageStore()
        {
            PackageVersion = "";
            ReloadMetadata();
        }

        public static Stream OpenActiveZipStream()
        {
            if (File.Exists(OverrideZipPath))
                return new FileStream(OverrideZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            Assembly asm = Assembly.GetExecutingAssembly();
            Stream s = asm.GetManifestResourceStream(EmbeddedResourceName);
            if (s == null)
                throw new InvalidOperationException("Pacote ANR FG embutido (package.zip) não encontrado no executável.");
            return s;
        }

        public static string DetectPrefix(ZipArchive archive)
        {
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                string norm = archive.Entries[i].FullName.Replace('\\', '/');
                if (string.Equals(norm, "nr_fg_package.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(norm, "game/dxgi.dll", StringComparison.OrdinalIgnoreCase))
                    return "";
            }
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                string norm = archive.Entries[i].FullName.Replace('\\', '/');
                int slash = norm.IndexOf('/');
                if (slash > 0)
                {
                    string tail = norm.Substring(slash + 1);
                    if (string.Equals(tail, "nr_fg_package.json", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tail, "game/dxgi.dll", StringComparison.OrdinalIgnoreCase))
                        return norm.Substring(0, slash + 1);
                }
            }
            return "";
        }

        public static ZipArchiveEntry FindEntry(ZipArchive archive, string prefix, string relPath)
        {
            string target = (prefix + relPath).Replace('\\', '/');
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                string norm = archive.Entries[i].FullName.Replace('\\', '/');
                if (string.Equals(norm, target, StringComparison.OrdinalIgnoreCase))
                    return archive.Entries[i];
            }
            return null;
        }

        public static bool HasEntry(string relPath)
        {
            try
            {
                using (Stream s = OpenActiveZipStream())
                using (ZipArchive z = new ZipArchive(s, ZipArchiveMode.Read))
                {
                    string prefix = DetectPrefix(z);
                    return FindEntry(z, prefix, relPath) != null;
                }
            }
            catch { return false; }
        }

        public static string ReadText(string relPath)
        {
            try
            {
                using (Stream s = OpenActiveZipStream())
                using (ZipArchive z = new ZipArchive(s, ZipArchiveMode.Read))
                {
                    string prefix = DetectPrefix(z);
                    ZipArchiveEntry e = FindEntry(z, prefix, relPath);
                    if (e == null) return null;
                    using (Stream es = e.Open())
                    using (StreamReader sr = new StreamReader(es, Encoding.UTF8))
                        return sr.ReadToEnd();
                }
            }
            catch { return null; }
        }

        public static void ExtractSingleFile(string relPath, string destPath)
        {
            using (Stream s = OpenActiveZipStream())
            using (ZipArchive z = new ZipArchive(s, ZipArchiveMode.Read))
            {
                string prefix = DetectPrefix(z);
                ZipArchiveEntry e = FindEntry(z, prefix, relPath);
                if (e == null)
                    throw new FileNotFoundException("Arquivo não encontrado no pacote: " + relPath);
                string parent = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                using (Stream es = e.Open())
                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    es.CopyTo(fs);
            }
        }

        public static void ReloadMetadata()
        {
            PackageVersion = "";
            string json = ReadText("nr_fg_package.json");
            if (!string.IsNullOrEmpty(json))
            {
                Dictionary<string, object> obj = JsonHelper.ParseObject(json);
                PackageVersion = JsonHelper.GetString(obj, "version", "");
            }
            if (string.IsNullOrEmpty(PackageVersion) && !UsingOverride)
            {
                // plano B: numero da versao no nome do .zip embutido (gravado pelo build.cmd)
                try
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("package.name"))
                    {
                        if (s != null)
                        {
                            using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
                            {
                                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(sr.ReadToEnd(), @"[Bb]\s*(\d+)");
                                if (m.Success) PackageVersion = "V.B" + m.Groups[1].Value;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        public static string ValidateAndImportZip(string zipPath, out string oldVersion, out string newVersion)
        {
            oldVersion = PackageVersion;
            newVersion = "";
            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive z = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                string prefix = DetectPrefix(z);
                ZipArchiveEntry dxgi = FindEntry(z, prefix, "game/dxgi.dll");
                ZipArchiveEntry pkgJson = FindEntry(z, prefix, "nr_fg_package.json");
                if (dxgi == null)
                    return "O .zip não contém um pacote ANR FG válido (game/dxgi.dll ausente).";
                if (pkgJson != null)
                {
                    using (Stream es = pkgJson.Open())
                    using (StreamReader sr = new StreamReader(es, Encoding.UTF8))
                    {
                        Dictionary<string, object> obj = JsonHelper.ParseObject(sr.ReadToEnd());
                        newVersion = JsonHelper.GetString(obj, "version", "");
                    }
                }
            }

            string dst = OverrideZipPath;
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(zipPath, dst, true);
            ReloadMetadata();
            if (string.IsNullOrEmpty(newVersion)) newVersion = PackageVersion;
            return null;
        }

        public static void RevertToEmbedded()
        {
            if (File.Exists(OverrideZipPath))
                File.Delete(OverrideZipPath);
            ReloadMetadata();
        }
    }

    public static class AnrInstaller
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public static string GetDlssgDir(string gameDir)
        {
            return Path.Combine(gameDir, @"OptiScaler\custom\" + PackageStore.ProductDir + @"\dlssg");
        }

        public static GameState GetGameState(string gameDir)
        {
            GameState s = new GameState();
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir)) return s;
            try
            {
                if (Directory.GetFiles(gameDir, "*.exe").Length == 0) return s;
            }
            catch { return s; }

            s.Valid = true;
            string opti = Path.Combine(gameDir, "OptiScaler");
            string record = Path.Combine(opti, "nr_fg_install.json");
            if (File.Exists(record))
            {
                s.Installed = true;
                try
                {
                    Dictionary<string, object> info = JsonHelper.ParseObject(File.ReadAllText(record, Encoding.UTF8));
                    string px = JsonHelper.GetString(info, "proxy", "");
                    s.Proxy = string.IsNullOrEmpty(px) ? "dxgi.dll" : px;
                    s.InstalledAt = JsonHelper.GetString(info, "installed_at", null);
                }
                catch
                {
                    s.Proxy = "dxgi.dll";
                }
            }
            else if (Directory.Exists(opti))
            {
                s.Foreign = true;
            }
            return s;
        }

        private static string TryGetProcessPath(Process p)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                if (h == IntPtr.Zero) return null;
                StringBuilder sb = new StringBuilder(1024);
                int len = sb.Capacity;
                if (QueryFullProcessImageName(h, 0, sb, ref len))
                    return sb.ToString(0, len);
            }
            catch { }
            finally
            {
                if (h != IntPtr.Zero) CloseHandle(h);
            }
            return null;
        }

        private static string FindRunningInFolder(string gameDir)
        {
            string full = Path.GetFullPath(gameDir).TrimEnd('\\') + "\\";
            Process[] procs = Process.GetProcesses();
            for (int i = 0; i < procs.Length; i++)
            {
                try
                {
                    string path = TryGetProcessPath(procs[i]);
                    if (!string.IsNullOrEmpty(path) && path.StartsWith(full, StringComparison.OrdinalIgnoreCase))
                        return procs[i].ProcessName;
                }
                catch { }
                finally
                {
                    try { procs[i].Dispose(); } catch { }
                }
            }
            return null;
        }

        public static string Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static string DetectAutoProxy(string gameDir)
        {
            string largest = null;
            long maxSize = -1;
            string[] exes = Directory.GetFiles(gameDir, "*.exe");
            for (int i = 0; i < exes.Length; i++)
            {
                long sz = new FileInfo(exes[i]).Length;
                if (sz > maxSize) { maxSize = sz; largest = exes[i]; }
            }
            if (string.IsNullOrEmpty(largest)) return "dxgi.dll";

            string[] names = { "vulkan-1.dll", "dxgi.dll", "d3d12.dll", "d3d11.dll" };
            bool[] found = Scanner.ScanSpecificDlls(largest, names);
            if (found[0] && !found[1] && !found[2] && !found[3])
                return "winmm.dll";
            return "dxgi.dll";
        }

        public static void Install(string gameDir, string forcedProxy, Action<string> log)
        {
            if (string.IsNullOrEmpty(gameDir))
                throw new InvalidOperationException("Nenhuma pasta de jogo informada.");
            gameDir = gameDir.Trim().Trim('"');
            if (!Directory.Exists(gameDir) || Directory.GetFiles(gameDir, "*.exe").Length == 0)
                throw new InvalidOperationException("Nenhum .exe encontrado em " + gameDir);

            string running = FindRunningInFolder(gameDir);
            if (!string.IsNullOrEmpty(running))
                throw new InvalidOperationException("Feche o jogo primeiro (" + running + " está em execução); nada foi alterado.");

            string opti = Path.Combine(gameDir, "OptiScaler");
            string record = Path.Combine(opti, "nr_fg_install.json");
            string backup = Path.Combine(opti, "nr_fg_backup");

            if (Directory.Exists(opti) && !File.Exists(record))
                throw new InvalidOperationException(opti + " já existe e não é deste pacote; nada foi alterado.");

            string proxy = forcedProxy;
            if (File.Exists(record))
            {
                Dictionary<string, object> prevObj = JsonHelper.ParseObject(File.ReadAllText(record, Encoding.UTF8));
                string prevProxy = JsonHelper.GetString(prevObj, "proxy", "");
                proxy = string.IsNullOrEmpty(prevProxy) ? "dxgi.dll" : prevProxy;
            }
            if (string.IsNullOrEmpty(proxy))
                proxy = DetectAutoProxy(gameDir);

            if (proxy != "dxgi.dll" && proxy != "winmm.dll" && proxy != "version.dll")
                throw new InvalidOperationException("O OptiScaler carrega como dxgi.dll, winmm.dll ou version.dll, não " + proxy);

            Directory.CreateDirectory(backup);

            Dictionary<string, object> originals = new Dictionary<string, object>();
            Dictionary<string, object> info = new Dictionary<string, object>();
            info["originals"] = originals;
            info["proxy"] = proxy;

            if (File.Exists(record))
            {
                Dictionary<string, object> oldInfo = JsonHelper.ParseObject(File.ReadAllText(record, Encoding.UTF8));
                Dictionary<string, object> oldOrig = JsonHelper.GetDict(oldInfo, "originals");
                if (oldOrig != null)
                {
                    foreach (KeyValuePair<string, object> kv in oldOrig)
                        originals[kv.Key] = kv.Value;
                }
            }
            else
            {
                string[] tracked = new string[] { proxy, "OptiScaler.ini" };
                for (int i = 0; i < tracked.Length; i++)
                {
                    string f = tracked[i];
                    string p = Path.Combine(gameDir, f);
                    if (File.Exists(p))
                    {
                        string bakFile = Path.Combine(backup, f);
                        File.Copy(p, bakFile, true);
                        string h = Sha256File(p);
                        if (!string.Equals(Sha256File(bakFile), h, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("A cópia de backup de " + f + " não confere.");
                        originals[f] = h;
                    }
                }
                File.WriteAllText(record, JsonHelper.Serialize(info), Encoding.ASCII);
            }

            string newCustom = Path.Combine(gameDir, @"OptiScaler\custom\" + PackageStore.ProductDir);
            string oldCustom = Path.Combine(gameDir, @"OptiScaler\custom\nrfg");
            if (Directory.Exists(oldCustom))
            {
                if (Directory.Exists(newCustom))
                    Directory.Delete(oldCustom, true);
                else
                    Directory.Move(oldCustom, newCustom);
            }

            string iniPath = Path.Combine(newCustom, @"dlssg\nr_fg.ini");
            string keepPath = iniPath + ".keep";
            bool keep = File.Exists(iniPath);
            if (keep)
                File.Copy(iniPath, keepPath, true);

            string gameFull = Path.GetFullPath(gameDir).TrimEnd('\\');
            string gamePrefix = gameFull + "\\";
            int copied = 0;

            using (Stream zs = PackageStore.OpenActiveZipStream())
            using (ZipArchive z = new ZipArchive(zs, ZipArchiveMode.Read))
            {
                string prefix = PackageStore.DetectPrefix(z);
                string gameEntryPrefix = prefix + "game/";
                for (int i = 0; i < z.Entries.Count; i++)
                {
                    ZipArchiveEntry e = z.Entries[i];
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    string norm = e.FullName.Replace('\\', '/');
                    if (!norm.StartsWith(gameEntryPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = norm.Substring(gameEntryPrefix.Length);
                    if (string.IsNullOrEmpty(rel)) continue;

                    string dest;
                    if (string.Equals(rel, "dxgi.dll", StringComparison.OrdinalIgnoreCase))
                        dest = Path.Combine(gameFull, proxy);
                    else
                        dest = Path.GetFullPath(Path.Combine(gameFull, rel.Replace('/', '\\')));

                    if (!dest.StartsWith(gamePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string parent = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                    using (Stream es = e.Open())
                    using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                        es.CopyTo(fs);
                    copied++;
                }
            }

            if (keep && File.Exists(keepPath))
            {
                if (File.Exists(iniPath)) File.Delete(iniPath);
                File.Move(keepPath, iniPath);
            }

            Dictionary<string, object> installedMap = new Dictionary<string, object>();
            string[] checkFiles = new string[] { proxy, "OptiScaler.ini" };
            for (int i = 0; i < checkFiles.Length; i++)
            {
                string f = checkFiles[i];
                string fp = Path.Combine(gameDir, f);
                if (File.Exists(fp)) installedMap[f] = Sha256File(fp);
            }
            info["installed"] = installedMap;
            info["installed_at"] = DateTime.UtcNow.ToString("o");
            File.WriteAllText(record, JsonHelper.Serialize(info), Encoding.ASCII);

            if (log != null)
                log("ANR FG (Apex Neural Rendering) instalado em " + gameDir + " (" + copied + " arquivos), OptiScaler como " + proxy + " (manteve nr_fg.ini: " + (keep ? "sim" : "não") + ").");

            string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string[] crt = new string[] { "MSVCP140.dll", "MSVCP140_ATOMIC_WAIT.dll", "VCRUNTIME140.dll", "VCRUNTIME140_1.dll" };
            List<string> missing = new List<string>();
            for (int i = 0; i < crt.Length; i++)
            {
                if (!File.Exists(Path.Combine(sys32, crt[i]))) missing.Add(crt[i]);
            }
            if (missing.Count > 0 && log != null)
            {
                log("AVISO: O OptiScaler precisa do Microsoft Visual C++ 2015-2022 Redistributable (x64), ausente neste PC (" +
                    string.Join(", ", missing.ToArray()) + "): https://aka.ms/vs/17/release/vc_redist.x64.exe");
            }
        }

        public static void Uninstall(string gameDir, Action<string> log)
        {
            if (string.IsNullOrEmpty(gameDir))
                throw new InvalidOperationException("Nenhuma pasta de jogo informada.");
            gameDir = gameDir.Trim().Trim('"');

            string running = FindRunningInFolder(gameDir);
            if (!string.IsNullOrEmpty(running))
                throw new InvalidOperationException("Feche o jogo primeiro (" + running + " está em execução); nada foi alterado.");

            string opti = Path.Combine(gameDir, "OptiScaler");
            string record = Path.Combine(opti, "nr_fg_install.json");
            if (!File.Exists(record))
                throw new InvalidOperationException("Nenhuma instalação nossa encontrada em " + gameDir);

            Dictionary<string, object> info = JsonHelper.ParseObject(File.ReadAllText(record, Encoding.UTF8));
            string proxy = JsonHelper.GetString(info, "proxy", "");
            if (string.IsNullOrEmpty(proxy)) proxy = "dxgi.dll";
            Dictionary<string, object> originals = JsonHelper.GetDict(info, "originals");

            string[] tracked = new string[] { proxy, "OptiScaler.ini" };
            for (int i = 0; i < tracked.Length; i++)
            {
                string f = tracked[i];
                string current = Path.Combine(gameDir, f);
                string bak = Path.Combine(Path.Combine(opti, "nr_fg_backup"), f);
                string origHash = JsonHelper.GetString(originals, f, null);
                if (!string.IsNullOrEmpty(origHash))
                {
                    if (!File.Exists(bak) || !string.Equals(Sha256File(bak), origHash, StringComparison.OrdinalIgnoreCase))
                        throw new IOException(bak + " não corresponde ao arquivo original; nada foi alterado.");
                    File.Copy(bak, current, true);
                }
                else if (File.Exists(current))
                {
                    File.Delete(current);
                }
            }

            try
            {
                string optiLog = Path.Combine(gameDir, "OptiScaler.log");
                if (File.Exists(optiLog)) File.Delete(optiLog);
            }
            catch { }

            try
            {
                string optiBak = Path.Combine(gameDir, "OptiScaler.ini.anrbak");
                if (File.Exists(optiBak)) File.Delete(optiBak);
            }
            catch { }

            Directory.Delete(opti, true);

            if (log != null)
                log("ANR FG (Apex Neural Rendering) desinstalado de " + gameDir + ".");
        }
    }

    public class GpuInfo
    {
        public string Name = "Não identificada";
        public string Vendor = "unknown";
        public string Arch = "";
        public int Width;
        public int Height;
        public string SizeClass = "1080p";
        public string Warn = "";
    }

    public class Recommendation
    {
        public string Path = "auto";
        public int MaxGen = 1;
        public bool Scene = true;
        public string Proxy = "dxgi.dll";
        public List<string> Lines = new List<string>();
        public string Source = "";
    }

    public static class HardwareAdvisor
    {
        private static GpuInfo cachedGpu;

        public static GpuInfo GetGpuInfo()
        {
            if (cachedGpu != null) return cachedGpu;
            GpuInfo info = new GpuInfo();

            try
            {
                List<GpuInfo> all = new List<GpuInfo>();
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string name = obj["Name"] as string;
                        if (string.IsNullOrEmpty(name)) continue;
                        if (Regex.IsMatch(name, @"Microsoft|Basic Render|Remote|Virtual|Parsec|Meta|Citrix", RegexOptions.IgnoreCase))
                            continue;
                        GpuInfo item = new GpuInfo();
                        item.Name = name;
                        if (obj["CurrentHorizontalResolution"] != null) item.Width = Convert.ToInt32(obj["CurrentHorizontalResolution"]);
                        if (obj["CurrentVerticalResolution"] != null) item.Height = Convert.ToInt32(obj["CurrentVerticalResolution"]);
                        all.Add(item);
                    }
                }

                GpuInfo pick = null;
                for (int i = 0; i < all.Count; i++)
                {
                    if (Regex.IsMatch(all[i].Name, @"RTX|GTX|GeForce|Radeon RX|\bRX\s*\d|Arc", RegexOptions.IgnoreCase))
                    {
                        pick = all[i];
                        break;
                    }
                }
                if (pick == null && all.Count > 0) pick = all[0];
                if (pick != null)
                {
                    info.Name = pick.Name;
                    info.Width = pick.Width;
                    info.Height = pick.Height;
                }
            }
            catch { }

            if (info.Width <= 0 || info.Height <= 0)
            {
                System.Drawing.Rectangle b = Screen.PrimaryScreen.Bounds;
                info.Width = b.Width;
                info.Height = b.Height;
            }

            string n = info.Name;
            if (Regex.IsMatch(n, @"NVIDIA|GeForce|RTX|GTX|Quadro", RegexOptions.IgnoreCase))
            {
                info.Vendor = "nvidia";
                if (Regex.IsMatch(n, @"RTX\s*50\d\d", RegexOptions.IgnoreCase)) info.Arch = "blackwell";
                else if (Regex.IsMatch(n, @"RTX\s*40\d\d|RTX\s*\d000\s*Ada", RegexOptions.IgnoreCase)) info.Arch = "ada";
                else if (Regex.IsMatch(n, @"RTX\s*30\d\d|RTX\s*A\d", RegexOptions.IgnoreCase)) info.Arch = "ampere";
                else if (Regex.IsMatch(n, @"RTX\s*20\d\d|GTX\s*16\d\d|TITAN RTX|Quadro RTX", RegexOptions.IgnoreCase)) info.Arch = "turing";
                else info.Warn = "O site lista como compatíveis as RTX série 20 ou mais novas e as GTX série 16. Esta GPU pode não ser suportada.";
            }
            else if (Regex.IsMatch(n, @"AMD|Radeon", RegexOptions.IgnoreCase))
            {
                info.Vendor = "amd";
                if (Regex.IsMatch(n, @"RX\s*\d{3}\b", RegexOptions.IgnoreCase) && !Regex.IsMatch(n, @"RX\s*\d{4}", RegexOptions.IgnoreCase))
                    info.Warn = "O site lista Radeon RX 5000 ou mais novas e Steam Deck. Esta GPU pode não ser suportada.";
            }
            else if (Regex.IsMatch(n, @"Intel", RegexOptions.IgnoreCase))
            {
                info.Vendor = "intel";
                if (!Regex.IsMatch(n, @"Arc", RegexOptions.IgnoreCase))
                    info.Warn = "O site lista a Intel Arc (séries A e B). Gráficos integrados Intel podem não ser suportados.";
            }
            else if (Regex.IsMatch(n, @"Apple", RegexOptions.IgnoreCase))
            {
                info.Vendor = "apple";
            }

            long px = (long)info.Width * info.Height;
            info.SizeClass = (px <= 2073600) ? "1080p" : (px <= 3686400 ? "1440p" : "2160p");
            cachedGpu = info;
            return info;
        }

        public static Recommendation GetRecommendation(GameInfo game)
        {
            GpuInfo gpu = GetGpuInfo();
            Recommendation r = new Recommendation();
            List<string> L = r.Lines;
            if (!string.IsNullOrEmpty(gpu.Warn)) L.Add("ATENÇÃO: " + gpu.Warn);

            Dictionary<string, object> entry = null;
            string tblJson = PackageStore.ReadText(PackageStore.RelSizeTable);
            if (!string.IsNullOrEmpty(tblJson))
            {
                try
                {
                    Dictionary<string, object> tbl = JsonHelper.ParseObject(tblJson);
                    List<object> entries = JsonHelper.GetList(tbl, "entries");
                    if (entries != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            Dictionary<string, object> e = entries[i] as Dictionary<string, object>;
                            if (e == null) continue;
                            string vendor = JsonHelper.GetString(e, "vendor", "");
                            string arch = JsonHelper.GetString(e, "architecture", "");
                            if (string.Equals(vendor, gpu.Vendor, StringComparison.OrdinalIgnoreCase) &&
                                (string.Equals(arch, gpu.Arch, StringComparison.OrdinalIgnoreCase) || arch == "*"))
                            {
                                entry = e;
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            List<object> range = null;
            if (entry != null)
            {
                Dictionary<string, object> large = JsonHelper.GetDict(entry, "large");
                range = JsonHelper.GetList(large, gpu.SizeClass);
            }

            if (range != null && range.Count >= 2)
            {
                int rMin = Convert.ToInt32(range[0]);
                int rMax = Convert.ToInt32(range[1]);
                r.MaxGen = Math.Min(5, Math.Max(1, rMax - 1));
                r.Source = JsonHelper.GetString(entry, "source", "");
                string fonte = (r.Source == "measured") ? "medido" : "estimado";
                string entryPath = JsonHelper.GetString(entry, "path", "auto");
                L.Add("Para a sua GPU em " + gpu.SizeClass + ", a tabela do pacote indica de " + rMin + "X até " + rMax + "X (" + fonte + "). Recomendado: até " + (r.MaxGen + 1) + "X.");
                L.Add("Caminho de convolução: deixe em 'auto'; para esta GPU o pacote mede o caminho '" + entryPath + "' e o 'auto' já o usa.");
            }
            else if (entry != null)
            {
                L.Add("A tabela do pacote não tem medição para a sua GPU em " + gpu.SizeClass + ". Comece em 2X e aumente aos poucos, observando o desempenho.");
            }
            else
            {
                L.Add("O pacote não traz medições para a sua GPU. Comece em 2X com o caminho 'auto' e aumente aos poucos se o desempenho permitir.");
            }

            L.Add("Cortes de cena: deixe ligado, para não interpolar entre uma cena e outra.");

            if (game != null)
            {
                if (game.Api.IndexOf("D3D12", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    r.Proxy = "dxgi.dll";
                    L.Add("Jogo: usa D3D12, o caso ideal. O OptiScaler entra como dxgi.dll.");
                }
                else if (string.Equals(game.Api, "Vulkan", StringComparison.OrdinalIgnoreCase))
                {
                    r.Proxy = "winmm.dll";
                    L.Add("Jogo: parece usar só Vulkan. O instalador usa winmm.dll nesses casos (modo Automático).");
                }
                else if (game.Api.IndexOf("Vulkan", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    L.Add("Jogo: usa D3D e Vulkan. Se o OptiScaler não aparecer com dxgi.dll, tente winmm.dll na aba Instalação.");
                }
                else if (game.Api.IndexOf("D3D11", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    L.Add("Jogo: parece usar só D3D11. O ANR FG é para jogos D3D12, então pode não funcionar.");
                }
                else
                {
                    L.Add("Jogo: não deu para identificar a API gráfica. Instale, teste e, se algo falhar, veja os logs.");
                }
            }
            else
            {
                L.Add("Selecione um jogo na lista para ver recomendações específicas (API gráfica e DLL do OptiScaler).");
            }

            L.Add("No jogo, ligue o DLSS Frame Generation: o ANR FG assume o lugar dele.");
            L.Add("No primeiro teste, deixe o log ligado. Ao aplicar, o OptiScaler.log é ativado para facilitar o diagnóstico.");
            return r;
        }
    }
}
