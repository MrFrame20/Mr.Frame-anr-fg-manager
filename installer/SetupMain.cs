using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace AnrFgManager
{
    static class SetupProgram
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool silent = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "/silent", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "/update", StringComparison.OrdinalIgnoreCase))
                    silent = true;
            }

            try
            {
                if (silent) return SetupCore.RunSilent();
                Application.Run(new SetupForm());
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro no instalador:\n\n" + ex.Message, "ANR FG Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    static class SetupCore
    {
        public static Process[] RunningManagers()
        {
            Process[] all = Process.GetProcessesByName("ANR-FG-Manager");
            int me = Process.GetCurrentProcess().Id;
            System.Collections.Generic.List<Process> list = new System.Collections.Generic.List<Process>();
            for (int i = 0; i < all.Length; i++) if (all[i].Id != me) list.Add(all[i]);
            return list.ToArray();
        }

        // fecha o programa se estiver aberto (pergunta antes, quando ha janela)
        public static bool CloseRunning(IWin32Window owner, bool ask)
        {
            Process[] running = RunningManagers();
            if (running.Length == 0) return true;
            if (ask && !Theme.ShowMsg(owner, "O ANR FG Manager está aberto.\n\nFechar agora para continuar a instalação?", "Instalador", true))
                return false;
            for (int i = 0; i < running.Length; i++)
            {
                try
                {
                    running[i].CloseMainWindow();
                    if (!running[i].WaitForExit(3000)) running[i].Kill();
                }
                catch { }
            }
            Thread.Sleep(400);
            return true;
        }

        // grava o executavel embutido na pasta de instalacao
        public static string ExtractPayload(string dir)
        {
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, InstallInfo.ExeName);
            string tmp = target + ".new";
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.exe"))
            {
                if (s == null) throw new InvalidOperationException("O programa não foi encontrado dentro do instalador.");
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                }
            }
            string old = target + ".old";
            try { if (File.Exists(old)) File.Delete(old); } catch { }
            if (File.Exists(target))
            {
                try { File.Move(target, old); }
                catch { try { File.Delete(target); } catch { } }
            }
            File.Move(tmp, target);
            try { if (File.Exists(old)) File.Delete(old); } catch { }
            return target;
        }

        public static string ReadVersion(string exe)
        {
            try
            {
                string v = FileVersionInfo.GetVersionInfo(exe).FileVersion;
                Version ver;
                if (Version.TryParse(v, out ver)) return ver.ToString(3);
                return v ?? "";
            }
            catch { return ""; }
        }

        public static void MakeShortcuts(string exe, bool startMenu, bool desktop)
        {
            string dir = Path.GetDirectoryName(exe);
            if (startMenu) InstallInfo.CreateShortcut(InstallInfo.StartMenuLnk, exe, dir, InstallInfo.AppName);
            if (desktop) InstallInfo.CreateShortcut(InstallInfo.DesktopLnk, exe, dir, InstallInfo.AppName);
        }

        // modo silencioso (/update): usado pela atualizacao automatica
        public static int RunSilent()
        {
            string dir = InstallInfo.GetInstalledDir();
            bool firstTime = (dir == null);
            if (firstTime) dir = InstallInfo.DefaultDir;

            // o programa que pediu a atualizacao esta fechando: espera ate 40 s
            for (int i = 0; i < 80 && RunningManagers().Length > 0; i++) Thread.Sleep(500);
            CloseRunning(null, false);

            try
            {
                bool hadStart = File.Exists(InstallInfo.StartMenuLnk);
                bool hadDesktop = File.Exists(InstallInfo.DesktopLnk);
                string exe = ExtractPayload(dir);
                MakeShortcuts(exe, firstTime || hadStart, hadDesktop);
                InstallInfo.RegisterUninstall(dir, ReadVersion(exe));
                Process.Start(exe);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível atualizar o ANR FG Manager:\n\n" + ex.Message, "ANR FG Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    // barra de progresso simples, arredondada
    public class MiniProgress : Control
    {
        private int value;

        public MiniProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 12;
        }

        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath tp = Theme.RoundPath(track, Height / 2))
            {
                g.FillPath(Theme.BrInput, tp);
                g.DrawPath(Theme.PenBorder, tp);
            }
            int w = (int)((Width - 1) * (value / 100.0));
            if (w > Height)
            {
                using (GraphicsPath fp = Theme.RoundPath(new Rectangle(0, 0, w, Height - 1), Height / 2))
                {
                    g.FillPath(Theme.BrAccent, fp);
                }
            }
        }
    }

    public class SetupForm : Form
    {
        private RoundBox txtDir;
        private Button btnBrowse, btnInstall, btnCancel, btnFinish;
        private DarkCheck chkDesktop, chkStart, chkLaunch;
        private Label lblStatus, lblDone, lblExisting;
        private MiniProgress progress;
        private Control[] optionCtls;
        private Control[] progressCtls;
        private Control[] doneCtls;
        private static readonly Font FontLogo = new Font("Segoe UI", 20f, FontStyle.Bold);
        private static readonly Font FontBrand = new Font("Segoe UI", 17f, FontStyle.Bold);
        private string version = "";
        private string installedExe = "";

        public SetupForm()
        {
            Text = "Instalador - ANR FG Manager";
            ClientSize = new Size(640, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = Theme.Font;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Theme.SetDarkTitle(this);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // versao do programa embutido (lida do proprio .exe embutido, gravado numa pasta temporaria)
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "anrfg-setup-" + Guid.NewGuid().ToString("N"));
                string exe = SetupCore.ExtractPayload(tmp);
                version = SetupCore.ReadVersion(exe);
                try { Directory.Delete(tmp, true); } catch { }
            }
            catch { }

            BuildUi();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle logo = new Rectangle(30, 26, 52, 52);
            using (GraphicsPath p = Theme.RoundPath(logo, 14))
            {
                g.FillPath(Theme.BrAccent, p);
            }
            TextRenderer.DrawText(g, "A", FontLogo, logo, Color.FromArgb(5, 26, 30),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "ANR FG Manager", FontBrand, new Point(96, 24), Theme.Text);
            TextRenderer.DrawText(g, "Instalador" + (version.Length > 0 ? "  ·  versão " + version : ""), Theme.Font, new Point(98, 56), Theme.Muted);
            g.DrawLine(Theme.PenBorder, 30, 100, ClientSize.Width - 30, 100);
        }

        private void BuildUi()
        {
            string existingDir = InstallInfo.GetInstalledDir();
            string existingVer = InstallInfo.GetInstalledVersion();

            Label desc = new Label();
            desc.Text = "Gerenciador do ANR FG (Apex Neural Rendering) com OptiScaler: encontra os seus jogos, instala e configura o frame generation, e ainda abre o jogo pelo botão Jogar.";
            desc.Location = new Point(30, 116);
            desc.Size = new Size(580, 44);
            desc.ForeColor = Theme.Muted;

            lblExisting = new Label();
            lblExisting.Location = new Point(30, 164);
            lblExisting.Size = new Size(580, 22);
            lblExisting.ForeColor = Theme.Warn;
            if (existingDir != null)
                lblExisting.Text = "Já instalado (versão " + (existingVer ?? "?") + "). Esta instalação vai atualizá-lo.";

            Label lblDir = Theme.NewLabel("Pasta de instalação", 30, 196);
            txtDir = Theme.NewInput(30, 222, 470, false);
            txtDir.Text = existingDir ?? InstallInfo.DefaultDir;
            btnBrowse = Theme.NewButton("Procurar...", 510, 222, 100, 34, "ghost");
            btnBrowse.Click += delegate
            {
                string p = Theme.PickFolder(this, "Escolha a pasta onde instalar o ANR FG Manager");
                if (!string.IsNullOrEmpty(p))
                {
                    string leaf = Path.GetFileName(p.TrimEnd('\\'));
                    txtDir.Text = string.Equals(leaf, InstallInfo.AppName, StringComparison.OrdinalIgnoreCase) ? p : Path.Combine(p, InstallInfo.AppName);
                }
            };

            chkStart = Theme.NewCheck("Criar atalho no Menu Iniciar", 28, 282);
            chkStart.Checked = true;
            chkDesktop = Theme.NewCheck("Criar atalho na Área de Trabalho", 28, 310);
            chkDesktop.Checked = true;
            chkLaunch = Theme.NewCheck("Abrir o ANR FG Manager ao terminar", 28, 338);
            chkLaunch.Checked = true;

            btnInstall = Theme.NewButton(existingDir != null ? "Atualizar" : "Instalar", 440, 410, 170, 42, "primary");
            btnCancel = Theme.NewButton("Cancelar", 330, 410, 100, 42, "ghost");
            btnCancel.Click += delegate { Close(); };
            btnInstall.Click += delegate { DoInstall(); };

            optionCtls = new Control[] { desc, lblExisting, lblDir, txtDir, btnBrowse, chkStart, chkDesktop, chkLaunch, btnInstall, btnCancel };

            lblStatus = new Label();
            lblStatus.Location = new Point(30, 200);
            lblStatus.Size = new Size(580, 24);
            lblStatus.ForeColor = Theme.Text;
            progress = new MiniProgress();
            progress.Location = new Point(30, 236);
            progress.Width = 580;
            progressCtls = new Control[] { lblStatus, progress };

            lblDone = new Label();
            lblDone.Location = new Point(30, 160);
            lblDone.Size = new Size(580, 120);
            lblDone.Font = new Font("Segoe UI", 12f);
            lblDone.ForeColor = Theme.Ok;
            btnFinish = Theme.NewButton("Concluir", 440, 410, 170, 42, "primary");
            btnFinish.Click += delegate
            {
                if (chkLaunch.Checked && installedExe.Length > 0)
                {
                    try { Process.Start(installedExe); } catch { }
                }
                Close();
            };
            doneCtls = new Control[] { lblDone, btnFinish };

            Controls.AddRange(optionCtls);
            Controls.AddRange(progressCtls);
            Controls.AddRange(doneCtls);
            SetPage(0);
        }

        private void SetPage(int n)
        {
            for (int i = 0; i < optionCtls.Length; i++) optionCtls[i].Visible = (n == 0);
            for (int i = 0; i < progressCtls.Length; i++) progressCtls[i].Visible = (n == 1);
            for (int i = 0; i < doneCtls.Length; i++) doneCtls[i].Visible = (n == 2);
            if (n == 2) chkLaunch.Visible = true;
        }

        private void Step(int pct, string text)
        {
            progress.Value = pct;
            lblStatus.Text = text;
            Application.DoEvents();
            Thread.Sleep(120);
        }

        private void DoInstall()
        {
            string dir = (txtDir.Text ?? "").Trim().Trim('"');
            if (dir.Length == 0 || !Path.IsPathRooted(dir))
            {
                Theme.ShowMsg(this, "Escolha uma pasta de instalação válida (com a letra do disco, ex.: C:\\Programas\\ANR FG Manager).", "Instalador", false);
                return;
            }

            SetPage(1);
            try
            {
                Step(5, "Verificando se o programa está aberto...");
                if (!SetupCore.CloseRunning(this, true))
                {
                    SetPage(0);
                    return;
                }
                Step(25, "Copiando arquivos...");
                string exe = SetupCore.ExtractPayload(dir);
                installedExe = exe;
                string ver = SetupCore.ReadVersion(exe);

                Step(60, "Criando atalhos...");
                SetupCore.MakeShortcuts(exe, chkStart.Checked, chkDesktop.Checked);

                Step(85, "Registrando em Aplicativos e recursos do Windows...");
                InstallInfo.RegisterUninstall(dir, ver);

                Step(100, "Pronto.");
                lblDone.Text = "Instalação concluída!\r\n\r\nANR FG Manager " + ver + " foi instalado em:\r\n" + dir;
                SetPage(2);
            }
            catch (Exception ex)
            {
                SetPage(0);
                Theme.ShowMsg(this, "Não foi possível instalar:\n\n" + ex.Message, "Instalador", false);
            }
        }
    }
}
