using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AnrFgManager
{
    public class MainForm : AppForm
    {
        private AppSettings settings;
        private readonly ToolTip tip;
        private readonly System.Windows.Forms.Timer timer;
        private Bitmap brandIconBmp;

        // Sidebar
        private Label lblSub;
        private LinkLabel lnkUpdate;
        private RoundBox txtFilter;
        private DarkCombo cmbStore;
        private DarkCombo cmbSort;
        private DarkCheck chkCompat;
        private DarkCheck chkInstalled;
        private DarkCheck chkOther;
        private Label lblScan;
        private Button btnAddGame;
        private Button btnRescan;
        private Button btnSettings;
        private DoubleBufferedListView lvGames;

        // Main detail
        private Panel pEmpty;
        private Panel pSide, pMain, pHeader, pNav, pHost, pBrand, pFilters, pSideBottom;
        private CaptionButton btnMin, btnMax, btnClose;
        private Button btnPlay;
        private Control[] headerGameCtls;
        private CardPanel cardOut, gbFg, gbOpti, cardPc, cardGame, cardReco, cardLog;
        private LinkLabel lblVc;
        private Bitmap heroImg;
        private ReleaseInfo pendingRelease;
        private ReleaseInfo pendingPackage;
        private string headerIconExe = "";
        private string bannerGameKey;
        private string bannerFor = "";
        private string bannerKind = "";
        private WebClient bannerWc;
        private readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();
        private int hoverIdx = -1;
        private NotifyIcon tray;
        private System.Windows.Forms.Timer watchTimer;
        private string watchName = "";
        private bool watchSeen;
        private DateTime watchStart = DateTime.MinValue;
        private FormWindowState prevState = FormWindowState.Normal;
        private Label lblGameName;
        private Label lblMeta;
        private Label lblStatus;
        private Button btnInstall;
        private Button btnUninstall;
        private RoundBox txtGame;
        private Button btnBrowse;
        private Button btnOpenGame;

        // Tabs
        private Panel[] pages;
        private Label[] navLabels;
        private Panel tabInstall, tabFg, tabOpti, tabReco, tabLogs;

        // Install tab
        private DarkCombo cmbProxy;
        private Button btnResetAll;
        private TextBox txtOut;

        // ANR FG tab
        private DarkCombo cmbPath;
        private Button[] genBtns;
        private Label lGenVal;
        private RoundBox txtNits;
        private DarkCheck chkScene;
        private Label lModelVal;
        private Label lBuildVal;
        private Button btnFgSave, btnFgReload, btnFgDefault;
        private Label lblFgMsg;
        private int genValue = 5;

        // OptiScaler tab
        private DarkCombo cmbFgEn, cmbFgIn, cmbFgOut, cmbUp, cmbLvl;
        private DarkCheck chkLog;
        private KeyBox keyBox;
        private Button btnOptiSave, btnOptiReload, btnOptiReset, btnOptiRestore;
        private Label lblOptiMsg;

        // Reco tab
        private Label rGpu, rArch, rRes, rGame, rApi, rProxy, rSummary, lblRecoMsg;
        private TextBox rNotes;
        private Button btnRecoApply, btnRecoRefresh;
        private Recommendation currentReco;
        private bool recoDirty = true;

        // Logs tab
        private DarkCombo cmbLogFile;
        private TextBox txtLog;

        // State
        private readonly List<GameInfo> dgGames = new List<GameInfo>();
        private readonly Dictionary<string, GameInfo> dgIndex = new Dictionary<string, GameInfo>();
        private bool rendering;
        private bool loadingPrefs = true;
        private bool dirtyList;
        private DateTime lastRender = DateTime.MinValue;
        private bool wasRunning;
        private bool rescanPending;
        private bool pendingFull;
        private bool actionBusy;
        private string selectExe;
        private GameInfo selectGame;

        private static readonly TextFormatFlags FlagsLine =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public MainForm()
        {
            Text = "ANR FG Manager";
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(1180, wa.Width - 40), Math.Min(780, wa.Height - 60));
            MinimumSize = new Size(920, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Font = Theme.Font;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(7);
            HandleCreated += delegate { Native.RoundCorners(Handle); };

            try
            {
                using (Stream icoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                {
                    if (icoStream != null)
                    {
                        byte[] icoBytes;
                        using (MemoryStream ms = new MemoryStream())
                        {
                            icoStream.CopyTo(ms);
                            icoBytes = ms.ToArray();
                        }
                        using (MemoryStream msIcon = new MemoryStream(icoBytes))
                        {
                            Icon = new Icon(msIcon);
                        }
                        using (MemoryStream msBmp = new MemoryStream(icoBytes))
                        using (Icon hi = new Icon(msBmp, 48, 48))
                        {
                            brandIconBmp = hi.ToBitmap();
                        }
                    }
                    else
                    {
                        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                        if (Icon != null) brandIconBmp = Icon.ToBitmap();
                    }
                }
            }
            catch
            {
                try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            }

            tip = Theme.CreateDarkTooltip();
            settings = AppSettings.Load();

            BuildUi();
            BuildTray();

            loadingPrefs = true;
            cmbSort.SelectedIndex = Math.Min(4, Math.Max(0, settings.Sort));
            chkCompat.Checked = settings.Compat;
            chkInstalled.Checked = settings.Installed;
            chkOther.Checked = settings.Other;
            loadingPrefs = false;

            ShowPage(0);
            UpdateStatus();

            Scanner.Load(AppSettings.GetCacheFile());
            PumpScanner();
            if (dgGames.Count > 0)
                lblScan.Text = dgGames.Count + " jogo(s) carregado(s) do cache. Verificando mudanças...";

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 250;
            timer.Tick += delegate { PumpScanner(); };

            FormClosing += delegate
            {
                Scanner.Cancel();
                timer.Stop();
                if (watchTimer != null) watchTimer.Stop();
                if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            };
            FormClosed += delegate
            {
                if (brandIconBmp != null) { brandIconBmp.Dispose(); brandIconBmp = null; }
                if (heroImg != null) { heroImg.Dispose(); heroImg = null; }
            };

            Shown += delegate
            {
                LayoutWindow();
                LayoutSide();
                LayoutHeader();
                LayoutPages();
                timer.Start();
                StartScan(false);
                StartUpdateCheck();
            };
        }

        private void BuildUi()
        {
            Panel pRoot = new Panel();
            pRoot.Dock = DockStyle.Fill;
            pRoot.BackColor = Theme.Bg;
            pRoot.Padding = new Padding(3);

            // ================= Sidebar (biblioteca) =================
            pSide = new Panel();
            pSide.Dock = DockStyle.Left;
            pSide.Width = 400;
            pSide.BackColor = Theme.Panel;
            pSide.Resize += delegate { Theme.SetRoundRegion(pSide, 18); LayoutSide(); };

            Panel pGap = new Panel();
            pGap.Dock = DockStyle.Left;
            pGap.Width = 10;
            pGap.BackColor = Theme.Bg;

            pBrand = new BufPanel();
            pBrand.Dock = DockStyle.Top;
            pBrand.Height = 76;
            pBrand.BackColor = Theme.Panel;
            pBrand.Paint += BrandPaint;

            Label lblTitle = new Label();
            lblTitle.Text = "ANR FG Manager";
            lblTitle.Font = new Font("Segoe UI", 12.5f, FontStyle.Bold);
            lblTitle.Location = new Point(70, 18);
            lblTitle.AutoSize = true;
            lblTitle.BackColor = Theme.Panel;

            lblSub = new Label();
            lblSub.Text = "Apex Neural Rendering  " + PackageStore.PackageVersion;
            lblSub.ForeColor = Theme.Muted;
            lblSub.Font = Theme.FontSm;
            lblSub.Location = new Point(72, 44);
            lblSub.AutoSize = true;
            lblSub.BackColor = Theme.Panel;

            lnkUpdate = new LinkLabel();
            lnkUpdate.Text = "v" + PackageStore.AppVersion + "  ·  atualizações";
            lnkUpdate.AutoSize = false;
            lnkUpdate.Size = new Size(190, 20);
            lnkUpdate.TextAlign = ContentAlignment.MiddleRight;
            lnkUpdate.Location = new Point(230, 30);
            lnkUpdate.BackColor = Theme.Panel;
            lnkUpdate.Font = Theme.FontSm;
            lnkUpdate.LinkColor = Theme.Accent;
            lnkUpdate.ActiveLinkColor = Theme.AccentHi;
            lnkUpdate.VisitedLinkColor = Theme.Accent;
            lnkUpdate.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkUpdate.LinkClicked += delegate { ShowUpdatesDialog(); };
            pBrand.Controls.AddRange(new Control[] { lblTitle, lblSub, lnkUpdate });
            WireDrag(pBrand);
            WireDrag(lblTitle);
            WireDrag(lblSub);

            pFilters = new Panel();
            pFilters.Dock = DockStyle.Top;
            pFilters.Height = 156;
            pFilters.BackColor = Theme.Panel;

            txtFilter = Theme.NewInput(16, 8, 368, false);
            txtFilter.SetCue("Buscar jogo...");
            txtFilter.TextChanged += delegate { RenderGameList(); };

            cmbStore = Theme.NewCombo(16, 54, 176);
            cmbStore.Items.Add("Todos os launchers");
            cmbStore.SelectedIndex = 0;
            cmbStore.SelectedIndexChanged += delegate { if (!rendering) RenderGameList(); };

            cmbSort = Theme.NewCombo(204, 54, 180);
            cmbSort.Items.AddRange(new object[] { "Nome (A–Z)", "Nome (Z–A)", "Launcher", "API gráfica", "Instalados primeiro" });
            cmbSort.SelectedIndex = 0;
            cmbSort.SelectedIndexChanged += delegate
            {
                if (!loadingPrefs) { RenderGameList(); SaveUiPrefs(); }
            };

            chkCompat = Theme.NewCheck("Só D3D12 / Vulkan", 14, 100);
            chkInstalled = Theme.NewCheck("Só instalados", 192, 100);
            chkOther = Theme.NewCheck("Incluir outros programas", 14, 126);
            chkCompat.BackColor = chkInstalled.BackColor = chkOther.BackColor = Theme.Panel;
            chkCompat.CheckedChanged += delegate { if (!rendering && !loadingPrefs) { RenderGameList(); SaveUiPrefs(); } };
            chkInstalled.CheckedChanged += delegate { if (!rendering && !loadingPrefs) { RenderGameList(); SaveUiPrefs(); } };
            chkOther.CheckedChanged += delegate
            {
                if (loadingPrefs) return;
                UpdateStoreCombo(); RenderGameList(); SaveUiPrefs(); StartScan(false);
            };

            tip.SetToolTip(chkOther, "Inclui programas do registro do Windows que não vêm de launchers de jogos");
            tip.SetToolTip(cmbStore, "Filtrar pela plataforma (launcher)");
            tip.SetToolTip(cmbSort, "Ordem da lista");
            pFilters.Controls.AddRange(new Control[] { txtFilter, cmbStore, cmbSort, chkCompat, chkInstalled, chkOther });

            pSideBottom = new Panel();
            pSideBottom.Dock = DockStyle.Bottom;
            pSideBottom.Height = 108;
            pSideBottom.BackColor = Theme.Panel;

            lblScan = new Label();
            lblScan.Location = new Point(16, 10);
            lblScan.Size = new Size(368, 38);
            lblScan.ForeColor = Theme.Muted;
            lblScan.Font = Theme.FontSm;

            btnAddGame = Theme.NewButton("Procurar .exe...", 16, 56, 140, 38, "ghost");
            btnRescan = Theme.NewButton("Atualizar", 164, 56, 92, 38, "ghost");
            btnSettings = Theme.NewButton("Configurações", 264, 56, 120, 38, "ghost");

            btnAddGame.Click += delegate
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "Escolha o executável (.exe) do jogo";
                    d.Filter = "Executável (*.exe)|*.exe";
                    if (d.ShowDialog(this) == DialogResult.OK) AddManualGame(d.FileName);
                }
            };
            btnRescan.Click += delegate { StartScan(true); };
            btnSettings.Click += delegate { ShowSettingsDialog(); };

            tip.SetToolTip(btnAddGame, "Adicionar um jogo manualmente escolhendo o .exe dele");
            tip.SetToolTip(btnRescan, "Busca completa em todos os discos (mais lenta). Ao abrir, só o que mudou é verificado.");
            tip.SetToolTip(btnSettings, "Pastas de busca, pastas excluídas, nomes ignorados, bandeja e jogos ocultos");
            pSideBottom.Controls.AddRange(new Control[] { lblScan, btnAddGame, btnRescan, btnSettings });

            lvGames = new DoubleBufferedListView();
            lvGames.View = View.Details;
            lvGames.FullRowSelect = true;
            lvGames.MultiSelect = false;
            lvGames.HideSelection = false;
            lvGames.HeaderStyle = ColumnHeaderStyle.None;
            lvGames.BorderStyle = BorderStyle.None;
            lvGames.OwnerDraw = true;
            lvGames.BackColor = Theme.Panel;
            lvGames.ForeColor = Theme.Text;
            lvGames.Dock = DockStyle.Fill;
            lvGames.Columns.Add("Jogo", 380);
            ImageList rowImages = new ImageList();
            rowImages.ImageSize = new Size(1, 60);
            lvGames.SmallImageList = rowImages;
            lvGames.SizeChanged += delegate
            {
                if (lvGames.Columns.Count > 0)
                    lvGames.Columns[0].Width = Math.Max(100, lvGames.ClientSize.Width);
            };
            lvGames.DrawItem += delegate { };
            lvGames.DrawSubItem += LvGames_DrawSubItem;
            lvGames.MouseMove += delegate(object s, MouseEventArgs e)
            {
                ListViewItem hit = lvGames.GetItemAt(e.X, e.Y);
                int idx = (hit != null) ? hit.Index : -1;
                if (idx != hoverIdx)
                {
                    int old = hoverIdx;
                    hoverIdx = idx;
                    InvalidateRow(old);
                    InvalidateRow(idx);
                }
            };
            lvGames.MouseLeave += delegate
            {
                int old = hoverIdx;
                hoverIdx = -1;
                InvalidateRow(old);
            };
            lvGames.SelectedIndexChanged += delegate
            {
                if (rendering || lvGames.SelectedItems.Count == 0) return;
                GameInfo g = lvGames.SelectedItems[0].Tag as GameInfo;
                if (g == null) return;
                txtGame.Text = g.ExeDir;
                recoDirty = true;
                if (tabReco.Visible) UpdateReco();
            };
            Theme.SetDarkScroll(lvGames);

            // Menu de contexto
            ContextMenuStrip ctx = new ContextMenuStrip();
            ctx.BackColor = Theme.Panel;
            ctx.ForeColor = Theme.Text;
            ctx.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            ToolStripItem miPlay = ctx.Items.Add("Jogar");
            ctx.Items.Add("-");
            ToolStripItem miExe = ctx.Items.Add("Abrir pasta do .exe");
            ToolStripItem miRoot = ctx.Items.Add("Abrir pasta do jogo (raiz)");
            ToolStripItem miCopy = ctx.Items.Add("Copiar caminho do .exe");
            ctx.Items.Add("-");
            ToolStripItem miHide = ctx.Items.Add("Ocultar (não é um jogo)");
            ToolStripItem miDel = ctx.Items.Add("Remover da lista (só jogos manuais)");
            miPlay.ForeColor = miExe.ForeColor = miRoot.ForeColor = miCopy.ForeColor = miHide.ForeColor = miDel.ForeColor = Theme.Text;
            ctx.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                if (lvGames.SelectedItems.Count == 0) { e.Cancel = true; return; }
                GameInfo sel = lvGames.SelectedItems[0].Tag as GameInfo;
                miDel.Enabled = (sel != null && sel.Manual);
            };
            miPlay.Click += delegate { PlayGame(GetCurrentGame()); };
            miExe.Click += delegate { GameInfo g = GetCurrentGame(); if (g != null && !string.IsNullOrEmpty(g.ExeDir)) Process.Start("explorer.exe", g.ExeDir); };
            miRoot.Click += delegate { GameInfo g = GetCurrentGame(); if (g != null && Directory.Exists(g.Dir)) Process.Start("explorer.exe", g.Dir); };
            miCopy.Click += delegate { GameInfo g = GetCurrentGame(); if (g != null && !string.IsNullOrEmpty(g.ExeDir)) Clipboard.SetText(g.ExeDir); };
            miHide.Click += delegate
            {
                GameInfo g = GetCurrentGame();
                if (g != null)
                {
                    File.AppendAllText(AppSettings.GetHiddenFile(), g.Key + Environment.NewLine);
                    RemoveGameFromList(g);
                    StartScan(false);
                }
            };
            miDel.Click += delegate
            {
                GameInfo g = GetCurrentGame();
                if (g != null && g.Manual && !string.IsNullOrEmpty(g.ExePath))
                {
                    string mf = AppSettings.GetManualFile();
                    if (File.Exists(mf))
                    {
                        List<string> keep = new List<string>();
                        string[] lines = File.ReadAllLines(mf);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(lines[i]) && !string.Equals(lines[i].Trim(), g.ExePath, StringComparison.OrdinalIgnoreCase))
                                keep.Add(lines[i]);
                        }
                        File.WriteAllLines(mf, keep.ToArray());
                    }
                    RemoveGameFromList(g);
                    StartScan(false);
                }
            };
            lvGames.ContextMenuStrip = ctx;

            pSide.Controls.Add(lvGames);
            pSide.Controls.Add(pSideBottom);
            pSide.Controls.Add(pFilters);
            pSide.Controls.Add(pBrand);

            // ================= Painel direito (detalhes) =================
            pMain = new Panel();
            pMain.Dock = DockStyle.Fill;
            pMain.BackColor = Theme.Panel;
            pMain.Resize += delegate { Theme.SetRoundRegion(pMain, 18); };

            pEmpty = new Panel();
            pEmpty.Dock = DockStyle.Fill;
            pEmpty.BackColor = Theme.Panel;
            Label lblEmpty = new Label();
            lblEmpty.Dock = DockStyle.Fill;
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.ForeColor = Theme.Muted;
            lblEmpty.Font = new Font("Segoe UI", 12f);
            lblEmpty.Text = "Selecione um jogo na lista\r\n\r\nou use  Procurar .exe...  para indicar um que não apareceu.";
            pEmpty.Controls.Add(lblEmpty);

            // Cabecalho (banner)
            pHeader = new BufPanel();
            pHeader.Height = 56;
            pHeader.Dock = DockStyle.Top;
            pHeader.BackColor = Theme.Panel;
            pHeader.Paint += HeaderPaint;
            pHeader.Resize += delegate { LayoutHeader(); pHeader.Invalidate(); };

            lblGameName = new Label();
            lblGameName.Font = Theme.FontTitle;
            lblGameName.AutoEllipsis = true;
            lblGameName.BackColor = Color.Transparent;

            lblMeta = new Label();
            lblMeta.ForeColor = Theme.Muted;
            lblMeta.AutoEllipsis = true;
            lblMeta.BackColor = Color.Transparent;

            lblStatus = new Label();
            lblStatus.Font = Theme.FontB;
            lblStatus.AutoEllipsis = true;
            lblStatus.BackColor = Color.Transparent;

            btnPlay = Theme.NewButton("▶   Jogar", 0, 0, 250, 46, "primary");
            btnPlay.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            Theme.SetTransparent(btnPlay);
            btnPlay.Click += delegate { PlayGame(GetCurrentGame()); };

            btnInstall = Theme.NewButton("Instalar / Atualizar", 0, 0, 140, 36, "ghost");
            Theme.SetTransparent(btnInstall);
            btnInstall.Click += delegate { DoAction("install"); };

            btnUninstall = Theme.NewButton("Desinstalar", 0, 0, 100, 36, "ghost");
            Theme.SetTransparent(btnUninstall);
            btnUninstall.Click += delegate { DoAction("uninstall"); };

            txtGame = Theme.NewInput(0, 0, 300, true);
            txtGame.AllowDrop = true;
            txtGame.TextChanged += delegate { UpdateStatus(); };
            txtGame.DragEnter += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            txtGame.DragDrop += delegate(object s, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    string p = files[0];
                    if (File.Exists(p)) p = Path.GetDirectoryName(p);
                    txtGame.Text = p;
                }
            };

            btnBrowse = Theme.NewButton("Procurar...", 0, 0, 100, 34, "ghost");
            Theme.SetTransparent(btnBrowse);
            btnBrowse.Click += delegate
            {
                string p = Theme.PickFolder(this, "Selecione a pasta onde fica o .exe do jogo");
                if (!string.IsNullOrEmpty(p)) txtGame.Text = p;
            };

            btnOpenGame = Theme.NewButton("Abrir pasta", 0, 0, 110, 34, "ghost");
            Theme.SetTransparent(btnOpenGame);
            btnOpenGame.Click += delegate
            {
                string g = GetGame();
                if (Directory.Exists(g)) Process.Start("explorer.exe", g);
            };

            tip.SetToolTip(btnPlay, "Abre o jogo (pela Steam ou Epic, quando for o caso)");
            tip.SetToolTip(btnInstall, "Instala o ANR FG neste jogo, ou atualiza se já estiver instalado");
            tip.SetToolTip(btnBrowse, "Escolher manualmente a pasta onde fica o .exe do jogo");
            tip.SetToolTip(btnOpenGame, "Abrir esta pasta no Explorer");

            btnMin = new CaptionButton("min");
            btnMax = new CaptionButton("max");
            btnClose = new CaptionButton("close");
            btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            btnMax.Click += delegate { ToggleMaximize(); btnMax.Invalidate(); };
            btnClose.Click += delegate { Close(); };

            headerGameCtls = new Control[] { lblGameName, lblMeta, lblStatus, btnPlay, btnInstall, btnUninstall, txtGame, btnBrowse, btnOpenGame };
            pHeader.Controls.AddRange(headerGameCtls);
            pHeader.Controls.Add(btnMin);
            pHeader.Controls.Add(btnMax);
            pHeader.Controls.Add(btnClose);
            btnMin.BringToFront();
            btnMax.BringToFront();
            btnClose.BringToFront();
            WireDrag(pHeader);
            WireDrag(lblGameName);
            WireDrag(lblMeta);
            WireDrag(lblStatus);

            // Abas (pilulas)
            pNav = new Panel();
            pNav.Dock = DockStyle.Top;
            pNav.Height = 52;
            pNav.BackColor = Theme.Panel;

            pHost = new Panel();
            pHost.Dock = DockStyle.Fill;
            pHost.BackColor = Theme.Panel;
            pHost.AutoScroll = true;
            pHost.AutoScrollMinSize = new Size(660, 440);
            Theme.SetDarkScroll(pHost);

            tabInstall = NewPage();
            tabFg = NewPage();
            tabOpti = NewPage();
            tabReco = NewPage();
            tabLogs = NewPage();
            pages = new Panel[] { tabInstall, tabFg, tabOpti, tabReco, tabLogs };
            pHost.Controls.AddRange(pages);

            BuildTabInstall();
            BuildTabFg();
            BuildTabOpti();
            BuildTabReco();
            BuildTabLogs();

            string[] navNames = new string[] { "Instalação", "ANR FG", "OptiScaler", "Recomendação", "Logs" };
            navLabels = new Label[navNames.Length];
            int nx = 18;
            for (int i = 0; i < navNames.Length; i++)
            {
                int idx = i;
                Label l = new Label();
                l.AccessibleName = navNames[i];
                l.Cursor = Cursors.Hand;
                l.BackColor = Theme.Panel;
                int tw = TextRenderer.MeasureText(navNames[i], Theme.FontB).Width;
                l.Size = new Size(tw + 34, 36);
                l.Location = new Point(nx, 8);
                nx += l.Width + 6;
                l.Tag = "";
                l.Paint += NavPaint;
                l.Click += delegate { ShowPage(idx); };
                l.MouseEnter += delegate(object s, EventArgs e)
                {
                    Label lb = (Label)s;
                    if ((lb.Tag as string) != "active") { lb.Tag = "hover"; lb.Invalidate(); }
                };
                l.MouseLeave += delegate(object s, EventArgs e)
                {
                    Label lb = (Label)s;
                    if ((lb.Tag as string) != "active") { lb.Tag = ""; lb.Invalidate(); }
                };
                navLabels[i] = l;
                pNav.Controls.Add(l);
            }

            pMain.Controls.Add(pEmpty);
            pMain.Controls.Add(pHost);
            pMain.Controls.Add(pNav);
            pMain.Controls.Add(pHeader);
            pRoot.Controls.Add(pMain);
            pRoot.Controls.Add(pGap);
            pRoot.Controls.Add(pSide);
            Controls.Add(pRoot);

            Resize += delegate { LayoutWindow(); btnMax.Invalidate(); };
        }

        // ---- janela propria: arrastar, ampliar e organizar o layout conforme o tamanho ----
        private void WireDrag(Control c)
        {
            c.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Native.DragWindow(Handle);
            };
            c.DoubleClick += delegate { ToggleMaximize(); };
        }

        private void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
        }

        private void LayoutWindow()
        {
            if (pSide == null) return;
            int w = ClientSize.Width - Padding.Horizontal - 6;
            if (w > 300) pSide.Width = Math.Max(340, Math.Min(470, (int)(w * 0.32)));
        }

        private void LayoutSide()
        {
            if (pSide == null || txtFilter == null || btnSettings == null) return;
            int W = pSide.ClientSize.Width;
            if (W < 200) return;
            int iw = W - 32;
            txtFilter.SetBounds(16, 8, iw, 34);
            int half = (iw - 12) / 2;
            cmbStore.SetBounds(16, 54, half, cmbStore.Height);
            cmbSort.SetBounds(16 + half + 12, 54, half, cmbSort.Height);
            lnkUpdate.Left = W - 16 - lnkUpdate.Width;
            lblScan.Width = iw;
            int bw = iw - 16;
            int w1 = (int)(bw * 0.40);
            int w2 = (int)(bw * 0.24);
            int w3 = bw - w1 - w2;
            btnAddGame.SetBounds(16, 56, w1, 38);
            btnRescan.SetBounds(16 + w1 + 8, 56, w2, 38);
            btnSettings.SetBounds(16 + w1 + w2 + 16, 56, w3, 38);
        }

        private void LayoutHeader()
        {
            if (pHeader == null || btnClose == null || btnPlay == null) return;
            int W = pHeader.ClientSize.Width;
            if (W < 300) return;
            int right = W - 24;
            int x = W - 8;
            CaptionButton[] caps = new CaptionButton[] { btnClose, btnMax, btnMin };
            for (int i = 0; i < caps.Length; i++) { x -= 42; caps[i].SetBounds(x, 8, 42, 30); }
            if (pHeader.Height < 120) return;
            int cw = 250;
            btnPlay.SetBounds(right - cw, 62, cw, 46);
            btnInstall.SetBounds(right - 140, 116, 140, 36);
            btnUninstall.SetBounds(right - 140 - 10 - 100, 116, 100, 36);
            int tw = Math.Max(120, right - cw - 20 - 100);
            lblGameName.SetBounds(104, 58, tw, 38);
            lblMeta.SetBounds(106, 98, tw, 20);
            lblStatus.SetBounds(106, 122, tw, 22);
            int by = 170;
            btnOpenGame.SetBounds(right - 110, by, 110, 34);
            btnBrowse.SetBounds(right - 110 - 8 - 100, by, 100, 34);
            txtGame.SetBounds(24, by, Math.Max(100, right - 110 - 8 - 100 - 8 - 24), 34);
        }

        private void SetDetailMode(bool has)
        {
            if (pHeader == null || headerGameCtls == null) return;
            pHeader.Height = has ? 218 : 56;
            for (int i = 0; i < headerGameCtls.Length; i++) headerGameCtls[i].Visible = has;
            pNav.Visible = has;
            pHost.Visible = has;
            pEmpty.Visible = !has;
            LayoutHeader();
            pHeader.Invalidate();
        }

        private void LayoutPages()
        {
            if (tabInstall == null || cardOut == null || gbFg == null || gbOpti == null || cardPc == null || cardLog == null || lblVc == null) return;
            int W = tabInstall.ClientSize.Width;
            int H = tabInstall.ClientSize.Height;
            if (W > 300 && H > 200)
            {
                cardOut.SetBounds(18, 140, W - 36, Math.Max(100, H - 140 - 44));
                lblVc.Location = new Point(W - 18 - lblVc.PreferredWidth, H - 30);
            }
            W = tabFg.ClientSize.Width;
            if (W > 300) gbFg.Width = W - 36;
            W = tabOpti.ClientSize.Width;
            if (W > 300) gbOpti.Width = W - 36;
            W = tabReco.ClientSize.Width;
            H = tabReco.ClientSize.Height;
            if (W > 300 && H > 200)
            {
                int half = (W - 36 - 10) / 2;
                cardPc.SetBounds(18, 14, half, 120);
                cardGame.SetBounds(18 + half + 10, 14, W - 36 - half - 10, 120);
                cardReco.SetBounds(18, 146, W - 36, Math.Max(150, H - 146 - 70));
                rSummary.Width = cardReco.Width - 40;
                rNotes.SetBounds(20, 78, cardReco.Width - 40, Math.Max(60, cardReco.Height - 90));
                btnRecoApply.Top = H - 56;
                btnRecoRefresh.Top = H - 56;
                lblRecoMsg.Top = H - 44;
            }
            W = tabLogs.ClientSize.Width;
            H = tabLogs.ClientSize.Height;
            if (W > 300 && H > 200)
            {
                cmbLogFile.Width = Math.Min(W - 36, 720);
                cardLog.SetBounds(18, 104, W - 36, Math.Max(80, H - 104 - 14));
            }
        }

        private void BrandPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle logoRect = new Rectangle(20, 18, 38, 38);
            using (GraphicsPath path = Theme.RoundPath(logoRect, 11))
            {
                if (brandIconBmp != null)
                {
                    GraphicsState st = g.Save();
                    g.SetClip(path);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(brandIconBmp, logoRect);
                    g.Restore(st);
                }
                else
                {
                    g.FillPath(Theme.BrAccent, path);
                    TextRenderer.DrawText(g, "A", Theme.FontName, logoRect,
                        Color.FromArgb(5, 26, 30), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void NavPaint(object sender, PaintEventArgs e)
        {
            Label s = (Label)sender;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            string state = (s.Tag as string) ?? "";
            if (state == "active" || state == "hover")
            {
                using (SolidBrush br = new SolidBrush(state == "active" ? Theme.Select : Color.FromArgb(28, 35, 47)))
                using (GraphicsPath p = Theme.RoundPath(new Rectangle(0, 0, s.Width - 1, s.Height - 1), 14))
                {
                    g.FillPath(br, p);
                }
            }
            Color col = (state == "active") ? Theme.Text : Theme.Muted;
            TextRenderer.DrawText(g, s.AccessibleName, Theme.FontB, new Rectangle(0, 0, s.Width, s.Height), col,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void InvalidateRow(int i)
        {
            if (i >= 0 && i < lvGames.Items.Count) lvGames.Invalidate(lvGames.Items[i].Bounds);
        }

        // banner do jogo (estilo Steam) + icone extraido do .exe
        private void HeaderPaint(object sender, PaintEventArgs e)
        {
            Panel s = (Panel)sender;
            Graphics g = e.Graphics;
            int W = s.Width;
            int H = s.Height;
            if (W < 10 || H < 10) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, W, H);
            using (LinearGradientBrush b = new LinearGradientBrush(rect, Color.FromArgb(22, 36, 48), Color.FromArgb(16, 44, 52), 20f))
            {
                g.FillRectangle(b, rect);
            }
            Bitmap img = heroImg;
            if (img != null && H > 100)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float scale = Math.Max((float)W / img.Width, (float)H / img.Height);
                float sw = W / scale;
                float sh = H / scale;
                float sx = (img.Width - sw) / 2f;
                g.DrawImage(img, rect, sx, 0f, sw, sh, GraphicsUnit.Pixel);
            }
            using (LinearGradientBrush o1 = new LinearGradientBrush(rect, Color.FromArgb(238, 22, 27, 36), Color.FromArgb(60, 22, 27, 36), 0f))
            {
                g.FillRectangle(o1, rect);
            }
            int y2 = (int)(H * 0.30);
            Rectangle r2 = new Rectangle(0, y2, W, H - y2 + 1);
            using (LinearGradientBrush o2 = new LinearGradientBrush(r2, Color.FromArgb(0, 22, 27, 36), Color.FromArgb(255, 22, 27, 36), 90f))
            {
                g.FillRectangle(o2, r2);
            }
            if (!string.IsNullOrEmpty(headerIconExe) && H > 150)
            {
                Bitmap ico = GetExeIcon(headerIconExe);
                Rectangle pr = new Rectangle(26, 60, 62, 62);
                using (GraphicsPath pp = Theme.RoundPath(pr, 16))
                using (SolidBrush plate = new SolidBrush(Color.FromArgb(225, 30, 36, 48)))
                {
                    g.FillPath(plate, pp);
                    g.DrawPath(Theme.PenBorder, pp);
                }
                if (ico != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(ico, new Rectangle(37, 71, 40, 40));
                }
            }
        }

        private Panel NewPage()
        {
            Panel p = new Panel();
            p.Size = new Size(800, 500);
            p.Dock = DockStyle.Fill;
            p.Visible = false;
            p.BackColor = Theme.Panel;
            p.Resize += delegate { LayoutPages(); };
            return p;
        }

        private void BuildTabInstall()
        {
            Label lblProxy = Theme.NewLabel("Nome com que o OptiScaler será carregado", 18, 16);
            cmbProxy = Theme.NewCombo(18, 40, 380);
            cmbProxy.Items.AddRange(new object[] {
                "Automático (dxgi.dll, ou winmm.dll em jogos Vulkan)",
                "dxgi.dll", "winmm.dll", "version.dll"
            });
            cmbProxy.SelectedIndex = 0;
            Label lblProxyHint = Theme.NewHint("Mantém o nome já usado, se houver instalação.", 412, 46);
            btnResetAll = Theme.NewButton("Redefinir configurações", 18, 88, 210, 38, "ghost");
            tip.SetToolTip(btnResetAll, "Volta o nr_fg.ini e o OptiScaler.ini deste jogo ao padrão do pacote");
            btnResetAll.Click += delegate
            {
                if (!Theme.ShowMsg(this,
                    "Redefinir as configurações deste jogo?\n\nO nr_fg.ini e o OptiScaler.ini voltam ao padrão do pacote. Suas alterações serão perdidas.",
                    "Confirmar", true)) return;
                try
                {
                    ResetFgIni();
                    ResetOptiIni();
                    txtOut.Clear();
                    AppendOut("[OK] nr_fg.ini e OptiScaler.ini redefinidos para o padrão do pacote.");
                    LoadFgIni();
                    LoadOptiIni();
                    UpdateStatus();
                }
                catch (Exception ex) { AppendOut("[ERRO] " + ex.Message); }
            };

            cardOut = Theme.NewCard("Saída da instalação", 18, 140, 700, 300);
            cardOut.Padding = new Padding(12, 34, 12, 12);
            txtOut = new TextBox();
            txtOut.Multiline = true;
            txtOut.ReadOnly = true;
            txtOut.ScrollBars = ScrollBars.Vertical;
            txtOut.BorderStyle = BorderStyle.None;
            txtOut.Dock = DockStyle.Fill;
            txtOut.Font = Theme.FontMono;
            txtOut.BackColor = Theme.ConsoleBg;
            txtOut.ForeColor = Theme.ConsoleFg;
            Theme.SetDarkScroll(txtOut);
            cardOut.Controls.Add(txtOut);

            lblVc = new LinkLabel();
            lblVc.Text = "Requer o Visual C++ 2015–2022 (x64) · baixar";
            lblVc.AutoSize = true;
            lblVc.Font = Theme.FontSm;
            lblVc.LinkColor = Theme.Accent;
            lblVc.ActiveLinkColor = Theme.AccentHi;
            lblVc.VisitedLinkColor = Theme.Accent;
            lblVc.LinkBehavior = LinkBehavior.HoverUnderline;
            lblVc.LinkClicked += delegate { Process.Start("https://aka.ms/vs/17/release/vc_redist.x64.exe"); };

            tabInstall.Controls.AddRange(new Control[] { lblProxy, cmbProxy, lblProxyHint, btnResetAll, cardOut, lblVc });
        }

        private void BuildTabFg()
        {
            gbFg = Theme.NewCard("nr_fg.ini  ·  reinicie o jogo após salvar", 18, 14, 700, 300);

            Label lPath = Theme.NewLabel("Caminho de convolução (path)", 20, 50);
            cmbPath = Theme.NewCombo(262, 46, 180);
            cmbPath.Items.AddRange(new object[] { "auto", "coopmat", "packed", "dp4a", "portable", "portable-linalg", "portable-xmx" });
            Label lPathHint = Theme.NewHint("auto = ordem medida para a sua GPU", 456, 50);

            Label lGen = Theme.NewLabel("Multiplicador de quadros", 20, 104);
            genBtns = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                int val = i + 1;
                Button gb = Theme.NewButton((i + 2) + "X", 262 + 62 * i, 98, 56, 34, "ghost");
                gb.Click += delegate { SetGen(val); };
                genBtns[i] = gb;
            }
            lGenVal = Theme.NewHint("", 262, 138);
            lGenVal.ForeColor = Theme.Accent;

            Label lNits = Theme.NewLabel("Nits por unidade (HDR10 / PQ)", 20, 176);
            txtNits = Theme.NewInput(262, 170, 100, false);
            txtNits.MaxLength = 5;
            txtNits.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            Label lNitsHint = Theme.NewHint("luminância do 1.0 linear (padrão 80)", 376, 176);

            chkScene = Theme.NewCheck("Não interpolar em cortes de cena (scene_cut)", 18, 214);
            Label lModel = Theme.NewHint("Modelo", 20, 254);
            lModelVal = Theme.NewLabel("", 262, 254);
            Label lBuild = Theme.NewHint("Build", 20, 278);
            lBuildVal = Theme.NewLabel("", 262, 278);

            gbFg.Controls.AddRange(new Control[] { lPath, cmbPath, lPathHint, lGen });
            gbFg.Controls.AddRange(genBtns);
            gbFg.Controls.AddRange(new Control[] { lGenVal, lNits, txtNits, lNitsHint, chkScene, lModel, lModelVal, lBuild, lBuildVal });

            btnFgSave = Theme.NewButton("Salvar", 18, 330, 120, 40, "primary");
            btnFgReload = Theme.NewButton("Recarregar", 146, 330, 120, 40, "ghost");
            btnFgDefault = Theme.NewButton("Redefinir ao padrão", 274, 330, 180, 40, "ghost");
            tip.SetToolTip(btnFgDefault, "Restaura o nr_fg.ini original do pacote neste jogo");
            lblFgMsg = Theme.NewLabel("", 470, 340);

            btnFgSave.Click += delegate
            {
                try
                {
                    string ini = Path.Combine(AnrInstaller.GetDlssgDir(GetGame()), "nr_fg.ini");
                    int nits = 80;
                    int.TryParse(txtNits.Text, out nits);
                    nits = Math.Min(10000, Math.Max(1, nits));
                    IniHelper.SaveChanges(ini, new IniChange[] {
                        new IniChange("", "path", Convert.ToString(cmbPath.SelectedItem)),
                        new IniChange("", "max_generated", genValue.ToString(CultureInfo.InvariantCulture)),
                        new IniChange("", "nits_per_unit", nits.ToString(CultureInfo.InvariantCulture)),
                        new IniChange("", "scene_cut", chkScene.Checked ? "1" : "0")
                    });
                    lblFgMsg.ForeColor = Theme.Ok;
                    lblFgMsg.Text = "Salvo. Reinicie o jogo.";
                }
                catch (Exception ex) { lblFgMsg.ForeColor = Theme.Err; lblFgMsg.Text = ex.Message; }
            };
            btnFgReload.Click += delegate { LoadFgIni(); };
            btnFgDefault.Click += delegate
            {
                if (!Theme.ShowMsg(this, "Restaurar o nr_fg.ini original do pacote neste jogo?", "Confirmar", true)) return;
                try
                {
                    ResetFgIni();
                    LoadFgIni();
                    lblFgMsg.ForeColor = Theme.Ok;
                    lblFgMsg.Text = "Configurações do ANR FG redefinidas. Reinicie o jogo.";
                }
                catch (Exception ex) { lblFgMsg.ForeColor = Theme.Err; lblFgMsg.Text = ex.Message; }
            };

            tabFg.Controls.AddRange(new Control[] { gbFg, btnFgSave, btnFgReload, btnFgDefault, lblFgMsg });
            SetGen(5);
        }

        private void BuildTabOpti()
        {
            gbOpti = Theme.NewCard("OptiScaler.ini  ·  os demais ajustes ficam no menu do OptiScaler, dentro do jogo", 18, 14, 700, 352);

            Label lFgEn = Theme.NewLabel("Frame Generation (Enabled)", 20, 50);
            cmbFgEn = Theme.NewCombo(262, 46, 140);
            cmbFgEn.Items.AddRange(new object[] { "auto", "true", "false" });
            Label lFgEnHint = Theme.NewHint("auto segue a opção DLSS FG do jogo", 416, 50);

            Label lFgIn = Theme.NewLabel("Entrada do FG (FGInput)", 20, 90);
            cmbFgIn = Theme.NewCombo(262, 86, 170);
            cmbFgIn.Items.AddRange(new object[] { "auto", "nofg", "dlssg", "nvngxfg", "fsrfg", "upscaler", "fsrfg30", "xefg" });

            Label lFgOut = Theme.NewLabel("Saída do FG (FGOutput)", 20, 130);
            cmbFgOut = Theme.NewCombo(262, 126, 170);
            cmbFgOut.Items.AddRange(new object[] { "auto", "nofg", "fsrfg", "xefg", "dlssg" });

            Label lUp = Theme.NewLabel("Upscaler DX12 (Dx12Upscaler)", 20, 170);
            cmbUp = Theme.NewCombo(262, 166, 170);
            cmbUp.Items.AddRange(new object[] { "auto", "dlss", "ffx", "xess", "fsr21", "fsr22" });
            Label lUpHint = Theme.NewHint("ou uma pasta em 'custom'", 446, 170);

            chkLog = Theme.NewCheck("Gravar OptiScaler.log (LogToFile)", 18, 210);

            Label lLvl = Theme.NewLabel("Nível do log (LogLevel)", 20, 252);
            cmbLvl = Theme.NewCombo(262, 248, 170);
            cmbLvl.Items.AddRange(new object[] { "auto", "0", "1", "2", "3", "4", "5" });
            Label lLvlHint = Theme.NewHint("0 Trace, 1 Debug, 2 Info, 3 Aviso, 4 Erro", 446, 252);

            Label lKey = Theme.NewLabel("Tecla do menu (ShortcutKey)", 20, 296);
            keyBox = new KeyBox();
            keyBox.Location = new Point(262, 290);
            keyBox.Size = new Size(190, 34);
            Button bKeyDef = Theme.NewButton("Padrão", 462, 290, 74, 34, "ghost");
            Button bKeyOff = Theme.NewButton("Nenhuma", 542, 290, 90, 34, "ghost");
            bKeyDef.Click += delegate { keyBox.SetValue(-2); };
            bKeyOff.Click += delegate { keyBox.SetValue(-1); };
            tip.SetToolTip(keyBox, "Clique no campo e pressione a tecla que abre o menu do OptiScaler no jogo");
            tip.SetToolTip(bKeyDef, "Volta para a tecla padrão (Insert)");
            tip.SetToolTip(bKeyOff, "Desativa o atalho do menu");
            Label lKeyHint = Theme.NewHint("Uma tecla só: o .ini do OptiScaler não aceita combinações como Ctrl+Insert.", 20, 324);

            gbOpti.Controls.AddRange(new Control[] {
                lFgEn, cmbFgEn, lFgEnHint, lFgIn, cmbFgIn, lFgOut, cmbFgOut,
                lUp, cmbUp, lUpHint, chkLog, lLvl, cmbLvl, lLvlHint, lKey, keyBox, bKeyDef, bKeyOff, lKeyHint
            });

            btnOptiSave = Theme.NewButton("Salvar", 18, 382, 120, 40, "primary");
            btnOptiReload = Theme.NewButton("Recarregar", 146, 382, 120, 40, "ghost");
            btnOptiReset = Theme.NewButton("Redefinir ao padrão", 274, 382, 180, 40, "ghost");
            btnOptiRestore = Theme.NewButton("Restaurar cópia", 462, 382, 150, 40, "ghost");
            tip.SetToolTip(btnOptiReset, "Restaura o OptiScaler.ini original do pacote neste jogo");
            tip.SetToolTip(btnOptiRestore, "Volta ao OptiScaler.ini como estava antes da primeira edição por este programa");
            lblOptiMsg = Theme.NewLabel("", 18, 432);

            btnOptiSave.Click += delegate
            {
                try
                {
                    string ini = Path.Combine(GetGame(), "OptiScaler.ini");
                    string up = (cmbUp.Text ?? "").Trim(); if (up.Length == 0) up = "auto";
                    string lv = (cmbLvl.Text ?? "").Trim(); if (lv.Length == 0) lv = "auto";
                    string key = keyBox.ToIniValue();
                    IniHelper.SaveChanges(ini, new IniChange[] {
                        new IniChange("FrameGen", "Enabled", Convert.ToString(cmbFgEn.SelectedItem)),
                        new IniChange("FrameGen", "FGInput", Convert.ToString(cmbFgIn.SelectedItem)),
                        new IniChange("FrameGen", "FGOutput", Convert.ToString(cmbFgOut.SelectedItem)),
                        new IniChange("Upscalers", "Dx12Upscaler", up),
                        new IniChange("Log", "LogToFile", chkLog.Checked ? "true" : "false"),
                        new IniChange("Log", "LogLevel", lv),
                        new IniChange("Menu", "ShortcutKey", key)
                    });
                    lblOptiMsg.ForeColor = Theme.Ok;
                    lblOptiMsg.Text = "Salvo (cópia original em OptiScaler.ini.anrbak).";
                    btnOptiRestore.Enabled = true;
                }
                catch (Exception ex) { lblOptiMsg.ForeColor = Theme.Err; lblOptiMsg.Text = ex.Message; }
            };
            btnOptiReload.Click += delegate { LoadOptiIni(); };
            btnOptiReset.Click += delegate
            {
                if (!Theme.ShowMsg(this,
                    "Restaurar o OptiScaler.ini original do pacote neste jogo?\n\nAjustes feitos pelo menu do OptiScaler também serão perdidos.",
                    "Confirmar", true)) return;
                try
                {
                    ResetOptiIni();
                    LoadOptiIni();
                    lblOptiMsg.ForeColor = Theme.Ok;
                    lblOptiMsg.Text = "OptiScaler.ini redefinido ao padrão do pacote.";
                }
                catch (Exception ex) { lblOptiMsg.ForeColor = Theme.Err; lblOptiMsg.Text = ex.Message; }
            };
            btnOptiRestore.Click += delegate
            {
                string ini = Path.Combine(GetGame(), "OptiScaler.ini");
                string bak = ini + ".anrbak";
                if (File.Exists(bak))
                {
                    File.Copy(bak, ini, true);
                    LoadOptiIni();
                    lblOptiMsg.ForeColor = Theme.Ok;
                    lblOptiMsg.Text = "Restaurado da cópia.";
                }
            };

            tabOpti.Controls.AddRange(new Control[] { gbOpti, btnOptiSave, btnOptiReload, btnOptiReset, btnOptiRestore, lblOptiMsg });
        }

        private void BuildTabReco()
        {
            cardPc = Theme.NewCard("Seu PC", 18, 14, 340, 120);
            Label lKGpu = Theme.NewHint("GPU", 20, 42);
            rGpu = Theme.NewLabel("-", 112, 42); rGpu.AutoSize = false; rGpu.Size = new Size(214, 20); rGpu.AutoEllipsis = true;
            Label lKArch = Theme.NewHint("Arquitetura", 20, 68);
            rArch = Theme.NewLabel("-", 112, 68);
            Label lKRes = Theme.NewHint("Tela", 20, 94);
            rRes = Theme.NewLabel("-", 112, 94);
            cardPc.Controls.AddRange(new Control[] { lKGpu, rGpu, lKArch, rArch, lKRes, rRes });

            cardGame = Theme.NewCard("Jogo selecionado", 368, 14, 350, 120);
            Label lKGame = Theme.NewHint("Jogo", 20, 42);
            rGame = Theme.NewLabel("-", 134, 42); rGame.AutoSize = false; rGame.Size = new Size(204, 20); rGame.AutoEllipsis = true;
            Label lKApi = Theme.NewHint("API gráfica", 20, 68);
            rApi = Theme.NewLabel("-", 134, 68);
            Label lKProxy = Theme.NewHint("DLL do OptiScaler", 20, 94);
            rProxy = Theme.NewLabel("-", 134, 94);
            cardGame.Controls.AddRange(new Control[] { lKGame, rGame, lKApi, rApi, lKProxy, rProxy });

            cardReco = Theme.NewCard("Recomendação", 18, 146, 700, 250);
            rSummary = new Label();
            rSummary.Location = new Point(20, 42);
            rSummary.Size = new Size(660, 26);
            rSummary.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            rSummary.ForeColor = Theme.Accent;

            rNotes = new TextBox();
            rNotes.Multiline = true;
            rNotes.ReadOnly = true;
            rNotes.ScrollBars = ScrollBars.Vertical;
            rNotes.BorderStyle = BorderStyle.None;
            rNotes.Location = new Point(20, 78);
            rNotes.Size = new Size(660, 160);
            rNotes.BackColor = Theme.Card;
            rNotes.ForeColor = Theme.Text;
            Theme.SetDarkScroll(rNotes);
            cardReco.Controls.AddRange(new Control[] { rSummary, rNotes });

            btnRecoApply = Theme.NewButton("Aplicar recomendação", 18, 410, 210, 42, "primary");
            btnRecoRefresh = Theme.NewButton("Reanalisar", 236, 410, 120, 42, "ghost");
            lblRecoMsg = Theme.NewLabel("", 370, 422);

            btnRecoRefresh.Click += delegate { UpdateReco(); };
            btnRecoApply.Click += delegate
            {
                if (currentReco == null) return;
                try
                {
                    string g = GetGame();
                    IniHelper.SaveChanges(Path.Combine(AnrInstaller.GetDlssgDir(g), "nr_fg.ini"), new IniChange[] {
                        new IniChange("", "path", currentReco.Path),
                        new IniChange("", "max_generated", currentReco.MaxGen.ToString(CultureInfo.InvariantCulture)),
                        new IniChange("", "scene_cut", "1")
                    });
                    IniHelper.SaveChanges(Path.Combine(g, "OptiScaler.ini"), new IniChange[] {
                        new IniChange("Log", "LogToFile", "true")
                    });
                    LoadFgIni();
                    LoadOptiIni();
                    lblRecoMsg.ForeColor = Theme.Ok;
                    lblRecoMsg.Text = "Aplicado. Reinicie o jogo.";
                }
                catch (Exception ex) { lblRecoMsg.ForeColor = Theme.Err; lblRecoMsg.Text = ex.Message; }
            };

            tabReco.Controls.AddRange(new Control[] { cardPc, cardGame, cardReco, btnRecoApply, btnRecoRefresh, lblRecoMsg });
        }

        private void BuildTabLogs()
        {
            Button btnLogFolder = Theme.NewButton("Abrir pasta dos logs", 18, 14, 170, 38, "ghost");
            Button btnLogRefresh = Theme.NewButton("Atualizar", 196, 14, 100, 38, "ghost");
            Button btnLogCopy = Theme.NewButton("Copiar", 304, 14, 90, 38, "ghost");
            Button btnLogZip = Theme.NewButton("Reunir logs em .zip", 402, 14, 170, 38, "primary");
            tip.SetToolTip(btnLogZip, "Cria um .zip na Área de Trabalho com o nr_fg-*.log mais recente e o OptiScaler.log");

            cmbLogFile = Theme.NewCombo(18, 62, 600);
            cmbLogFile.SelectedIndexChanged += delegate
            {
                string f = cmbLogFile.SelectedItem as string;
                if (!string.IsNullOrEmpty(f) && File.Exists(f))
                {
                    try
                    {
                        using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            string all = sr.ReadToEnd();
                            if (all.Length > 200000)
                                all = "... (mostrando o final)\r\n" + all.Substring(all.Length - 200000);
                            txtLog.Text = all;
                            txtLog.SelectionStart = txtLog.Text.Length;
                            txtLog.ScrollToCaret();
                        }
                    }
                    catch (Exception ex) { txtLog.Text = "Não foi possível ler: " + ex.Message; }
                }
            };

            cardLog = Theme.NewCard("", 18, 104, 700, 300);
            cardLog.Padding = new Padding(10);
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Both;
            txtLog.WordWrap = false;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = Theme.FontMonoSm;
            txtLog.BackColor = Theme.ConsoleBg;
            txtLog.ForeColor = Theme.ConsoleFg;
            Theme.SetDarkScroll(txtLog);
            cardLog.Controls.Add(txtLog);

            btnLogRefresh.Click += delegate { RefreshLogs(); };
            btnLogCopy.Click += delegate { if (!string.IsNullOrEmpty(txtLog.Text)) Clipboard.SetText(txtLog.Text); };
            btnLogFolder.Click += delegate
            {
                string g = GetGame();
                string d = AnrInstaller.GetDlssgDir(g);
                if (Directory.Exists(d)) Process.Start("explorer.exe", d);
                else if (Directory.Exists(g)) Process.Start("explorer.exe", g);
            };
            btnLogZip.Click += delegate
            {
                string g = GetGame();
                List<string> files = new List<string>();
                string d = AnrInstaller.GetDlssgDir(g);
                if (Directory.Exists(d))
                {
                    FileInfo[] nrs = new DirectoryInfo(d).GetFiles("nr_fg-*.log");
                    Array.Sort(nrs, delegate(FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                    if (nrs.Length > 0) files.Add(nrs[0].FullName);
                }
                string ol = Path.Combine(g, "OptiScaler.log");
                if (File.Exists(ol)) files.Add(ol);
                if (files.Count == 0)
                {
                    Theme.ShowMsg(this, "Não há logs para reunir.", "ANR FG Manager", false);
                    return;
                }
                string zipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "anr-fg-logs-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
                try
                {
                    using (FileStream fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (ZipArchive za = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        for (int i = 0; i < files.Count; i++)
                        {
                            ZipArchiveEntry entry = za.CreateEntry(Path.GetFileName(files[i]), CompressionLevel.Optimal);
                            using (FileStream src = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (Stream dst = entry.Open())
                                src.CopyTo(dst);
                        }
                    }
                    Process.Start("explorer.exe", "/select,\"" + zipPath + "\"");
                }
                catch (Exception ex) { Theme.ShowMsg(this, "Erro ao criar .zip: " + ex.Message, "ANR FG Manager", false); }
            };

            tabLogs.Controls.AddRange(new Control[] { btnLogFolder, btnLogRefresh, btnLogCopy, btnLogZip, cmbLogFile, cardLog });
        }

        private void ShowPage(int idx)
        {
            for (int k = 0; k < pages.Length; k++)
            {
                pages[k].Visible = (k == idx);
                navLabels[k].Tag = (k == idx) ? "active" : "";
                navLabels[k].Invalidate();
            }
            LayoutPages();
            if (idx == 3 && recoDirty) UpdateReco();
        }

        private void SetGen(int n)
        {
            n = Math.Min(5, Math.Max(1, n));
            genValue = n;
            for (int i = 0; i < genBtns.Length; i++)
            {
                genBtns[i].Tag = ((i + 1) == n) ? "primary" : "ghost";
                Theme.UpdateBtnColors(genBtns[i]);
            }
            lGenVal.Text = n + " gerado(s) por quadro real  =  até " + (n + 1) + "X";
        }

        private void LvGames_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (e.ColumnIndex != 0) return;
            GameInfo g = e.Item.Tag as GameInfo;
            Rectangle b = e.Item.Bounds;
            Graphics gr = e.Graphics;
            bool sel = e.Item.Selected;
            bool hov = (e.Item.Index == hoverIdx);

            gr.FillRectangle(Theme.BrRowA, b);
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle card = new Rectangle(b.X + 10, b.Y + 3, b.Width - 20, b.Height - 6);
            if (sel || hov)
            {
                using (GraphicsPath cp = Theme.RoundPath(card, 12))
                {
                    gr.FillPath(sel ? Theme.BrSel : Theme.BrHover, cp);
                }
            }
            if (g == null) return;

            // icone do jogo (extraido do .exe)
            int ix = card.X + 10;
            int iy = card.Y + (card.Height - 40) / 2;
            using (GraphicsPath plate = Theme.RoundPath(new Rectangle(ix, iy, 40, 40), 11))
            {
                gr.FillPath(Theme.BrIconPlate, plate);
            }
            Bitmap ico = GetExeIcon(g.ExePath);
            if (ico != null)
            {
                gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gr.DrawImage(ico, new Rectangle(ix + 6, iy + 6, 28, 28));
            }
            int textX = ix + 40 + 12;

            string pillText = "";
            Color pillCol = Theme.Ok;
            SolidBrush pillBr = Theme.BrPillOk;
            if (g.Status == "Instalado") { pillText = "INSTALADO"; }
            else if (g.Status == "Outro OptiScaler") { pillText = "OPTISCALER"; pillCol = Theme.Warn; pillBr = Theme.BrPillWarn; }

            int textRight = card.Right - 12;
            if (pillText.Length > 0)
            {
                int tw = TextRenderer.MeasureText(gr, pillText, Theme.FontS, new Size(500, 20), TextFormatFlags.NoPadding).Width;
                int pw = tw + 20;
                Rectangle pr = new Rectangle(card.Right - 12 - pw, card.Y + (card.Height - 22) / 2, pw, 22);
                using (GraphicsPath pp = Theme.RoundPath(pr, 11))
                {
                    gr.FillPath(pillBr, pp);
                    using (Pen pen = new Pen(pillCol, 1f)) gr.DrawPath(pen, pp);
                }
                TextRenderer.DrawText(gr, pillText, Theme.FontS, pr, pillCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                textRight = pr.X - 10;
            }

            Rectangle nameRect = new Rectangle(textX, card.Y + 8, Math.Max(40, textRight - textX), 22);
            TextRenderer.DrawText(gr, g.Name, Theme.FontName, nameRect, Theme.Text, FlagsLine);

            Color storeCol = Theme.GetStoreColor(g.Store);
            string storeText = (g.Store ?? "").ToUpperInvariant();
            int sw = TextRenderer.MeasureText(gr, storeText, Theme.FontS, new Size(500, 20), TextFormatFlags.NoPadding).Width;
            Rectangle subRect = new Rectangle(textX, card.Y + 33, sw + 4, 16);
            TextRenderer.DrawText(gr, storeText, Theme.FontS, subRect, storeCol, FlagsLine);

            List<string> rest = new List<string>();
            if (!string.IsNullOrEmpty(g.Api) && g.Api != "?") rest.Add(g.Api);
            if (!string.IsNullOrEmpty(g.Engine)) rest.Add(g.Engine);
            if (rest.Count > 0)
            {
                int rx = textX + sw + 10;
                Rectangle restRect = new Rectangle(rx, card.Y + 33, Math.Max(20, textRight - rx), 16);
                Color apiCol = (g.Api.IndexOf("D3D12", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                g.Api.IndexOf("Vulkan", StringComparison.OrdinalIgnoreCase) >= 0) ? Theme.Accent : Theme.Muted;
                TextRenderer.DrawText(gr, string.Join("  ·  ", rest.ToArray()), Theme.FontSm, restRect, apiCol, FlagsLine);
            }
        }

        private void UpdateGameStatus(GameInfo g)
        {
            if (string.IsNullOrEmpty(g.ExeDir)) { g.Status = "sem .exe"; return; }
            string opti = Path.Combine(g.ExeDir, "OptiScaler");
            if (File.Exists(Path.Combine(opti, "nr_fg_install.json"))) g.Status = "Instalado";
            else if (Directory.Exists(opti)) g.Status = "Outro OptiScaler";
            else g.Status = "Disponível";
        }

        private bool IsVisibleGame(GameInfo g)
        {
            if (g.Store == "Programa" && !chkOther.Checked) return false;
            return true;
        }

        private string GetStoreKey()
        {
            string t = cmbStore.SelectedItem as string;
            if (string.IsNullOrEmpty(t) || t.StartsWith("Todos")) return "";
            return Regex.Replace(t, @"\s*\(\d+\)$", "");
        }

        private void UpdateStoreCombo()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dgGames.Count; i++)
            {
                GameInfo g = dgGames[i];
                if (!IsVisibleGame(g)) continue;
                int c;
                counts.TryGetValue(g.Store, out c);
                counts[g.Store] = c + 1;
            }
            List<string> keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            List<string> want = new List<string>();
            want.Add("Todos os launchers");
            for (int i = 0; i < keys.Count; i++) want.Add(keys[i] + " (" + counts[keys[i]] + ")");

            if (want.Count == cmbStore.Items.Count)
            {
                bool same = true;
                for (int i = 0; i < want.Count; i++)
                {
                    if (!string.Equals(want[i], Convert.ToString(cmbStore.Items[i]), StringComparison.Ordinal)) { same = false; break; }
                }
                if (same) return;
            }

            string prev = GetStoreKey();
            rendering = true;
            try
            {
                cmbStore.Items.Clear();
                for (int i = 0; i < want.Count; i++) cmbStore.Items.Add(want[i]);
                cmbStore.SelectedIndex = 0;
                if (!string.IsNullOrEmpty(prev))
                {
                    for (int i = 1; i < cmbStore.Items.Count; i++)
                    {
                        if (Convert.ToString(cmbStore.Items[i]).StartsWith(prev + " (", StringComparison.OrdinalIgnoreCase))
                        {
                            cmbStore.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            finally { rendering = false; }
        }

        private int ApiRank(GameInfo g)
        {
            if (g.Api.IndexOf("D3D12", StringComparison.OrdinalIgnoreCase) >= 0 ||
                g.Api.IndexOf("Vulkan", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
            if (g.Api == "?") return 2;
            return 1;
        }

        private void RenderGameList()
        {
            rendering = true;
            try
            {
                GameInfo sel = (lvGames.SelectedItems.Count > 0) ? (lvGames.SelectedItems[0].Tag as GameInfo) : null;
                string q = (txtFilter.Text ?? "").Trim().ToLowerInvariant();
                string store = GetStoreKey();
                List<GameInfo> shown = new List<GameInfo>();

                for (int i = 0; i < dgGames.Count; i++)
                {
                    GameInfo g = dgGames[i];
                    if (!IsVisibleGame(g)) continue;
                    if (!string.IsNullOrEmpty(store) && !string.Equals(g.Store, store, StringComparison.OrdinalIgnoreCase)) continue;
                    if (q.Length > 0 &&
                        g.Name.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 &&
                        g.Dir.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 &&
                        g.Store.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0)
                        continue;
                    if (chkCompat.Checked &&
                        g.Api.IndexOf("D3D12", StringComparison.OrdinalIgnoreCase) < 0 &&
                        g.Api.IndexOf("Vulkan", StringComparison.OrdinalIgnoreCase) < 0 &&
                        g.Api != "?")
                        continue;
                    if (chkInstalled.Checked && g.Status != "Instalado") continue;
                    shown.Add(g);
                }

                int sortIdx = cmbSort.SelectedIndex;
                shown.Sort(delegate(GameInfo a, GameInfo b)
                {
                    int cmp;
                    switch (sortIdx)
                    {
                        case 1: return string.Compare(b.Name, a.Name, StringComparison.CurrentCultureIgnoreCase);
                        case 2:
                            cmp = string.Compare(a.Store, b.Store, StringComparison.CurrentCultureIgnoreCase);
                            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                        case 3:
                            cmp = ApiRank(a).CompareTo(ApiRank(b));
                            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                        case 4:
                            int ia = (a.Status == "Instalado") ? 0 : 1;
                            int ib = (b.Status == "Instalado") ? 0 : 1;
                            cmp = ia.CompareTo(ib);
                            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                        default:
                            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                    }
                });

                lvGames.BeginUpdate();
                lvGames.Items.Clear();
                for (int i = 0; i < shown.Count; i++)
                {
                    GameInfo g = shown[i];
                    ListViewItem it = new ListViewItem(g.Name);
                    it.Tag = g;
                    lvGames.Items.Add(it);
                    if (sel != null && ReferenceEquals(sel, g)) it.Selected = true;
                }
                lvGames.EndUpdate();
            }
            finally { rendering = false; }
            dirtyList = false;
            lastRender = DateTime.Now;
        }

        private void SelectGame(GameInfo g)
        {
            if (g == null) return;
            rendering = true;
            txtFilter.Text = "";
            chkCompat.Checked = false;
            chkInstalled.Checked = false;
            if (cmbStore.Items.Count > 0) cmbStore.SelectedIndex = 0;
            rendering = false;
            RenderGameList();
            for (int i = 0; i < lvGames.Items.Count; i++)
            {
                if (ReferenceEquals(lvGames.Items[i].Tag, g))
                {
                    lvGames.Items[i].Selected = true;
                    lvGames.Items[i].EnsureVisible();
                    break;
                }
            }
        }

        private void StartScan(bool full)
        {
            if (Scanner.Running)
            {
                rescanPending = true;
                if (full) pendingFull = true;
                return;
            }
            List<string> libs = new List<string>();
            for (int i = 0; i < settings.SearchFolders.Count; i++)
            {
                if (!string.IsNullOrEmpty(settings.SearchFolders[i]) && Directory.Exists(settings.SearchFolders[i]))
                    libs.Add(settings.SearchFolders[i]);
            }
            string[] man = AppSettings.ReadExistingListFile(AppSettings.GetManualFile());
            string[] hid = AppSettings.ReadRawListFile(AppSettings.GetHiddenFile());
            btnRescan.Enabled = false;
            Scanner.Start(full, libs.ToArray(), man, hid, chkOther.Checked,
                settings.ExcludePaths.ToArray(), settings.ExcludeNames, settings.ScanCommon);
        }

        private void PumpScanner()
        {
            GameInfo gi;
            int n = 0;
            while (n < 300 && Scanner.Out.TryDequeue(out gi))
            {
                n++;
                string key = gi.Key;
                if (gi.Removed)
                {
                    GameInfo existing;
                    if (dgIndex.TryGetValue(key, out existing))
                    {
                        dgGames.Remove(existing);
                        dgIndex.Remove(key);
                    }
                }
                else
                {
                    UpdateGameStatus(gi);
                    GameInfo existing;
                    if (dgIndex.TryGetValue(key, out existing))
                    {
                        int idx = dgGames.IndexOf(existing);
                        if (idx >= 0) dgGames[idx] = gi; else dgGames.Add(gi);
                    }
                    else
                    {
                        dgGames.Add(gi);
                    }
                    dgIndex[key] = gi;
                    if (!string.IsNullOrEmpty(selectExe) && string.Equals(gi.ExePath, selectExe, StringComparison.OrdinalIgnoreCase))
                        selectGame = gi;
                }
                dirtyList = true;
            }

            bool running = Scanner.Running;
            if (running) lblScan.Text = Scanner.Status;
            if (dirtyList && (!running || (DateTime.Now - lastRender).TotalMilliseconds > 600))
            {
                UpdateStoreCombo();
                RenderGameList();
                if (selectGame != null)
                {
                    SelectGame(selectGame);
                    selectGame = null;
                    selectExe = null;
                }
            }
            if (wasRunning && !running)
            {
                btnRescan.Enabled = true;
                int count = 0;
                for (int i = 0; i < dgGames.Count; i++) if (IsVisibleGame(dgGames[i])) count++;
                string st = Scanner.Status ?? "";
                if (st.StartsWith("Erro")) lblScan.Text = st;
                else lblScan.Text = count + " jogo(s) na lista  ·  " + Scanner.PlaceCount + " local(is) salvo(s). Na próxima vez só o que mudou é analisado.";

                if (rescanPending)
                {
                    rescanPending = false;
                    bool f = pendingFull;
                    pendingFull = false;
                    StartScan(f);
                }
            }
            wasRunning = running;
        }

        private string GetGame() { return (txtGame.Text ?? "").Trim().Trim('"'); }

        private GameInfo GetCurrentGame()
        {
            string p = GetGame().TrimEnd('\\');
            if (lvGames.SelectedItems.Count > 0)
            {
                GameInfo s = lvGames.SelectedItems[0].Tag as GameInfo;
                if (s != null && (p.Length == 0 || string.Equals((s.ExeDir ?? "").TrimEnd('\\'), p, StringComparison.OrdinalIgnoreCase)))
                    return s;
            }
            if (p.Length > 0)
            {
                for (int i = 0; i < dgGames.Count; i++)
                {
                    if (!string.IsNullOrEmpty(dgGames[i].ExeDir) &&
                        string.Equals(dgGames[i].ExeDir.TrimEnd('\\'), p, StringComparison.OrdinalIgnoreCase))
                        return dgGames[i];
                }
            }
            return null;
        }

        private void SetCombo(ComboBox cmb, string val)
        {
            if (string.IsNullOrEmpty(val)) val = "auto";
            int i = cmb.Items.IndexOf(val);
            if (i < 0) i = cmb.Items.Add(val);
            cmb.SelectedIndex = i;
        }

        private void UpdateHeader()
        {
            GameInfo game = GetCurrentGame();
            string g = GetGame();
            if (game != null)
            {
                lblGameName.Text = game.Name;
                List<string> meta = new List<string>();
                meta.Add(game.Store);
                if (!string.IsNullOrEmpty(game.Api) && game.Api != "?") meta.Add(game.Api);
                if (!string.IsNullOrEmpty(game.Engine)) meta.Add(game.Engine);
                lblMeta.Text = string.Join("  ·  ", meta.ToArray());
            }
            else if (g.Length > 0)
            {
                lblGameName.Text = Path.GetFileName(g.TrimEnd('\\'));
                lblMeta.Text = "Pasta escolhida manualmente";
            }
            else
            {
                lblGameName.Text = "";
                lblMeta.Text = "";
            }
            string key = (game != null) ? game.Key : "";
            if (key != bannerGameKey)
            {
                bannerGameKey = key;
                RequestGameBanner(game);
            }
            headerIconExe = (game != null && game.ExePath != null) ? game.ExePath : "";
            btnPlay.Enabled = (game != null && !string.IsNullOrEmpty(game.ExePath));
            pHeader.Invalidate();
        }

        private void UpdateStatus()
        {
            string g = GetGame();
            GameState s = AnrInstaller.GetGameState(g);
            SetDetailMode(g.Length > 0);
            btnInstall.Enabled = !actionBusy && s.Valid && !s.Foreign;
            btnUninstall.Enabled = !actionBusy && s.Installed;
            btnResetAll.Enabled = !actionBusy && s.Installed;
            btnOpenGame.Enabled = s.Valid;

            if (g.Length == 0)
            {
                lblStatus.Text = "";
            }
            else if (!s.Valid)
            {
                lblStatus.Text = "Pasta inválida: nenhum .exe encontrado.";
                lblStatus.ForeColor = Theme.Err;
            }
            else if (s.Foreign)
            {
                lblStatus.Text = "Já existe um OptiScaler que não é deste pacote. Nada será alterado.";
                lblStatus.ForeColor = Theme.Warn;
            }
            else if (s.Installed)
            {
                string when = "";
                if (!string.IsNullOrEmpty(s.InstalledAt))
                {
                    DateTime dt;
                    if (DateTime.TryParse(s.InstalledAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                        when = "  ·  " + dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                }
                lblStatus.Text = "ANR FG instalado (" + s.Proxy + ")" + when;
                lblStatus.ForeColor = Theme.Ok;
            }
            else
            {
                lblStatus.Text = "Pronto para instalar";
                lblStatus.ForeColor = Theme.Info;
            }

            btnFgSave.Enabled = btnFgReload.Enabled = s.Installed;
            btnFgDefault.Enabled = s.Installed && PackageStore.HasEntry(PackageStore.RelFgIni);
            btnOptiSave.Enabled = btnOptiReload.Enabled = s.Installed;
            btnOptiReset.Enabled = s.Installed && PackageStore.HasEntry(PackageStore.RelOptiIni);
            btnOptiRestore.Enabled = s.Installed && g.Length > 0 && File.Exists(Path.Combine(g, "OptiScaler.ini.anrbak"));

            if (s.Installed) { LoadFgIni(); LoadOptiIni(); }
            UpdateHeader();
            RefreshLogs();
            recoDirty = true;
            if (tabReco.Visible) UpdateReco();
        }

        private void LoadFgIni()
        {
            string ini = Path.Combine(AnrInstaller.GetDlssgDir(GetGame()), "nr_fg.ini");
            if (!File.Exists(ini)) return;
            SetCombo(cmbPath, IniHelper.ReadValue(ini, "", "path"));
            int mg = 5; int.TryParse(IniHelper.ReadValue(ini, "", "max_generated"), out mg);
            SetGen(mg);
            int n = 80; int.TryParse(IniHelper.ReadValue(ini, "", "nits_per_unit"), out n);
            txtNits.Text = Math.Min(10000, Math.Max(1, n)).ToString(CultureInfo.InvariantCulture);
            chkScene.Checked = IniHelper.ReadValue(ini, "", "scene_cut") != "0";
            lModelVal.Text = (IniHelper.ReadValue(ini, "", "model_size") ?? "") + " (" + (IniHelper.ReadValue(ini, "", "model_note") ?? "") + ")";
            lBuildVal.Text = IniHelper.ReadValue(ini, "", "build") ?? "";
            lblFgMsg.Text = "";
        }

        private void LoadOptiIni()
        {
            string ini = Path.Combine(GetGame(), "OptiScaler.ini");
            if (!File.Exists(ini)) return;
            SetCombo(cmbFgEn, IniHelper.ReadValue(ini, "FrameGen", "Enabled"));
            SetCombo(cmbFgIn, IniHelper.ReadValue(ini, "FrameGen", "FGInput"));
            SetCombo(cmbFgOut, IniHelper.ReadValue(ini, "FrameGen", "FGOutput"));
            SetCombo(cmbUp, IniHelper.ReadValue(ini, "Upscalers", "Dx12Upscaler"));
            chkLog.Checked = string.Equals(IniHelper.ReadValue(ini, "Log", "LogToFile"), "true", StringComparison.OrdinalIgnoreCase);
            SetCombo(cmbLvl, IniHelper.ReadValue(ini, "Log", "LogLevel"));
            string k = IniHelper.ReadValue(ini, "Menu", "ShortcutKey");
            keyBox.FromIni(k);
            lblOptiMsg.Text = "";
        }

        private void ResetFgIni()
        {
            PackageStore.ExtractSingleFile(PackageStore.RelFgIni, Path.Combine(AnrInstaller.GetDlssgDir(GetGame()), "nr_fg.ini"));
        }

        private void ResetOptiIni()
        {
            PackageStore.ExtractSingleFile(PackageStore.RelOptiIni, Path.Combine(GetGame(), "OptiScaler.ini"));
        }

        private void RefreshLogs()
        {
            cmbLogFile.Items.Clear();
            txtLog.Clear();
            string g = GetGame();
            if (g.Length == 0 || !Directory.Exists(g)) return;

            List<string> files = new List<string>();
            string d = AnrInstaller.GetDlssgDir(g);
            if (Directory.Exists(d))
            {
                try
                {
                    FileInfo[] nrs = new DirectoryInfo(d).GetFiles("nr_fg-*.log");
                    Array.Sort(nrs, delegate(FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                    for (int i = 0; i < nrs.Length; i++) files.Add(nrs[i].FullName);
                }
                catch { }
            }
            string ol = Path.Combine(g, "OptiScaler.log");
            if (File.Exists(ol)) files.Add(ol);

            for (int i = 0; i < files.Count; i++) cmbLogFile.Items.Add(files[i]);
            if (cmbLogFile.Items.Count > 0) cmbLogFile.SelectedIndex = 0;
            else txtLog.Text = "Nenhum log ainda. Abra o jogo com o DLSS Frame Generation ativado e volte aqui.";
        }

        private void UpdateReco()
        {
            recoDirty = false;
            GpuInfo gpu = HardwareAdvisor.GetGpuInfo();
            GameInfo game = GetCurrentGame();
            rGpu.Text = gpu.Name;
            rArch.Text = string.IsNullOrEmpty(gpu.Arch) ? gpu.Vendor : (gpu.Vendor + " · " + gpu.Arch);
            rRes.Text = gpu.Width + "x" + gpu.Height + "  (" + gpu.SizeClass + ")";
            rGame.Text = (game != null) ? game.Name : "(nenhum selecionado)";
            rApi.Text = (game != null && !string.IsNullOrEmpty(game.Api)) ? game.Api : "-";

            Recommendation r = HardwareAdvisor.GetRecommendation(game);
            currentReco = r;
            rProxy.Text = r.Proxy;
            rSummary.Text = "path = " + r.Path + "    ·    até " + (r.MaxGen + 1) + "X (" + r.MaxGen + " gerado(s))    ·    scene_cut ligado";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < r.Lines.Count; i++)
            {
                if (i > 0) sb.Append("\r\n\r\n");
                sb.Append("• ").Append(r.Lines[i]);
            }
            rNotes.Text = sb.ToString();

            GameState st = AnrInstaller.GetGameState(GetGame());
            btnRecoApply.Enabled = st.Installed;
            lblRecoMsg.ForeColor = Theme.Muted;
            lblRecoMsg.Text = st.Installed ? "" : "Instale o ANR FG neste jogo para poder aplicar.";
        }

        private void AppendOut(string text)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { AppendOut(text); }); } catch { }
                return;
            }
            txtOut.AppendText(text + "\r\n");
        }

        private void RefreshGameRow(string dir)
        {
            string norm = dir.TrimEnd('\\');
            for (int i = 0; i < dgGames.Count; i++)
            {
                if (!string.IsNullOrEmpty(dgGames[i].ExeDir) &&
                    string.Equals(dgGames[i].ExeDir.TrimEnd('\\'), norm, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateGameStatus(dgGames[i]);
                    RenderGameList();
                    break;
                }
            }
        }

        private void DoAction(string kind)
        {
            if (actionBusy) return;
            string g = GetGame();
            GameState s = AnrInstaller.GetGameState(g);
            if (!s.Valid) return;

            string forcedProxy = null;
            if (kind == "install")
            {
                switch (cmbProxy.SelectedIndex)
                {
                    case 1: forcedProxy = "dxgi.dll"; break;
                    case 2: forcedProxy = "winmm.dll"; break;
                    case 3: forcedProxy = "version.dll"; break;
                }
            }
            else
            {
                if (!Theme.ShowMsg(this,
                    "Desinstalar o ANR FG e o OptiScaler de:\n" + g + "\n\nOs arquivos originais com backup serão restaurados.",
                    "Confirmar", true))
                    return;
            }

            actionBusy = true;
            Cursor = Cursors.WaitCursor;
            btnInstall.Enabled = false;
            btnUninstall.Enabled = false;
            btnResetAll.Enabled = false;
            txtOut.Clear();
            AppendOut(kind == "install" ? "> Instalando em " + g + " ..." : "> Desinstalando de " + g + " ...");

            Thread t = new Thread(delegate()
            {
                bool ok = true;
                try
                {
                    if (kind == "install")
                        AnrInstaller.Install(g, forcedProxy, AppendOut);
                    else
                        AnrInstaller.Uninstall(g, AppendOut);
                }
                catch (Exception ex)
                {
                    ok = false;
                    AppendOut("[ERRO] " + ex.Message);
                }

                if (!IsDisposed && IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (IsDisposed) return;
                            AppendOut(ok ? "\r\n[OK] Concluído." : "\r\n[ERRO] Operação não concluída.");
                            actionBusy = false;
                            Cursor = Cursors.Default;
                            RefreshGameRow(g);
                            UpdateStatus();
                        });
                    }
                    catch { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void AddManualGame(string exe)
        {
            string dir = Path.GetDirectoryName(exe).TrimEnd('\\');
            for (int i = 0; i < dgGames.Count; i++)
            {
                if (!string.IsNullOrEmpty(dgGames[i].ExeDir) &&
                    string.Equals(dgGames[i].ExeDir.TrimEnd('\\'), dir, StringComparison.OrdinalIgnoreCase))
                {
                    SelectGame(dgGames[i]);
                    return;
                }
            }
            string mf = AppSettings.GetManualFile();
            bool exists = false;
            if (File.Exists(mf))
            {
                string[] lines = File.ReadAllLines(mf);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.Equals(lines[i].Trim(), exe, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                }
            }
            if (!exists) File.AppendAllText(mf, exe + Environment.NewLine);
            selectExe = exe;
            StartScan(false);
        }

        private void RemoveGameFromList(GameInfo g)
        {
            string k = g.Key;
            GameInfo ex;
            if (dgIndex.TryGetValue(k, out ex))
            {
                dgGames.Remove(ex);
                dgIndex.Remove(k);
            }
            RenderGameList();
        }

        private void SaveUiPrefs()
        {
            if (loadingPrefs) return;
            settings.Sort = cmbSort.SelectedIndex;
            settings.Compat = chkCompat.Checked;
            settings.Installed = chkInstalled.Checked;
            settings.Other = chkOther.Checked;
            settings.Save();
        }

        private void ShowSettingsDialog()
        {
            using (Form f = Theme.NewDarkForm("Configurações", 690, 618))
            {
                f.Controls.Add(Theme.NewLabel("Pastas de busca  (cada subpasta delas é tratada como um jogo)", 20, 16));
                DarkListBox sdSearch = Theme.NewDarkList(20, 42, 540, 84);
                for (int i = 0; i < settings.SearchFolders.Count; i++) sdSearch.Items.Add(settings.SearchFolders[i]);
                Button bAdd1 = Theme.NewButton("Adicionar...", 570, 42, 100, 34, "ghost");
                Button bDel1 = Theme.NewButton("Remover", 570, 82, 100, 34, "ghost");
                bAdd1.Click += delegate
                {
                    string p = Theme.PickFolder(f, "Escolha uma pasta que contenha várias pastas de jogos (ex.: D:\\Jogos)");
                    if (!string.IsNullOrEmpty(p) && sdSearch.Items.IndexOf(p) < 0) sdSearch.Items.Add(p);
                };
                bDel1.Click += delegate { if (sdSearch.SelectedIndex >= 0) sdSearch.Items.RemoveAt(sdSearch.SelectedIndex); };

                f.Controls.Add(Theme.NewLabel("Pastas excluídas  (nunca são procuradas nem mostradas, com tudo que há dentro)", 20, 142));
                DarkListBox sdExclude = Theme.NewDarkList(20, 168, 540, 84);
                for (int i = 0; i < settings.ExcludePaths.Count; i++) sdExclude.Items.Add(settings.ExcludePaths[i]);
                Button bAdd2 = Theme.NewButton("Adicionar...", 570, 168, 100, 34, "ghost");
                Button bDel2 = Theme.NewButton("Remover", 570, 208, 100, 34, "ghost");
                bAdd2.Click += delegate
                {
                    string p = Theme.PickFolder(f, "Escolha uma pasta para nunca procurar jogos nela");
                    if (!string.IsNullOrEmpty(p) && sdExclude.Items.IndexOf(p) < 0) sdExclude.Items.Add(p);
                };
                bDel2.Click += delegate { if (sdExclude.SelectedIndex >= 0) sdExclude.Items.RemoveAt(sdExclude.SelectedIndex); };

                f.Controls.Add(Theme.NewLabel("Ignorar pastas cujo nome contenha  (separe por vírgula; vale para pastas comuns, Xbox e programas)", 20, 268));
                RoundBox sdNames = Theme.NewInput(20, 294, 650, false);
                sdNames.Text = settings.ExcludeNames ?? "";

                DarkCheck sdCommon = Theme.NewCheck("Procurar também em pastas chamadas Games e Jogos, na raiz dos discos", 18, 340);
                sdCommon.Checked = settings.ScanCommon;
                DarkCheck sdTray = Theme.NewCheck("Minimizar para a bandeja ao iniciar um jogo pelo botão Jogar", 18, 368);
                sdTray.Checked = settings.TrayOnPlay;
                DarkCheck sdBanner = Theme.NewCheck("Baixar banners dos jogos da Steam (usa a internet; o cache da Steam é usado primeiro)", 18, 396);
                sdBanner.Checked = settings.DownloadBanners;

                f.Controls.Add(Theme.NewLabel("Jogos ocultos  (para ocultar um: clique com o botão direito no jogo da lista)", 20, 436));
                DarkListBox sdHidden = Theme.NewDarkList(20, 462, 540, 64);
                string[] curHid = AppSettings.ReadRawListFile(AppSettings.GetHiddenFile());
                for (int i = 0; i < curHid.Length; i++) sdHidden.Items.Add(curHid[i]);
                Button bRes = Theme.NewButton("Restaurar", 570, 462, 100, 34, "ghost");
                bRes.Click += delegate { if (sdHidden.SelectedIndex >= 0) sdHidden.Items.RemoveAt(sdHidden.SelectedIndex); };

                Button bSave = Theme.NewButton("Salvar e atualizar", 398, 564, 178, 40, "primary");
                bSave.DialogResult = DialogResult.OK;
                Button bCancel = Theme.NewButton("Cancelar", 584, 564, 86, 40, "ghost");
                bCancel.DialogResult = DialogResult.Cancel;
                f.CancelButton = bCancel;

                f.Controls.AddRange(new Control[] {
                    sdSearch, bAdd1, bDel1, sdExclude, bAdd2, bDel2, sdNames, sdCommon, sdTray, sdBanner, sdHidden, bRes, bSave, bCancel
                });

                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    settings.SearchFolders.Clear();
                    for (int i = 0; i < sdSearch.Items.Count; i++) settings.SearchFolders.Add(Convert.ToString(sdSearch.Items[i]));
                    settings.ExcludePaths.Clear();
                    for (int i = 0; i < sdExclude.Items.Count; i++) settings.ExcludePaths.Add(Convert.ToString(sdExclude.Items[i]));
                    settings.ExcludeNames = sdNames.Text;
                    settings.ScanCommon = sdCommon.Checked;
                    settings.TrayOnPlay = sdTray.Checked;
                    settings.DownloadBanners = sdBanner.Checked;

                    List<string> newHid = new List<string>();
                    for (int i = 0; i < sdHidden.Items.Count; i++) newHid.Add(Convert.ToString(sdHidden.Items[i]));
                    string hf = AppSettings.GetHiddenFile();
                    if (newHid.Count > 0) File.WriteAllLines(hf, newHid.ToArray());
                    else if (File.Exists(hf)) File.Delete(hf);

                    settings.Save();
                    StartScan(true);
                }
            }
        }

        private void UpdateAllInstalled(IWin32Window owner)
        {
            List<GameInfo> targets = new List<GameInfo>();
            for (int i = 0; i < dgGames.Count; i++)
            {
                if (dgGames[i].Status == "Instalado" && !string.IsNullOrEmpty(dgGames[i].ExeDir))
                    targets.Add(dgGames[i]);
            }
            if (targets.Count == 0)
            {
                Theme.ShowMsg(owner, "Nenhum jogo da lista tem o ANR FG instalado.", "ANR FG Manager", false);
                return;
            }
            if (!Theme.ShowMsg(owner,
                "Atualizar o ANR FG em " + targets.Count + " jogo(s) com o pacote atual (" + PackageStore.PackageVersion + ")?",
                "Confirmar", true))
                return;

            List<string> fail = new List<string>();
            Cursor = Cursors.WaitCursor;
            try
            {
                txtOut.Clear();
                for (int i = 0; i < targets.Count; i++)
                {
                    GameInfo g = targets[i];
                    lblScan.Text = "Atualizando " + (i + 1) + " de " + targets.Count + ": " + g.Name;
                    Application.DoEvents();
                    AppendOut("> " + g.Name);
                    try { AnrInstaller.Install(g.ExeDir, null, AppendOut); }
                    catch (Exception ex)
                    {
                        fail.Add(g.Name);
                        AppendOut("[ERRO] " + ex.Message);
                    }
                    UpdateGameStatus(g);
                }
            }
            finally { Cursor = Cursors.Default; }

            RenderGameList();
            UpdateStatus();
            if (fail.Count == 0)
                Theme.ShowMsg(owner, "Pronto: ANR FG atualizado em " + targets.Count + " jogo(s).", "ANR FG Manager", false);
            else
                Theme.ShowMsg(owner, "Atualizado em " + (targets.Count - fail.Count) + " jogo(s). Falhou em:\n" +
                    string.Join("\n", fail.ToArray()) + "\n\nVeja os detalhes na aba Instalação.", "ANR FG Manager", false);
        }

        private static string Shorten(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length > n ? s.Substring(0, n) + "…" : s;
        }

        // ---------------------------------------------------------------- atualizacoes pelo GitHub
        // pacote ANR FG: repositorio oficial (apex-compute/ANR). programa: repositorio proprio, opcional.
        private void StartUpdateCheck()
        {
            if (!settings.CheckOnStart) return;
            string pkgRepo = AppUpdater.NormalizeRepo(settings.PackageRepo);
            string appRepo = AppUpdater.NormalizeRepo(settings.GithubRepo);
            if (pkgRepo == null && appRepo == null) return;
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                string err;
                ReleaseInfo pkg = null;
                ReleaseInfo app = null;
                if (pkgRepo != null)
                {
                    ReleaseInfo r1 = AppUpdater.CheckLatest(pkgRepo, out err);
                    if (r1 != null && AppUpdater.IsPackageNewer(r1, PackageStore.PackageVersion)) pkg = r1;
                }
                if (appRepo != null)
                {
                    ReleaseInfo r2 = AppUpdater.CheckLatest(appRepo, out err);
                    if (r2 != null && AppUpdater.IsNewer(r2)) app = r2;
                }
                if (pkg != null || app != null)
                {
                    try { BeginInvoke(new MethodInvoker(delegate { pendingPackage = pkg; pendingRelease = app; RefreshUpdateLink(); })); } catch { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void RefreshUpdateLink()
        {
            if (pendingPackage != null || pendingRelease != null)
            {
                if (pendingPackage != null && pendingRelease != null) lnkUpdate.Text = "atualizações disponíveis";
                else if (pendingPackage != null) lnkUpdate.Text = "pacote " + pendingPackage.PkgText + " disponível";
                else lnkUpdate.Text = "programa " + pendingRelease.VersionText + " disponível";
                lnkUpdate.LinkColor = Theme.Warn;
                lnkUpdate.ActiveLinkColor = Theme.Warn;
                lnkUpdate.VisitedLinkColor = Theme.Warn;
            }
            else
            {
                lnkUpdate.Text = "v" + PackageStore.AppVersion + "  ·  atualizações";
                lnkUpdate.LinkColor = Theme.Accent;
                lnkUpdate.ActiveLinkColor = Theme.AccentHi;
                lnkUpdate.VisitedLinkColor = Theme.Accent;
            }
        }

        // baixa um arquivo para a pasta temporaria (mostra o andamento em "status"); null se falhar
        private string DownloadAsset(string url, string ext, string label, Label status)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "anrfg-dl-" + Guid.NewGuid().ToString("N") + ext);
            bool done = false;
            Exception derr = null;
            using (WebClient wc = new WebClient())
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                wc.Headers.Add("User-Agent", "ANR-FG-Manager/" + PackageStore.AppVersion);
                wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
                {
                    status.ForeColor = Theme.Muted;
                    status.Text = "Baixando " + label + "...  " + e.ProgressPercentage + "%";
                };
                wc.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e)
                {
                    derr = e.Error;
                    done = true;
                };
                status.ForeColor = Theme.Muted;
                status.Text = "Baixando " + label + "...";
                wc.DownloadFileAsync(new Uri(url), tmp);
                while (!done)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(30);
                }
            }
            if (derr != null)
            {
                status.ForeColor = Theme.Err;
                status.Text = "Falha no download: " + derr.Message;
                try { File.Delete(tmp); } catch { }
                return null;
            }
            return tmp;
        }

        // atualiza o PROGRAMA: retorna true quando precisa fechar (a versao nova ja foi aberta)
        private bool ApplyRelease(IWin32Window owner, ReleaseInfo r, Label status)
        {
            string url; string sha; bool swap;
            if (!string.IsNullOrEmpty(r.ExeUrl)) { url = r.ExeUrl; sha = r.ExeSha; swap = true; }
            else if (!string.IsNullOrEmpty(r.SetupUrl)) { url = r.SetupUrl; sha = r.SetupSha; swap = false; }
            else
            {
                status.ForeColor = Theme.Err;
                status.Text = "A release " + r.Tag + " não tem um .exe anexado (anexe ANR-FG-Manager.exe e/ou o Setup).";
                return false;
            }
            if (!Theme.ShowMsg(owner,
                "Baixar e instalar a versão " + r.VersionText + " do programa?\n\nO programa será fechado e reaberto automaticamente.",
                "Atualizar", true))
                return false;

            string tmp = DownloadAsset(url, ".exe", "a versão " + r.VersionText, status);
            if (tmp == null) return false;

            string err;
            if (!AppUpdater.VerifyDownload(tmp, sha, true, out err))
            {
                status.ForeColor = Theme.Err;
                status.Text = err;
                try { File.Delete(tmp); } catch { }
                return false;
            }
            if (swap)
            {
                if (!AppUpdater.ApplyExeSwap(tmp, out err))
                {
                    status.ForeColor = Theme.Err;
                    status.Text = err;
                    try { File.Delete(tmp); } catch { }
                    return false;
                }
                return true;
            }
            if (!AppUpdater.LaunchSetupUpdate(tmp, out err))
            {
                status.ForeColor = Theme.Err;
                status.Text = err;
                return false;
            }
            return true;
        }

        private void ShowUpdatesDialog()
        {
            bool restartAfter = false;
            using (Form f = Theme.NewDarkForm("Atualizações", 640, 706))
            {
                Label lblInfo = Theme.NewLabel("", 20, 16);
                Action refreshHeader = delegate
                {
                    string src = PackageStore.UsingOverride ? "importado" : "embutido";
                    string curPkg = string.IsNullOrEmpty(PackageStore.PackageVersion) ? "não identificada" : PackageStore.PackageVersion;
                    lblInfo.Text = "Programa: v" + PackageStore.AppVersion + "    ·    Pacote ANR FG: " + curPkg + " (" + src + ")";
                    lblSub.Text = "Apex Neural Rendering  " + PackageStore.PackageVersion;
                };
                refreshHeader();
                f.Controls.Add(lblInfo);

                // ================= A) pacote ANR FG (GitHub oficial)
                Label hA = Theme.NewLabel("Pacote ANR FG  ·  atualizações pelo GitHub", 20, 50); hA.Font = Theme.FontB;
                f.Controls.Add(hA);
                f.Controls.Add(Theme.NewHint("Repositório oficial das releases do ANR FG (padrão: apex-compute/ANR).", 20, 74));
                RoundBox udPkgRepo = Theme.NewInput(20, 98, 430, false);
                udPkgRepo.Text = string.IsNullOrEmpty(settings.PackageRepo) ? AppUpdater.DefaultPackageRepo : settings.PackageRepo;
                Button bCheckPkg = Theme.NewButton("Verificar agora", 460, 98, 160, 34, "primary");
                Label relPkg = new Label();
                relPkg.Location = new Point(20, 140);
                relPkg.Size = new Size(600, 46);
                relPkg.ForeColor = Theme.Muted;
                Button bApplyPkg = Theme.NewButton("Baixar e importar", 20, 190, 190, 36, "primary");
                bApplyPkg.Visible = false;

                // ================= B) programa (opcional)
                Label hB = Theme.NewLabel("Programa (ANR FG Manager)  ·  atualizações pelo GitHub  (opcional)", 20, 242); hB.Font = Theme.FontB;
                f.Controls.Add(hB);
                f.Controls.Add(Theme.NewHint("Só se você publica releases do próprio programa. Deixe em branco se não usa.", 20, 266));
                RoundBox udRepo = Theme.NewInput(20, 290, 430, false);
                udRepo.Text = settings.GithubRepo ?? "";
                Button bCheck = Theme.NewButton("Verificar", 460, 290, 160, 34, "ghost");
                Label relInfo = new Label();
                relInfo.Location = new Point(20, 332);
                relInfo.Size = new Size(600, 40);
                relInfo.ForeColor = Theme.Muted;
                Button bApply = Theme.NewButton("Atualizar o programa", 20, 374, 200, 36, "primary");
                bApply.Visible = false;

                DarkCheck udAuto = Theme.NewCheck("Verificar atualizações ao abrir o programa", 18, 422);
                udAuto.Checked = settings.CheckOnStart;

                // ================= C) importacao manual
                Label h1 = Theme.NewLabel("Importar pacote manualmente", 20, 462); h1.Font = Theme.FontB;
                f.Controls.Add(h1);
                f.Controls.Add(Theme.NewHint("Use um .zip de pacote que você já baixou (ex.: anr-fg-V.B21-full.zip).", 20, 486));
                Button bFile = Theme.NewButton("Escolher arquivo .zip...", 20, 512, 185, 34, "ghost");
                Button bRevert = Theme.NewButton("Voltar ao pacote embutido", 215, 512, 195, 34, "ghost");
                bRevert.Enabled = PackageStore.UsingOverride;
                Button bAll = Theme.NewButton("Atualizar o ANR FG em todos os jogos instalados", 20, 556, 360, 34, "ghost");

                Label udMsg = new Label();
                udMsg.Location = new Point(20, 602);
                udMsg.Size = new Size(600, 56);
                udMsg.ForeColor = Theme.Muted;

                Button bClose = Theme.NewButton("Fechar", 534, 662, 86, 34, "ghost");
                bClose.DialogResult = DialogResult.Cancel;
                f.CancelButton = bClose;

                // ---- importar um .zip de pacote (usado pelo arquivo local e pelo download do GitHub)
                Func<string, Label, bool> importZip = delegate(string zipPath, Label target)
                {
                    try
                    {
                        string oldV, newV;
                        string err = PackageStore.ValidateAndImportZip(zipPath, out oldV, out newV);
                        if (!string.IsNullOrEmpty(err))
                        {
                            target.ForeColor = Theme.Err;
                            target.Text = err;
                            return false;
                        }
                        refreshHeader();
                        bRevert.Enabled = PackageStore.UsingOverride;
                        target.ForeColor = Theme.Ok;
                        target.Text = "Pacote importado: " + oldV + " → " + newV + ".";
                        if (Theme.ShowMsg(f, "Pacote atualizado. Reinstalar o ANR FG agora nos jogos que já o usam?", "Confirmar", true))
                            UpdateAllInstalled(f);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        target.ForeColor = Theme.Err;
                        target.Text = "Erro ao importar: " + ex.Message;
                        return false;
                    }
                };

                ReleaseInfo shownPkg = null;
                Action<ReleaseInfo> showPkg = delegate(ReleaseInfo r)
                {
                    shownPkg = r;
                    bApplyPkg.Visible = false;
                    if (r == null) return;
                    if (r.PkgNumber <= 0)
                    {
                        relPkg.ForeColor = Theme.Warn;
                        relPkg.Text = "Não consegui identificar a versão da release \"" + r.Tag + "\". Veja em: " + r.Page;
                    }
                    else if (AppUpdater.ParsePackageNumber(PackageStore.PackageVersion) <= 0)
                    {
                        relPkg.ForeColor = Theme.Warn;
                        relPkg.Text = "Não consegui identificar a versão do pacote atual. Última release: " + r.PkgText + ".\n" + Shorten(r.Notes, 120);
                        bApplyPkg.Visible = !string.IsNullOrEmpty(r.PkgUrl);
                    }
                    else if (AppUpdater.IsPackageNewer(r, PackageStore.PackageVersion))
                    {
                        if (string.IsNullOrEmpty(r.PkgUrl))
                        {
                            relPkg.ForeColor = Theme.Warn;
                            relPkg.Text = "Pacote " + r.PkgText + " disponível, mas sem o arquivo anr-fg-*-full.zip. Baixe em: " + r.Page;
                        }
                        else
                        {
                            relPkg.ForeColor = Theme.Warn;
                            relPkg.Text = "Pacote " + r.PkgText + " disponível (você usa " + PackageStore.PackageVersion + ").\n" + Shorten(r.Notes, 170);
                            bApplyPkg.Visible = true;
                        }
                    }
                    else
                    {
                        relPkg.ForeColor = Theme.Ok;
                        relPkg.Text = "Você já está no pacote mais recente (" + PackageStore.PackageVersion + ").";
                    }
                };
                if (pendingPackage != null) showPkg(pendingPackage);

                bCheckPkg.Click += delegate
                {
                    string repo = AppUpdater.NormalizeRepo(udPkgRepo.Text);
                    if (repo == null)
                    {
                        relPkg.ForeColor = Theme.Err;
                        relPkg.Text = "Informe o repositório no formato usuario/repositorio (ou cole o link do GitHub).";
                        bApplyPkg.Visible = false;
                        return;
                    }
                    udPkgRepo.Text = repo;
                    settings.PackageRepo = repo;
                    settings.CheckOnStart = udAuto.Checked;
                    settings.Save();
                    relPkg.ForeColor = Theme.Muted;
                    relPkg.Text = "Consultando o GitHub...";
                    Application.DoEvents();
                    string err;
                    ReleaseInfo r = AppUpdater.CheckLatest(repo, out err);
                    if (r == null)
                    {
                        relPkg.ForeColor = Theme.Err;
                        relPkg.Text = err;
                        bApplyPkg.Visible = false;
                        return;
                    }
                    pendingPackage = AppUpdater.IsPackageNewer(r, PackageStore.PackageVersion) ? r : null;
                    RefreshUpdateLink();
                    showPkg(r);
                };

                bApplyPkg.Click += delegate
                {
                    if (shownPkg == null || string.IsNullOrEmpty(shownPkg.PkgUrl)) return;
                    if (!Theme.ShowMsg(f, "Baixar o pacote ANR FG " + shownPkg.PkgText + " e importar?", "Atualizar", true)) return;
                    string tmp = DownloadAsset(shownPkg.PkgUrl, ".zip", "o pacote " + shownPkg.PkgText, relPkg);
                    if (tmp == null) return;
                    string verr;
                    if (!AppUpdater.VerifyDownload(tmp, shownPkg.PkgSha, false, out verr))
                    {
                        relPkg.ForeColor = Theme.Err;
                        relPkg.Text = verr;
                        try { File.Delete(tmp); } catch { }
                        return;
                    }
                    bool ok;
                    try { ok = importZip(tmp, relPkg); }
                    finally { try { File.Delete(tmp); } catch { } }
                    if (ok)
                    {
                        pendingPackage = null;
                        RefreshUpdateLink();
                        bApplyPkg.Visible = false;
                    }
                };

                // ---- programa
                ReleaseInfo shown = null;
                Action<ReleaseInfo> showRelease = delegate(ReleaseInfo r)
                {
                    shown = r;
                    bApply.Visible = false;
                    if (r == null) return;
                    if (r.Ver == null)
                    {
                        relInfo.ForeColor = Theme.Warn;
                        relInfo.Text = "A release \"" + r.Tag + "\" não tem número de versão do programa (use tags como v2.3.0). Este repositório parece publicar outra coisa.";
                    }
                    else if (AppUpdater.IsNewer(r))
                    {
                        relInfo.ForeColor = Theme.Warn;
                        relInfo.Text = "Versão " + r.VersionText + " disponível (você usa a " + PackageStore.AppVersion + ").\n" + Shorten(r.Notes, 120);
                        bApply.Visible = true;
                    }
                    else
                    {
                        relInfo.ForeColor = Theme.Ok;
                        relInfo.Text = "Você já está na versão mais recente (" + PackageStore.AppVersion + ").";
                    }
                };
                if (pendingRelease != null) showRelease(pendingRelease);

                bCheck.Click += delegate
                {
                    string repo = AppUpdater.NormalizeRepo(udRepo.Text);
                    if (repo == null)
                    {
                        relInfo.ForeColor = Theme.Err;
                        relInfo.Text = "Informe o repositório no formato usuario/repositorio (ou cole o link do GitHub).";
                        bApply.Visible = false;
                        return;
                    }
                    udRepo.Text = repo;
                    settings.GithubRepo = repo;
                    settings.CheckOnStart = udAuto.Checked;
                    settings.Save();
                    relInfo.ForeColor = Theme.Muted;
                    relInfo.Text = "Consultando o GitHub...";
                    Application.DoEvents();
                    string err;
                    ReleaseInfo r = AppUpdater.CheckLatest(repo, out err);
                    if (r == null)
                    {
                        relInfo.ForeColor = Theme.Err;
                        relInfo.Text = err;
                        bApply.Visible = false;
                        return;
                    }
                    pendingRelease = AppUpdater.IsNewer(r) ? r : null;
                    RefreshUpdateLink();
                    showRelease(r);
                };
                bApply.Click += delegate
                {
                    if (shown == null) return;
                    if (ApplyRelease(f, shown, relInfo)) { restartAfter = true; f.Close(); }
                };

                // ---- manual
                bFile.Click += delegate
                {
                    using (OpenFileDialog d = new OpenFileDialog())
                    {
                        d.Title = "Escolha o pacote ANR FG (.zip)";
                        d.Filter = "Pacote ANR FG (*.zip)|*.zip";
                        if (d.ShowDialog(f) == DialogResult.OK) importZip(d.FileName, udMsg);
                    }
                };
                bRevert.Click += delegate
                {
                    if (!Theme.ShowMsg(f, "Remover o pacote importado e voltar ao pacote embutido neste executável?", "Confirmar", true)) return;
                    PackageStore.RevertToEmbedded();
                    refreshHeader();
                    bRevert.Enabled = false;
                    udMsg.ForeColor = Theme.Ok;
                    udMsg.Text = "Voltou ao pacote embutido (" + PackageStore.PackageVersion + ").";
                };
                bAll.Click += delegate { UpdateAllInstalled(f); };

                f.Controls.AddRange(new Control[] {
                    udPkgRepo, bCheckPkg, relPkg, bApplyPkg,
                    udRepo, bCheck, relInfo, bApply,
                    udAuto, bFile, bRevert, bAll, udMsg, bClose
                });
                f.ShowDialog(this);

                string pkgSaved = AppUpdater.NormalizeRepo(udPkgRepo.Text);
                if (pkgSaved != null) settings.PackageRepo = pkgSaved;
                string repoSaved = AppUpdater.NormalizeRepo(udRepo.Text);
                settings.GithubRepo = (repoSaved != null) ? repoSaved : "";
                settings.CheckOnStart = udAuto.Checked;
                settings.Save();
            }
            if (restartAfter) Close();
        }

        // ---------------------------------------------------------------- imagens (icone do .exe e banner da Steam)
        private Bitmap GetExeIcon(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return null;
            Bitmap bmp;
            if (iconCache.TryGetValue(exe, out bmp)) return bmp;
            bmp = null;
            try
            {
                using (Icon ic = Icon.ExtractAssociatedIcon(exe))
                {
                    if (ic != null) bmp = ic.ToBitmap();
                }
            }
            catch { }
            iconCache[exe] = bmp;
            return bmp;
        }

        private static Bitmap LoadImageSafe(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image img = Image.FromStream(ms))
                {
                    return new Bitmap(img);
                }
            }
            catch { return null; }
        }

        private static string GetSteamRoot()
        {
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
                if (v is string && ((string)v).Length > 0) return ((string)v).Replace('/', '\\');
                v = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null);
                if (v is string && ((string)v).Length > 0) return ((string)v).Replace('/', '\\');
            }
            catch { }
            return null;
        }

        // procura a imagem no cache da propria Steam (sem internet)
        private static string FindSteamLocalImage(string appId, string name)
        {
            string root = GetSteamRoot();
            if (root == null) return null;
            string lc = Path.Combine(Path.Combine(root, "appcache"), "librarycache");
            string old = Path.Combine(lc, appId + "_" + name);
            if (File.Exists(old)) return old;
            string dir = Path.Combine(lc, appId);
            if (Directory.Exists(dir))
            {
                try
                {
                    string[] found = Directory.GetFiles(dir, name, SearchOption.AllDirectories);
                    if (found.Length > 0) return found[0];
                }
                catch { }
            }
            return null;
        }

        private static string GetImgDir()
        {
            string d = Path.Combine(AppSettings.GetDataDir(), "img");
            Directory.CreateDirectory(d);
            return d;
        }

        private void StartBannerDownload(string id, string kind)
        {
            if (bannerWc != null || !settings.DownloadBanners) return;
            try
            {
                string dest = Path.Combine(GetImgDir(), id + "_hero.jpg");
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                bannerKind = kind;
                bannerWc = new WebClient();
                bannerWc.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e)
                {
                    BannerDone(e.Error == null && !e.Cancelled, dest);
                };
                bannerWc.DownloadFileAsync(new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/" + id + "/" + kind), dest);
            }
            catch { bannerWc = null; }
        }

        private void BannerDone(bool ok, string dest)
        {
            string id = bannerFor;
            WebClient w = bannerWc;
            bannerWc = null;
            if (w != null) w.Dispose();
            if (!ok)
            {
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                if (bannerKind == "library_hero.jpg" && !string.IsNullOrEmpty(id)) StartBannerDownload(id, "header.jpg");
                return;
            }
            RequestGameBanner(GetCurrentGame());
        }

        // carrega o banner do jogo selecionado (Steam: cache local ou download em segundo plano)
        private void RequestGameBanner(GameInfo g)
        {
            if (heroImg != null) { heroImg.Dispose(); heroImg = null; }
            if (g != null && g.Store == "Steam" && !string.IsNullOrEmpty(g.AppId))
            {
                string id = g.AppId;
                bannerFor = id;
                string path = FindSteamLocalImage(id, "library_hero.jpg");
                if (path == null) path = FindSteamLocalImage(id, "header.jpg");
                if (path == null)
                {
                    string cached = Path.Combine(GetImgDir(), id + "_hero.jpg");
                    if (File.Exists(cached)) path = cached;
                    else StartBannerDownload(id, "library_hero.jpg");
                }
                if (path != null) heroImg = LoadImageSafe(path);
            }
            if (pHeader != null) pHeader.Invalidate();
        }

        // ---------------------------------------------------------------- jogar + bandeja do sistema
        private void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = Icon ?? SystemIcons.Application;
            tray.Text = "ANR FG Manager";
            tray.Visible = false;

            ContextMenuStrip tm = new ContextMenuStrip();
            tm.BackColor = Theme.Panel;
            tm.ForeColor = Theme.Text;
            tm.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            ToolStripItem miOpen = tm.Items.Add("Abrir o ANR FG Manager");
            ToolStripItem miExit = tm.Items.Add("Sair");
            miOpen.ForeColor = Theme.Text;
            miExit.ForeColor = Theme.Text;
            miOpen.Click += delegate { ShowFromTray(); };
            miExit.Click += delegate { tray.Visible = false; Show(); Close(); };
            tray.ContextMenuStrip = tm;
            tray.DoubleClick += delegate { ShowFromTray(); };

            watchTimer = new System.Windows.Forms.Timer();
            watchTimer.Interval = 3000;
            watchTimer.Tick += delegate
            {
                if (watchName.Length == 0) { watchTimer.Stop(); return; }
                bool running = false;
                try { running = Process.GetProcessesByName(watchName).Length > 0; } catch { }
                if (running) watchSeen = true;
                else if (watchSeen)
                {
                    watchName = "";
                    watchTimer.Stop();
                    if (!Visible) ShowFromTray();
                }
                else if ((DateTime.Now - watchStart).TotalSeconds > 180)
                {
                    watchName = "";
                    watchTimer.Stop();
                }
            };
        }

        private void ShowFromTray()
        {
            if (tray != null) tray.Visible = false;
            Show();
            WindowState = prevState;
            Activate();
        }

        private void HideToTray()
        {
            prevState = (WindowState == FormWindowState.Minimized) ? FormWindowState.Normal : WindowState;
            tray.Visible = true;
            Hide();
            tray.ShowBalloonTip(2500, "ANR FG Manager", "Continua aqui na bandeja. Clique duas vezes no ícone para abrir.", ToolTipIcon.Info);
        }

        // abre o jogo (Steam/Epic pelo proprio launcher; os demais direto pelo .exe)
        private void PlayGame(GameInfo g)
        {
            if (g == null || string.IsNullOrEmpty(g.ExePath)) return;
            try
            {
                if (g.Store == "Steam" && !string.IsNullOrEmpty(g.AppId))
                {
                    Process.Start("steam://rungameid/" + g.AppId);
                }
                else if (g.Store == "Epic" && !string.IsNullOrEmpty(g.AppId) && !g.AppId.StartsWith(":"))
                {
                    Process.Start("com.epicgames.launcher://apps/" + g.AppId.Replace(":", "%3A") + "?action=launch&silent=true");
                }
                else
                {
                    ProcessStartInfo psi = new ProcessStartInfo(g.ExePath);
                    psi.WorkingDirectory = g.ExeDir;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                Theme.ShowMsg(this, "Não foi possível abrir o jogo:\n" + ex.Message, "ANR FG Manager", false);
                return;
            }
            watchName = Path.GetFileNameWithoutExtension(g.ExePath);
            watchSeen = false;
            watchStart = DateTime.Now;
            watchTimer.Start();
            if (settings.TrayOnPlay) HideToTray();
        }
    }
}
