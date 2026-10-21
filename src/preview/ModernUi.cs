using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace APKRunner
{
    internal static class FlightTheme
    {
        public static readonly Color Background = Color.FromArgb(13, 23, 35);
        public static readonly Color Surface = Color.FromArgb(19, 37, 53);
        public static readonly Color Raised = Color.FromArgb(29, 53, 72);
        public static readonly Color Line = Color.FromArgb(54, 85, 108);
        public static readonly Color Text = Color.FromArgb(232, 243, 249);
        public static readonly Color Muted = Color.FromArgb(165, 192, 211);
        public static readonly Color Amber = Color.FromArgb(255, 185, 100);
        public static readonly Color Cyan = Color.FromArgb(102, 216, 255);
        public static readonly Color Blue = Color.FromArgb(40, 112, 170);
        public static readonly Color Green = Color.FromArgb(122, 224, 182);
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static Font Typeface(string family, float size)
        {
            string key = family + ":" + size.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lock (fonts) {
                Font font;
                if (!fonts.TryGetValue(key, out font)) { font = new Font(family, size, FontStyle.Regular); fonts.Add(key, font); }
                return font;
            }
        }
        public static Font Body(float size) { return Typeface("Segoe UI", size); }
        public static Font Strong(float size) { return Typeface("Segoe UI Semibold", size); }
        public static Font Mono(float size) { return Typeface("Consolas", size); }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
        public static void DarkTitle(Form form)
        {
            try { int enabled = 1; DwmSetWindowAttribute(form.Handle, 20, ref enabled, 4); } catch { }
        }

        public static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            float diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Label Label(string text, Color color, Font font)
        {
            return new Label { Text = text, ForeColor = color, BackColor = Color.Transparent, Font = font,
                AutoSize = true, Margin = new Padding(0), TextAlign = ContentAlignment.MiddleLeft };
        }

        public static void StyleList(ListView list)
        {
            list.BackColor = Surface;
            list.ForeColor = Text;
            list.BorderStyle = BorderStyle.None;
            list.Font = Body(10);
            list.OwnerDraw = true;
            list.HideSelection = false;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            var height = new ImageList { ImageSize = new Size(1, 33) };
            list.SmallImageList = height;
            list.Disposed += delegate { height.Dispose(); };
            list.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e) {
                using (var fill = new SolidBrush(Raised)) e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text.ToUpperInvariant(), Strong(8),
                    new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height), Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { };
            list.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e) {
                Color fill = e.Item.Selected ? Raised : (e.ItemIndex % 2 == 0 ? Surface : Color.FromArgb(16, 31, 46));
                using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, e.Bounds);
                Color color = e.ColumnIndex == 1 ? e.Item.ForeColor : (e.ColumnIndex == 0 ? Text : Muted);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.ColumnIndex == 1 ? Strong(9) : Body(9),
                    new Rectangle(e.Bounds.X + 14, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 20), e.Bounds.Height), color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (e.Item.Focused && list.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, Cyan, fill);
            };
            Action resize = delegate {
                if (list.Columns.Count < 3) return;
                list.Columns[0].Width = 190;
                list.Columns[1].Width = 110;
                list.Columns[2].Width = Math.Max(180, list.ClientSize.Width - 300);
            };
            list.Resize += delegate { resize(); };
            resize();
        }

        public static bool ChoosePerformance(IWin32Window owner, bool smoothingAvailable = false)
        {
            using (var form = new Form { Text = "APK Runner / Runtime profile", ClientSize = new Size(590, 280),
                BackColor = Background, ForeColor = Text, Font = Body(10), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false })
            {
                Branding.Apply(form);
                var title = Label("VÆLG YDEEVNEPROFIL", Cyan, Strong(18)); title.Location = new Point(28, 28);
                var explanation = Label(smoothingAvailable ? "Telefon: Normal har 30 FPS-grænse og en lille videobuffer.\nHøj: op til 60 FPS, flere CPU-kerner og mere Android-RAM.\nFuld opløsning. Første kolde opstart kan tage tid.\nÆldre 32-bit/x86-apps bruger den hidtidige videotilstand." : "Normal passer til de fleste apps. Høj ydeevne giver Android\nflere CPU-kerner og mere RAM, når computeren har plads.", Muted, Body(11)); explanation.Location = new Point(30, 82);
                var normal = new FlightButton { Text = smoothingAvailable ? "Normal / 30 FPS" : "Normal", Primary = true, Bounds = new Rectangle(30, 174, 245, 48), DialogResult = DialogResult.No };
                var high = new FlightButton { Text = smoothingAvailable ? "Høj ydeevne / op til 60 FPS" : "Høj ydeevne", Bounds = new Rectangle(290, 174, 270, 48), DialogResult = DialogResult.Yes };
                form.Controls.AddRange(new Control[] { title, explanation, normal, high });
                form.AcceptButton = normal; form.CancelButton = normal;
                form.Shown += delegate { DarkTitle(form); };
                return form.ShowDialog(owner) == DialogResult.Yes;
            }
        }
    }

    internal sealed class FlightButton : Button
    {
        bool hover;
        bool entered;
        public bool Primary;
        public FlightButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = FlightTheme.Strong(10);
            Cursor = Cursors.Hand;
            AutoSize = false;
            Height = 42;
            Margin = new Padding(0, 4, 10, 4);
            BackColor = FlightTheme.Background;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnter(EventArgs e) { entered = true; Invalidate(); base.OnEnter(e); }
        protected override void OnLeave(EventArgs e) { entered = false; Invalidate(); base.OnLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(FlightTheme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary && Enabled ? FlightTheme.Blue : (hover && Enabled ? FlightTheme.Raised : FlightTheme.Surface);
            bool focused = Focused || entered;
            Color border = focused || hover ? FlightTheme.Cyan : FlightTheme.Line;
            Color text = Enabled ? FlightTheme.Text : Color.FromArgb(135, 159, 178);
            using (var path = FlightTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, focused ? 2 : 1))
            {
                e.Graphics.FillPath(brush, path);
                if (!Primary || focused) e.Graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -7, -7), text, fill);
        }
    }

    internal sealed class FlightCombo : ComboBox
    {
        public FlightCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            BackColor = FlightTheme.Raised;
            ForeColor = FlightTheme.Text;
            Font = FlightTheme.Body(10);
            ItemHeight = 25;
            AccessibleName = "Appvinduets startorientering";
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            using (var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? FlightTheme.Line : FlightTheme.Raised)) e.Graphics.FillRectangle(fill, e.Bounds);
            if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
                new Rectangle(e.Bounds.X + 9, e.Bounds.Y, e.Bounds.Width - 15, e.Bounds.Height), FlightTheme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
    }

    internal sealed class FlightRail : Panel
    {
        public FlightRail() { DoubleBuffered = true; Dock = DockStyle.Fill; BackColor = FlightTheme.Background; TabStop = false; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var frame = new LinearGradientBrush(new Rectangle(0, 0, Width, Math.Max(1, Height)),
                Color.FromArgb(186, 201, 212), Color.FromArgb(75, 98, 119), LinearGradientMode.Horizontal))
                e.Graphics.FillRectangle(frame, 0, 0, 14, Height);
            using (var light = new Pen(FlightTheme.Cyan, 2)) e.Graphics.DrawLine(light, 18, 30, 18, Height - 30);
            using (var edge = new Pen(Color.FromArgb(218, 229, 236), 1)) e.Graphics.DrawLine(edge, 1, 0, 1, Height);
        }
    }

    internal sealed class FlightHeader : Panel
    {
        public FlightHeader()
        {
            Dock = DockStyle.Fill; DoubleBuffered = true; BackColor = FlightTheme.Background;
            var eyebrow = FlightTheme.Label("A P P L I C A T I O N   C O N T R O L", FlightTheme.Cyan, FlightTheme.Strong(8));
            eyebrow.Location = new Point(103, 4);
            var title = FlightTheme.Label("APK RUNNER", FlightTheme.Text, FlightTheme.Body(27)); title.Location = new Point(100, 26);
            var version = FlightTheme.Label("DESKTOP CONSOLE\nV" + AppUpdates.DisplayVersion, FlightTheme.Muted, FlightTheme.Mono(9));
            version.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            version.TextAlign = ContentAlignment.TopRight;
            Controls.AddRange(new Control[] { eyebrow, title, version });
            Resize += delegate { version.Location = new Point(Width - version.PreferredWidth, 24); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(Branding.Logo, new Rectangle(0, 0, 84, 84));
            using (var line = new Pen(FlightTheme.Line, 1)) e.Graphics.DrawLine(line, 0, Height - 12, Width, Height - 12);
            using (var light = new Pen(FlightTheme.Cyan, 2)) e.Graphics.DrawLine(light, 0, Height - 12, Math.Min(94, Width), Height - 12);
        }
    }
}
