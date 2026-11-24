using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace AnrFgManager
{
    // Dados e rotinas de instalacao usadas pelo instalador e pelo proprio programa (desinstalar).
    public static class InstallInfo
    {
        public const string AppName = "ANR FG Manager";
        public const string ExeName = "ANR-FG-Manager.exe";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ANRFGManager";

        public static string DefaultDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Path.Combine("Programs", AppName));
            }
        }

        public static string StartMenuFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName); }
        }

        public static string StartMenuLnk { get { return Path.Combine(StartMenuFolder, AppName + ".lnk"); } }

        public static string DesktopLnk
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"); }
        }

        public static string UserDataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ANR-FG-Manager"); }
        }

        public static string GetInstalledDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    if (k == null) return null;
                    string dir = k.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, ExeName))) return dir;
                }
            }
            catch { }
            return null;
        }

        public static string GetInstalledVersion()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    if (k == null) return null;
                    return k.GetValue("DisplayVersion") as string;
                }
            }
            catch { }
            return null;
        }

        private static string NormDir(string d)
        {
            try { return Path.GetFullPath(d).TrimEnd('\\'); }
            catch { return (d ?? "").TrimEnd('\\'); }
        }

        // o executavel em uso esta na pasta registrada pelo instalador?
        public static bool IsRunningFromInstallDir()
        {
            string dir = GetInstalledDir();
            if (dir == null) return false;
            string me = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            return string.Equals(NormDir(me), NormDir(dir), StringComparison.OrdinalIgnoreCase);
        }

        public static void CreateShortcut(string lnkPath, string target, string workDir, string description)
        {
            string folder = Path.GetDirectoryName(lnkPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) throw new InvalidOperationException("Não foi possível criar atalhos neste Windows.");
            object shell = Activator.CreateInstance(t);
            object lnk = null;
            try
            {
                lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { workDir });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { description });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            finally
            {
                if (lnk != null) Marshal.ReleaseComObject(lnk);
                Marshal.ReleaseComObject(shell);
            }
        }

        public static void RemoveShortcuts()
        {
            try { if (File.Exists(DesktopLnk)) File.Delete(DesktopLnk); } catch { }
            try { if (File.Exists(StartMenuLnk)) File.Delete(StartMenuLnk); } catch { }
            try { if (Directory.Exists(StartMenuFolder)) Directory.Delete(StartMenuFolder, false); } catch { }
        }

        public static long DirSizeKb(string dir)
        {
            long total = 0;
            try
            {
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++) total += new FileInfo(files[i]).Length;
            }
            catch { }
            return total / 1024;
        }

        // entrada em "Aplicativos e recursos" / "Adicionar ou remover programas" (so para o usuario atual)
        public static void RegisterUninstall(string dir, string version)
        {
            string exe = Path.Combine(dir, ExeName);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", version ?? "");
                k.SetValue("Publisher", AppName);
                k.SetValue("InstallLocation", dir);
                k.SetValue("DisplayIcon", exe);
                k.SetValue("UninstallString", "\"" + exe + "\" /uninstall");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)DirSizeKb(dir), RegistryValueKind.DWord);
            }
        }

        public static void UnregisterUninstall()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
        }
    }

    // desinstalacao: executada pelo proprio programa com o argumento /uninstall
    public static class Uninstaller
    {
        public static void Run()
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string dir = Path.GetDirectoryName(exe);

            if (!InstallInfo.IsRunningFromInstallDir())
            {
                Theme.ShowMsg(null,
                    "Este é o executável portátil (não foi instalado pelo instalador).\n\nPara remover, basta apagar o arquivo ANR-FG-Manager.exe.",
                    "Desinstalar", false);
                return;
            }
            if (!Theme.ShowMsg(null,
                "Desinstalar o ANR FG Manager?\n\nOs seus jogos e o ANR FG já instalado neles não serão alterados.",
                "Desinstalar", true)) return;

            bool wipe = Theme.ShowMsg(null,
                "Remover também as configurações, o cache de jogos e as imagens do programa?\n(" + InstallInfo.UserDataDir + ")",
                "Desinstalar", true);

            InstallInfo.RemoveShortcuts();
            InstallInfo.UnregisterUninstall();
            if (wipe)
            {
                try { if (Directory.Exists(InstallInfo.UserDataDir)) Directory.Delete(InstallInfo.UserDataDir, true); } catch { }
            }

            // a pasta so pode ser apagada depois que este processo terminar
            try
            {
                string bat = Path.Combine(Path.GetTempPath(), "anrfg-uninstall-" + Guid.NewGuid().ToString("N") + ".cmd");
                File.WriteAllText(bat,
                    "@echo off\r\nping 127.0.0.1 -n 4 >nul\r\nrmdir /s /q \"" + dir + "\"\r\ndel \"%~f0\"\r\n",
                    Encoding.Default);
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
            Theme.ShowMsg(null, "ANR FG Manager removido.", "Desinstalar", false);
        }
    }
}
