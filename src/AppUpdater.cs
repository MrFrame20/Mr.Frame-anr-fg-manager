using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AnrFgManager
{
    public class ReleaseInfo
    {
        public string Tag = "";
        public string Name = "";
        public string Notes = "";
        public string Page = "";
        public string VersionText = "";
        public Version Ver;
        public string ExeUrl = "";
        public string ExeSha = "";
        public string SetupUrl = "";
        public string SetupSha = "";
        public int PkgNumber = -1;
        public string PkgText = "";
        public string PkgUrl = "";
        public string PkgSha = "";
    }

    // atualizacao do proprio programa pelas Releases do GitHub
    public static class AppUpdater
    {
        // repositorio padrao (usuario/repositorio). Pode ser trocado em Atualizacoes.
        public const string DefaultRepo = "";

        // repositorio oficial do pacote ANR FG (releases como FG-V.B20, com anr-fg-...-full.zip)
        public const string DefaultPackageRepo = "apex-compute/ANR";

        public static string NormalizeRepo(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            string s = input.Trim();
            Match m = Regex.Match(s, @"github\.com[/:]([A-Za-z0-9_.\-]+)/([A-Za-z0-9_.\-]+?)(?:\.git)?(?:[/?#].*)?$", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value + "/" + m.Groups[2].Value;
            m = Regex.Match(s, @"^([A-Za-z0-9_.\-]+)/([A-Za-z0-9_.\-]+)$");
            if (m.Success) return m.Groups[1].Value + "/" + m.Groups[2].Value;
            return null;
        }

        // versao do PROGRAMA: precisa ter o formato numero.numero (ex.: v2.3.0). Tags como FG-V.B20 nao contam.
        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            Match m = Regex.Match(text, @"(\d+\.\d+(?:\.\d+){0,2})");
            if (!m.Success) return null;
            Version r;
            return Version.TryParse(m.Groups[1].Value, out r) ? r : null;
        }

        // versao do PACOTE ANR FG: o numero depois do B (V.B20 -> 20)
        public static int ParsePackageNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            Match m = Regex.Match(text, @"[Bb]\s*(\d+)");
            int n;
            if (m.Success && int.TryParse(m.Groups[1].Value, out n)) return n;
            return -1;
        }

        public static bool IsPackageNewer(ReleaseInfo r, string currentVersion)
        {
            int cur = ParsePackageNumber(currentVersion);
            return r != null && r.PkgNumber > 0 && cur > 0 && r.PkgNumber > cur;
        }

        public static bool IsNewer(ReleaseInfo r)
        {
            Version cur = ParseVersion(PackageStore.AppVersion);
            return r != null && r.Ver != null && cur != null && r.Ver > cur;
        }

        private static bool IsTrustedUrl(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
            if (u.Scheme != Uri.UriSchemeHttps) return false;
            string h = u.Host.ToLowerInvariant();
            return h == "github.com" || h.EndsWith(".github.com") || h.EndsWith(".githubusercontent.com");
        }

        private static string ShaFromDigest(string digest)
        {
            if (string.IsNullOrEmpty(digest)) return "";
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return "";
            return digest.Substring(7).Trim().ToLowerInvariant();
        }

        // consulta a ultima release publicada (sincrono, ate ~15 s)
        public static ReleaseInfo CheckLatest(string repo, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(repo)) { error = "Repositório não informado."; return null; }
            string json;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + repo + "/releases/latest");
                req.UserAgent = "ANR-FG-Manager/" + PackageStore.AppVersion;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                using (WebResponse resp = req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse hr = wex.Response as HttpWebResponse;
                if (hr != null && hr.StatusCode == HttpStatusCode.NotFound)
                    error = "Nenhuma release encontrada em " + repo + " (confira o nome do repositório; ele precisa ser público e ter ao menos uma release publicada).";
                else if (hr != null && hr.StatusCode == HttpStatusCode.Forbidden)
                    error = "O GitHub recusou a consulta (limite de consultas por hora). Tente de novo mais tarde.";
                else
                    error = "Não foi possível consultar o GitHub: " + wex.Message;
                return null;
            }
            catch (Exception ex)
            {
                error = "Não foi possível consultar o GitHub: " + ex.Message;
                return null;
            }

            Dictionary<string, object> d = JsonHelper.ParseObject(json);
            if (d == null) { error = "Resposta inesperada do GitHub."; return null; }
            if (JsonHelper.GetBool(d, "draft", false)) { error = "Nenhuma release publicada."; return null; }

            ReleaseInfo r = new ReleaseInfo();
            r.Tag = JsonHelper.GetString(d, "tag_name", "");
            r.Name = JsonHelper.GetString(d, "name", r.Tag);
            r.Notes = JsonHelper.GetString(d, "body", "");
            r.Page = JsonHelper.GetString(d, "html_url", "");
            r.Ver = ParseVersion(r.Tag);
            if (r.Ver == null) r.Ver = ParseVersion(r.Name);
            r.VersionText = (r.Ver != null) ? r.Ver.ToString() : "";
            Match pm = Regex.Match(r.Tag + " " + r.Name, @"V\.?\s*B\s*(\d+)", RegexOptions.IgnoreCase);
            if (pm.Success)
            {
                int pn;
                if (int.TryParse(pm.Groups[1].Value, out pn)) { r.PkgNumber = pn; r.PkgText = "V.B" + pm.Groups[1].Value.PadLeft(2, '0'); }
            }

            List<object> assets = JsonHelper.GetList(d, "assets");
            if (assets != null)
            {
                for (int i = 0; i < assets.Count; i++)
                {
                    Dictionary<string, object> a = assets[i] as Dictionary<string, object>;
                    if (a == null) continue;
                    string an = JsonHelper.GetString(a, "name", "");
                    string url = JsonHelper.GetString(a, "browser_download_url", "");
                    string sha = ShaFromDigest(JsonHelper.GetString(a, "digest", ""));
                    if (!IsTrustedUrl(url)) continue;
                    if (string.Equals(an, InstallInfo.ExeName, StringComparison.OrdinalIgnoreCase))
                    {
                        r.ExeUrl = url; r.ExeSha = sha;
                    }
                    else if (an.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && an.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        r.SetupUrl = url; r.SetupSha = sha;
                    }
                    else if (Regex.IsMatch(an, @"^anr-fg-.+-full\.zip$", RegexOptions.IgnoreCase) && an.IndexOf("source", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        r.PkgUrl = url; r.PkgSha = sha;
                    }
                }
            }
            return r;
        }

        public static bool VerifyDownload(string path, string expectedSha, bool isExe, out string error)
        {
            error = null;
            try
            {
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists || fi.Length < 100 * 1024) { error = "O arquivo baixado está incompleto."; return false; }
                byte[] head = new byte[2];
                using (FileStream fs = File.OpenRead(path)) { fs.Read(head, 0, 2); }
                byte b0 = isExe ? (byte)'M' : (byte)'P';
                byte b1 = isExe ? (byte)'Z' : (byte)'K';
                if (head[0] != b0 || head[1] != b1) { error = isExe ? "O arquivo baixado não é um executável válido." : "O arquivo baixado não é um .zip válido."; return false; }
                if (!string.IsNullOrEmpty(expectedSha))
                {
                    using (FileStream fs = File.OpenRead(path))
                    using (SHA256 sha = SHA256.Create())
                    {
                        string hex = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                        if (hex != expectedSha.ToLowerInvariant()) { error = "A verificação SHA-256 do arquivo falhou. A atualização foi cancelada."; return false; }
                    }
                }
                return true;
            }
            catch (Exception ex) { error = "Não foi possível verificar o arquivo: " + ex.Message; return false; }
        }

        // troca o executavel em uso (Windows permite renomear um .exe em execucao) e abre a versao nova
        public static bool ApplyExeSwap(string newExePath, out string error)
        {
            error = null;
            string cur = Process.GetCurrentProcess().MainModule.FileName;
            string old = cur + ".old";
            try
            {
                try { if (File.Exists(old)) File.Delete(old); } catch { }
                File.Move(cur, old);
                try
                {
                    File.Move(newExePath, cur);
                }
                catch
                {
                    try { File.Move(old, cur); } catch { }
                    throw;
                }
                Process.Start(cur);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = "Sem permissão para escrever em " + Path.GetDirectoryName(cur) + ". Use o instalador (Setup) da release, ou mova o programa para uma pasta sua.";
                return false;
            }
            catch (Exception ex)
            {
                error = "Não foi possível trocar o executável: " + ex.Message;
                return false;
            }
        }

        public static bool LaunchSetupUpdate(string setupPath, out string error)
        {
            error = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(setupPath, "/update");
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                error = "Não foi possível abrir o instalador: " + ex.Message;
                return false;
            }
        }

        // apaga o .old deixado pela troca (tenta algumas vezes, o processo antigo pode demorar a fechar)
        public static void CleanupOldInBackground()
        {
            Thread t = new Thread(delegate()
            {
                try
                {
                    string old = Process.GetCurrentProcess().MainModule.FileName + ".old";
                    for (int i = 0; i < 6; i++)
                    {
                        if (!File.Exists(old)) return;
                        try { File.Delete(old); return; } catch { }
                        Thread.Sleep(2500);
                    }
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
