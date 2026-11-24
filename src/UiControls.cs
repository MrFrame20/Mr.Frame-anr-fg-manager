using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AnrFgManager
{
    public static class Native
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public static void DarkTitle(IntPtr h, int caption, int text, int border)
        {
            try
            {
                int one = 1;
                if (DwmSetWindowAttribute(h, 20, ref one, 4) != 0)
                    DwmSetWindowAttribute(h, 19, ref one, 4);
                DwmSetWindowAttribute(h, 35, ref caption, 4);
                DwmSetWindowAttribute(h, 36, ref text, 4);
                DwmSetWindowAttribute(h, 34, ref border, 4);
            }
            catch { }
        }

        // cantos arredondados da janela (Windows 11)
        public static void RoundCorners(IntPtr h)
        {
            try { int v = 2; DwmSetWindowAttribute(h, 33, ref v, 4); } catch { }
        }

        public static void DarkScroll(IntPtr h)
        {
            try { SetWindowTheme(h, "DarkMode_Explorer", null); } catch { }
        }

        public static void Cue(IntPtr h, string text)
        {
            try { SendMessage(h, 0x1501, new IntPtr(1), text); } catch { }
        }

        // arrastar a janela segurando em qualquer area que chame isto
        public static void DragWindow(IntPtr h)
        {
            try { ReleaseCapture(); SendMessage(h, 0xA1, 2, 0); } catch { }
        }
    }

    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(14, 17, 23);
        public static readonly Color Panel = Color.FromArgb(22, 27, 36);
        public static readonly Color Card = Color.FromArgb(30, 36, 48);
        public static readonly Color Input = Color.FromArgb(17, 21, 29);
        public static readonly Color Border = Color.FromArgb(46, 55, 72);
        public static readonly Color Text = Color.FromArgb(230, 235, 243);
        public static readonly Color Muted = Color.FromArgb(138, 148, 166);
        public static readonly Color Accent = Color.FromArgb(45, 212, 191);
        public static readonly Color AccentHi = Color.FromArgb(94, 234, 212);
        public static readonly Color Ok = Color.FromArgb(45, 212, 191);
        public static readonly Color Err = Color.FromArgb(248, 113, 113);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);
        public static readonly Color Info = Color.FromArgb(96, 165, 250);
        public static readonly Color Select = Color.FromArgb(36, 50, 66);
        public static readonly Color ConsoleBg = Color.FromArgb(17, 21, 29);
        public static readonly Color ConsoleFg = Color.FromArgb(190, 205, 225);

        public static readonly Font Font = new Font("Segoe UI", 9f);
        public static readonly Font FontB = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font FontS = new Font("Segoe UI", 8f, FontStyle.Bold);
        public static readonly Font FontSm = new Font("Segoe UI", 8.25f);
        public static readonly Font FontName = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static readonly Font FontTitle = new Font("Segoe UI", 17f, FontStyle.Bold);
        public static readonly Font FontMono = new Font("Consolas", 9f);
        public static readonly Font FontMonoSm = new Font("Consolas", 8.5f);

        public static readonly SolidBrush BrAccent = new SolidBrush(Accent);
        public static readonly SolidBrush BrRowA = new SolidBrush(Panel);
        public static readonly SolidBrush BrSel = new SolidBrush(Select);
        public static readonly SolidBrush BrHover = new SolidBrush(Color.FromArgb(28, 35, 47));
        public static readonly SolidBrush BrIconPlate = new SolidBrush(Color.FromArgb(30, 36, 48));
        public static readonly SolidBrush BrInput = new SolidBrush(Input);
        public static readonly SolidBrush BrTip = new SolidBrush(Color.FromArgb(30, 37, 50));
        public static readonly SolidBrush BrPillOk = new SolidBrush(Color.FromArgb(14, 54, 52));
        public static readonly SolidBrush BrPillWarn = new SolidBrush(Color.FromArgb(66, 50, 14));

        public static readonly Pen PenBorder = new Pen(Border, 1f);
        public static readonly Pen PenRow = new Pen(Color.FromArgb(30, 37, 50), 1f);

        private static readonly Dictionary<string, Color> StoreColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Steam", Color.FromArgb(102, 192, 244) },
            { "Epic", Color.FromArgb(215, 220, 232) },
            { "GOG", Color.FromArgb(176, 130, 240) },
            { "Xbox", Color.FromArgb(90, 214, 110) },
            { "EA", Color.FromArgb(255, 112, 96) },
            { "Ubisoft", Color.FromArgb(96, 160, 255) },
            { "Pasta", Color.FromArgb(138, 148, 166) },
            { "Manual", Color.FromArgb(251, 191, 36) },
            { "Programa", Color.FromArgb(138, 148, 166) }
        };

        public static int ColorRef(Color c) { return c.R + 256 * c.G + 65536 * c.B; }

        public static Color GetStoreColor(string store)
        {
            Color c;
            if (!string.IsNullOrEmpty(store) && StoreColors.TryGetValue(store, out c)) return c;
            return Muted;
        }

        public static GraphicsPath RoundPath(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 2) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void SetRoundRegion(Control c, int rad)
        {
            if (c.Width <= 4 || c.Height <= 4) return;
            using (GraphicsPath gp = RoundPath(new Rectangle(0, 0, c.Width, c.Height), rad))
            {
                Region old = c.Region;
                c.Region = new Region(gp);
                if (old != null) old.Dispose();
            }
        }

        public static void SetDarkTitle(Form f)
        {
            f.HandleCreated += delegate(object s, EventArgs e)
            {
                Native.DarkTitle(((Form)s).Handle, ColorRef(Bg), ColorRef(Text), ColorRef(Border));
            };
        }

        public static void SetDarkScroll(Control ctl)
        {
            ctl.HandleCreated += delegate(object s, EventArgs e)
            {
                Native.DarkScroll(((Control)s).Handle);
            };
            if (ctl.IsHandleCreated) Native.DarkScroll(ctl.Handle);
        }

        public static void UpdateBtnColors(Button b)
        {
            RoundButton rb = b as RoundButton;
            if (rb != null)
            {
                string k = b.Tag as string;
                rb.Kind = string.IsNullOrEmpty(k) ? "ghost" : k;
                rb.Invalidate();
            }
        }

        public static Button NewButton(string text, int x, int y, int w, int h, string kind)
        {
            RoundButton b = new RoundButton();
            b.Kind = kind;
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.Tag = kind;
            b.Font = (kind == "primary" || kind == "secondary") ? FontB : Font;
            return b;
        }

        // botoes e campos sobre o banner precisam de fundo transparente
        public static void SetTransparent(Control c)
        {
            RoundButton rb = c as RoundButton;
            if (rb != null) { rb.Transparent = true; rb.BackColor = Color.Transparent; return; }
            RoundBox bx = c as RoundBox;
            if (bx != null) { bx.Transparent = true; bx.BackColor = Color.Transparent; }
        }

        public static Label NewLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            return l;
        }

        public static Label NewHint(string text, int x, int y)
        {
            Label l = NewLabel(text, x, y);
            l.ForeColor = Muted;
            return l;
        }

        public static RoundBox NewInput(int x, int y, int w, bool transparent)
        {
            RoundBox t = new RoundBox();
            t.Location = new Point(x, y);
            t.Width = w;
            if (transparent) SetTransparent(t);
            return t;
        }

        public static DarkCombo NewCombo(int x, int y, int w)
        {
            DarkCombo c = new DarkCombo();
            c.Location = new Point(x, y);
            c.Width = w;
            return c;
        }

        public static DarkCheck NewCheck(string text, int x, int y)
        {
            DarkCheck c = new DarkCheck();
            int tw = TextRenderer.MeasureText(text, Font).Width;
            c.Size = new Size(tw + 34, 24);
            c.Text = text;
            c.Location = new Point(x, y);
            c.ForeColor = Text;
            return c;
        }

        public static CardPanel NewCard(string title, int x, int y, int w, int h)
        {
            return new CardPanel(title, x, y, w, h);
        }

        public static Form NewDarkForm(string title, int w, int h)
        {
            Form f = new Form();
            f.Text = title;
            f.ClientSize = new Size(w, h);
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.Font = Font;
            f.BackColor = Bg;
            f.ForeColor = Text;
            SetDarkTitle(f);
            return f;
        }

        public static ToolTip CreateDarkTooltip()
        {
            ToolTip tip = new ToolTip();
            tip.OwnerDraw = true;
            tip.Popup += delegate(object s, PopupEventArgs e)
            {
                string t = tip.GetToolTip(e.AssociatedControl);
                Size sz = TextRenderer.MeasureText(t, Font, new Size(300, 0), TextFormatFlags.WordBreak);
                e.ToolTipSize = new Size(sz.Width + 20, sz.Height + 16);
            };
            tip.Draw += delegate(object s, DrawToolTipEventArgs e)
            {
                e.Graphics.FillRectangle(BrTip, e.Bounds);
                e.Graphics.DrawRectangle(PenBorder, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                Rectangle r = new Rectangle(10, 8, e.Bounds.Width - 20, e.Bounds.Height - 16);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, Font, r, Text, TextFormatFlags.WordBreak);
            };
            return tip;
        }

        public static bool ShowMsg(IWin32Window owner, string text, string title, bool yesNo)
        {
            Size sz = TextRenderer.MeasureText(text, Font, new Size(430, 0), TextFormatFlags.WordBreak);
            int h = Math.Max(40, sz.Height) + 108;
            using (Form f = NewDarkForm(title, 484, h))
            {
                Label l = new Label();
                l.Text = text;
                l.Location = new Point(24, 24);
                l.Size = new Size(436, sz.Height + 6);
                l.ForeColor = Text;
                f.Controls.Add(l);
                if (yesNo)
                {
                    Button b1 = NewButton("Sim", 278, h - 56, 88, 34, "primary");
                    b1.DialogResult = DialogResult.Yes;
                    Button b2 = NewButton("Não", 372, h - 56, 88, 34, "ghost");
                    b2.DialogResult = DialogResult.No;
                    f.Controls.AddRange(new Control[] { b1, b2 });
                    f.AcceptButton = b1;
                    f.CancelButton = b2;
                }
                else
                {
                    Button b1 = NewButton("OK", 372, h - 56, 88, 34, "primary");
                    b1.DialogResult = DialogResult.OK;
                    f.Controls.Add(b1);
                    f.AcceptButton = b1;
                    f.CancelButton = b1;
                }
                DialogResult r = (owner != null) ? f.ShowDialog(owner) : f.ShowDialog();
                return r == DialogResult.Yes || r == DialogResult.OK;
            }
        }

        public static string PickFolder(IWin32Window owner, string desc)
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = desc;
                if (d.ShowDialog(owner) == DialogResult.OK) return d.SelectedPath;
            }
            return null;
        }

        public static DarkListBox NewDarkList(int x, int y, int w, int h)
        {
            DarkListBox lb = new DarkListBox();
            lb.Location = new Point(x, y);
            lb.Size = new Size(w, h);
            return lb;
        }
    }

    // janela sem a barra do Windows: a borda de redimensionamento e feita aqui (Padding >= Grip)
    public class AppForm : Form
    {
        public int Grip = 7;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000;       // permite minimizar pela barra de tarefas
                cp.ClassStyle |= 0x20000;  // sombra
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84 && WindowState == FormWindowState.Normal)
            {
                long lp = m.LParam.ToInt64();
                int x = (short)(lp & 0xFFFF);
                int y = (short)((lp >> 16) & 0xFFFF);
                Point p = PointToClient(new Point(x, y));
                bool l = p.X <= Grip;
                bool r = p.X >= ClientSize.Width - Grip;
                bool t = p.Y <= Grip;
                bool b = p.Y >= ClientSize.Height - Grip;
                int res = 0;
                if (t && l) res = 13;
                else if (t && r) res = 14;
                else if (b && l) res = 16;
                else if (b && r) res = 17;
                else if (l) res = 10;
                else if (r) res = 11;
                else if (t) res = 12;
                else if (b) res = 15;
                if (res != 0) { m.Result = (IntPtr)res; return; }
            }
            base.WndProc(ref m);
        }
    }

    // botao arredondado (primary / secondary / ghost)
    public class RoundButton : Button
    {
        public string Kind = "ghost";
        public int Radius = 10;
        public bool Transparent;
        private bool hover;
        private bool down;

        public RoundButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            if (Transparent) base.OnPaintBackground(pe);
            else g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill; Color border; Color text;
            if (!Enabled)
            {
                fill = Color.FromArgb(24, 30, 41); border = Color.FromArgb(38, 46, 61); text = Color.FromArgb(96, 107, 128);
            }
            else if (Kind == "primary")
            {
                fill = down ? Color.FromArgb(34, 170, 152) : (hover ? Color.FromArgb(94, 234, 212) : Color.FromArgb(45, 212, 191));
                border = fill; text = Color.FromArgb(5, 26, 30);
            }
            else if (Kind == "secondary")
            {
                fill = down ? Color.FromArgb(214, 220, 232) : (hover ? Color.FromArgb(255, 255, 255) : Color.FromArgb(238, 241, 246));
                border = fill; text = Color.FromArgb(18, 24, 40);
            }
            else
            {
                fill = down ? Color.FromArgb(48, 60, 80) : (hover ? Color.FromArgb(40, 50, 68) : Color.FromArgb(30, 37, 50));
                border = Color.FromArgb(54, 64, 84); text = Color.FromArgb(230, 235, 243);
            }
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath gp = Theme.RoundPath(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) { g.FillPath(b, gp); }
                using (Pen p = new Pen(border)) { g.DrawPath(p, gp); }
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // campo de texto com bordas arredondadas
    public class RoundBox : Panel
    {
        public TextBox Inner;
        public Color Back = Color.FromArgb(17, 21, 29);
        public Color Line = Color.FromArgb(46, 55, 72);
        public Color FocusLine = Color.FromArgb(45, 212, 191);
        public bool Transparent;
        private bool focused;

        public RoundBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Inner = new TextBox();
            Inner.BorderStyle = BorderStyle.None;
            Inner.BackColor = Back;
            Inner.ForeColor = Color.FromArgb(230, 235, 243);
            Controls.Add(Inner);
            Inner.TextChanged += delegate(object s, EventArgs e) { OnTextChanged(e); };
            Inner.KeyPress += delegate(object s, KeyPressEventArgs e) { OnKeyPress(e); };
            Inner.DragEnter += delegate(object s, DragEventArgs e) { OnDragEnter(e); };
            Inner.DragDrop += delegate(object s, DragEventArgs e) { OnDragDrop(e); };
            Inner.GotFocus += delegate(object s, EventArgs e) { focused = true; Invalidate(); };
            Inner.LostFocus += delegate(object s, EventArgs e) { focused = false; Invalidate(); };
            Height = 34;
        }

        public override string Text
        {
            get { return Inner.Text; }
            set { Inner.Text = value; }
        }

        public bool ReadOnly
        {
            get { return Inner.ReadOnly; }
            set { Inner.ReadOnly = value; }
        }

        public int MaxLength
        {
            get { return Inner.MaxLength; }
            set { Inner.MaxLength = value; }
        }

        public override bool AllowDrop
        {
            get { return base.AllowDrop; }
            set { base.AllowDrop = value; Inner.AllowDrop = value; }
        }

        public void SetCue(string text)
        {
            if (Inner.IsHandleCreated) Native.Cue(Inner.Handle, text);
            else Inner.HandleCreated += delegate(object s, EventArgs e) { Native.Cue(Inner.Handle, text); };
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int h = Inner.PreferredHeight;
            Inner.SetBounds(12, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - 24), h);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            if (Transparent) base.OnPaintBackground(pe);
            else g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath gp = Theme.RoundPath(r, 10))
            {
                using (SolidBrush b = new SolidBrush(Back)) { g.FillPath(b, gp); }
                using (Pen p = new Pen(focused ? FocusLine : Line)) { g.DrawPath(p, gp); }
            }
        }
    }

    // botoes minimizar / ampliar / fechar desenhados no cabecalho
    public class CaptionButton : Control
    {
        public string Kind = "min";
        private bool hover;

        public CaptionButton(string kind)
        {
            Kind = kind;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(42, 30);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover)
            {
                Color col = (Kind == "close") ? Color.FromArgb(232, 72, 85) : Color.FromArgb(48, 255, 255, 255);
                using (SolidBrush br = new SolidBrush(col))
                using (GraphicsPath p = Theme.RoundPath(new Rectangle(3, 2, Width - 7, Height - 5), 9))
                {
                    g.FillPath(br, p);
                }
            }
            using (Pen pen = new Pen(Theme.Text, 1.4f))
            {
                int cx = Width / 2;
                int cy = Height / 2;
                if (Kind == "min")
                {
                    g.DrawLine(pen, cx - 5, cy + 3, cx + 5, cy + 3);
                }
                else if (Kind == "max")
                {
                    Form f = FindForm();
                    if (f != null && f.WindowState == FormWindowState.Maximized)
                    {
                        g.DrawRectangle(pen, cx - 3, cy - 5, 8, 8);
                        g.DrawRectangle(pen, cx - 5, cy - 3, 8, 8);
                    }
                    else g.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                }
                else
                {
                    g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                }
            }
        }
    }

    public class CardPanel : Panel
    {
        private readonly string title;

        public CardPanel(string title, int x, int y, int w, int h)
        {
            this.title = (title ?? "").ToUpperInvariant();
            Location = new Point(x, y);
            Size = new Size(w, h);
            BackColor = Theme.Card;
            DoubleBuffered = true;
            Theme.SetRoundRegion(this, 12);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Theme.SetRoundRegion(this, 12);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = Theme.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            {
                g.DrawPath(Theme.PenBorder, gp);
            }
            if (title.Length > 0)
                TextRenderer.DrawText(g, title, Theme.FontS, new Point(18, 11), Theme.Muted);
        }
    }

    public class DoubleBufferedListView : ListView
    {
        public DoubleBufferedListView()
        {
            DoubleBuffered = true;
        }
    }

    public class BufPanel : Panel
    {
        public BufPanel()
        {
            DoubleBuffered = true;
        }
    }

    public class DarkListBox : ListBox
    {
        public DarkListBox()
        {
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            BorderStyle = BorderStyle.FixedSingle;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 22;
            HorizontalScrollbar = true;
            Theme.SetDarkScroll(this);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool sel = (e.State & DrawItemState.Selected) != 0;
            e.Graphics.FillRectangle(sel ? Theme.BrSel : Theme.BrInput, e.Bounds);
            if (e.Index >= 0 && e.Index < Items.Count)
            {
                Rectangle r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, Convert.ToString(Items[e.Index]), Theme.Font, r, Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    public class DarkColors : ProfessionalColorTable
    {
        private static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        public override Color MenuItemSelected { get { return C(38, 52, 70); } }
        public override Color MenuItemSelectedGradientBegin { get { return C(38, 52, 70); } }
        public override Color MenuItemSelectedGradientEnd { get { return C(38, 52, 70); } }
        public override Color MenuItemBorder { get { return C(45, 212, 191); } }
        public override Color MenuBorder { get { return C(46, 55, 72); } }
        public override Color ToolStripDropDownBackground { get { return C(24, 30, 40); } }
        public override Color ImageMarginGradientBegin { get { return C(24, 30, 40); } }
        public override Color ImageMarginGradientMiddle { get { return C(24, 30, 40); } }
        public override Color ImageMarginGradientEnd { get { return C(24, 30, 40); } }
        public override Color SeparatorDark { get { return C(46, 55, 72); } }
        public override Color SeparatorLight { get { return C(46, 55, 72); } }
    }

    // lista suspensa com cantos arredondados e cores do programa
    public class DarkCombo : ComboBox
    {
        public Color Back = Color.FromArgb(17, 21, 29);
        public Color Fore = Color.FromArgb(230, 235, 243);
        public Color Line = Color.FromArgb(46, 55, 72);
        public Color Sel = Color.FromArgb(38, 52, 70);

        public DarkCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = 22;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool sel = !edit && (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(sel ? Sel : Back)) { g.FillRectangle(b, e.Bounds); }
            if (e.Index >= 0 && e.Index < Items.Count)
            {
                string t = GetItemText(Items[e.Index]);
                Rectangle r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
                TextRenderer.DrawText(g, t, Font, r, Fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0xF)
            {
                try
                {
                    using (Graphics g = Graphics.FromHwnd(Handle))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        Color pc = (Parent != null) ? Parent.BackColor : SystemColors.Control;
                        Rectangle full = new Rectangle(0, 0, Width, Height);
                        using (GraphicsPath rp = Theme.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), 9))
                        {
                            // cantos fora do arredondado: cor do fundo de quem esta atras
                            using (Region outside = new Region(full))
                            {
                                outside.Exclude(rp);
                                using (SolidBrush pb = new SolidBrush(pc)) { g.FillRegion(pb, outside); }
                            }
                            // interior inteiro: cobre a moldura branca que o Windows desenha por dentro
                            using (SolidBrush b = new SolidBrush(Back)) { g.FillPath(b, rp); }

                            string t = (SelectedIndex >= 0 && SelectedIndex < Items.Count) ? GetItemText(Items[SelectedIndex]) : "";
                            Rectangle tr = new Rectangle(10, 0, Math.Max(1, Width - 10 - 30), Height);
                            TextRenderer.DrawText(g, t, Font, tr, Enabled ? Fore : Color.Gray,
                                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                            int cx = Width - 14;
                            int cy = Height / 2;
                            using (Pen ap = new Pen(Fore, 1.6f))
                            {
                                g.DrawLines(ap, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
                            }
                            using (Pen bp = new Pen(Line)) { g.DrawPath(bp, rp); }
                        }
                    }
                }
                catch { }
            }
        }
    }

    public class DarkCheck : CheckBox
    {
        public Color Box = Color.FromArgb(17, 21, 29);
        public Color Line = Color.FromArgb(62, 74, 96);
        public Color Accent = Color.FromArgb(45, 212, 191);

        public DarkCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color bg = (Parent != null) ? Parent.BackColor : BackColor;
            g.Clear(bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(1, (Height - 16) / 2, 15, 15);
            using (GraphicsPath gp = Theme.RoundPath(r, 5))
            {
                using (SolidBrush b = new SolidBrush(Checked ? Accent : Box)) { g.FillPath(b, gp); }
                using (Pen p = new Pen(Checked ? Accent : Line)) { g.DrawPath(p, gp); }
            }
            if (Checked)
            {
                using (Pen ck = new Pen(Color.FromArgb(5, 26, 30), 2f))
                {
                    g.DrawLines(ck, new Point[] { new Point(5, r.Y + 7), new Point(7, r.Y + 10), new Point(12, r.Y + 4) });
                }
            }
            Rectangle tr = new Rectangle(24, 0, Math.Max(1, Width - 24), Height);
            TextRenderer.DrawText(g, Text, Font, tr, Enabled ? ForeColor : Color.Gray,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }

    // campo que captura UMA tecla (clique e pressione). Guarda o codigo virtual do Windows.
    public class KeyBox : Control
    {
        public int Vk = -2;   // -2 = padrao do OptiScaler (auto), -1 = nenhuma, >= 0 = codigo da tecla
        private bool capturing;
        public event EventHandler ValueChanged;

        public KeyBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(170, 34);
        }

        public void FromIni(string v)
        {
            Vk = -2;
            if (string.IsNullOrEmpty(v)) { Invalidate(); return; }
            v = v.Trim();
            int n;
            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                try { Vk = Convert.ToInt32(v.Substring(2), 16); } catch { Vk = -2; }
            }
            else if (int.TryParse(v, out n)) Vk = n;
            Invalidate();
        }

        public string ToIniValue()
        {
            if (Vk == -2) return "auto";
            if (Vk < 0) return "-1";
            return "0x" + Vk.ToString("X2");
        }

        public static string NameOf(int vk)
        {
            switch (vk)
            {
                case 0x10: return "Shift";
                case 0x11: return "Ctrl";
                case 0x12: return "Alt";
                case 0x13: return "Pause";
                case 0x14: return "Caps Lock";
                case 0x21: return "Page Up";
                case 0x22: return "Page Down";
                case 0x23: return "End";
                case 0x24: return "Home";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x91: return "Scroll Lock";
                case 0xBA: return ";";
                case 0xBB: return "=";
                case 0xBC: return ",";
                case 0xBD: return "-";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xC0: return "` (acento grave)";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";
            }
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return "Num " + (vk - 0x60);
            return ((Keys)vk).ToString();
        }

        private string DisplayText()
        {
            if (capturing) return "Pressione a tecla...";
            if (Vk == -2) return "Insert  (padrão)";
            if (Vk < 0) return "Nenhuma";
            return NameOf(Vk) + "  (0x" + Vk.ToString("X2") + ")";
        }

        public void SetValue(int vk)
        {
            Vk = vk;
            capturing = false;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            capturing = true;
            Focus();
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            capturing = false;
            Invalidate();
            base.OnLostFocus(e);
        }

        // pega qualquer tecla (inclusive Tab, setas, Esc e Shift/Ctrl/Alt sozinhos) enquanto captura
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (capturing)
            {
                int code = (int)(keyData & Keys.KeyCode);
                if (code != 0)
                {
                    SetValue(code);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath gp = Theme.RoundPath(r, 10))
            {
                g.FillPath(Theme.BrInput, gp);
                using (Pen p = new Pen(capturing ? Theme.Accent : Theme.Border)) { g.DrawPath(p, gp); }
            }
            TextRenderer.DrawText(g, DisplayText(), Theme.Font, new Rectangle(12, 0, Width - 20, Height),
                capturing ? Theme.Accent : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
