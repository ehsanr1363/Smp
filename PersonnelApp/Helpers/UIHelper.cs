using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class UIHelper
    {
        // 🎨 پالت رنگی عمومی
        public static readonly Color WarmBackground = Color.FromArgb(253, 251, 247);
        public static readonly Color WarmPanelBackground = Color.FromArgb(253, 250, 245);
        public static readonly Color WarmInputBackground = Color.FromArgb(255, 254, 252);
        public static readonly Color WarmBorder = Color.FromArgb(235, 225, 215);
        public static readonly Color WarmText = Color.FromArgb(60, 50, 45);

        public static readonly Color WarmPrimary = Color.FromArgb(234, 124, 105);
        public static readonly Color WarmSecondary = Color.FromArgb(107, 144, 128);
        public static readonly Color WarmDanger = Color.FromArgb(220, 80, 80);

        public static readonly Color SectionBlue = Color.FromArgb(59, 130, 246);
        public static readonly Color SectionOrange = Color.FromArgb(249, 115, 22);
        public static readonly Color SectionPurple = Color.FromArgb(168, 85, 247);
        public static readonly Color SectionTeal = Color.FromArgb(20, 184, 166);
        public static readonly Color SectionGray = Color.FromArgb(100, 116, 139);
        public static readonly Color SectionDark = Color.FromArgb(30, 41, 59);

        public static readonly Color ActionGreen = Color.FromArgb(34, 197, 94);
        public static readonly Color ActionRed = Color.FromArgb(239, 68, 68);
        public static readonly Color ActionAmber = Color.FromArgb(245, 158, 11);

        public static readonly Color WarmSuccessBackground = Color.FromArgb(220, 252, 231);
        public static readonly Color WarmErrorBackground = Color.FromArgb(254, 226, 226);
        public static readonly Color WarmErrorText = Color.FromArgb(153, 27, 27);
        public static readonly Color WarmWarningBackground = Color.FromArgb(255, 243, 205);
        public static readonly Color WarmWarningText = Color.FromArgb(133, 100, 4);

        // 🎨 پالت رنگ سایدبار
        public static readonly Color SidebarBackColor = Color.FromArgb(240, 244, 248);
        public static readonly Color SidebarHeaderBackColor = Color.FromArgb(241, 245, 249);
        public static readonly Color SidebarHeaderHoverColor = Color.FromArgb(226, 232, 240);
        public static readonly Color SidebarHeaderTextColor = Color.FromArgb(51, 65, 85);
        public static readonly Color SidebarItemTextColor = Color.FromArgb(30, 41, 59);
        public static readonly Color SidebarItemHoverColor = Color.FromArgb(248, 250, 252);
        public static readonly Color SidebarDividerColor = Color.FromArgb(220, 225, 230);

        private static readonly ConcurrentDictionary<(float Size, FontStyle Style), Font> _fontCache = new();

        public static Font PersianFont(float size, FontStyle style = FontStyle.Regular)
        {
            return _fontCache.GetOrAdd((size, style), key =>
            {
                foreach (var name in new[] { "Vazirmatn", "IRANSans", "B Nazanin", "Tahoma" })
                {
                    try
                    {
                        var f = new Font(name, key.Size, key.Style);
                        if (f.Name == name) return f;
                        f.Dispose();
                    }
                    catch { }
                }
                return new Font("Tahoma", key.Size, key.Style);
            });
        }

        public static void SetupForm(Form form, string title, Size size)
        {
            form.Text = title;
            form.Size = size;
            form.StartPosition = FormStartPosition.CenterParent;
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
            form.Font = PersianFont(12f);
            form.BackColor = WarmBackground;
            form.Padding = new Padding(15);
            form.MaximizeBox = false;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            EnterNavigationHelper.EnableForForm(form);
        }

        // ==========================================================
        // 🔘 متدهای آکاردئون (نسخه جدید با فونت بزرگ)
        // ==========================================================

        /// <summary>
        /// هدر رنگی بخش‌های سایدبار. با کلیک روی آن، بخش زیرش باز/بسته می‌شود.
        /// </summary>
        public static Panel CreateAccordionHeader(string title, string icon, Color accentColor, bool isExpanded, out Label chevron)
        {
            var header = new Panel
            {
                Width = 240,
                Height = 42,
                BackColor = accentColor,
                Margin = new Padding(0, 10, 0, 0),
                Cursor = Cursors.Hand
            };

            var colorStrip = new Panel
            {
                Width = 4,
                Dock = DockStyle.Right,
                BackColor = DarkenColor(accentColor, 30)
            };

            chevron = new Label
            {
                Text = isExpanded ? "▼" : "▶",
                Width = 30,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = PersianFont(9f, FontStyle.Bold),
                ForeColor = Color.White
            };

            var lblIcon = new Label
            {
                Text = icon,
                Width = 32,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = PersianFont(13f),
                ForeColor = Color.White
            };

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = PersianFont(11f, FontStyle.Bold),
                ForeColor = Color.White,
                Padding = new Padding(0, 0, 5, 0)
            };

            var hoverColor = DarkenColor(accentColor, 25);
            EventHandler onEnter = (s, e) => header.BackColor = hoverColor;
            EventHandler onLeave = (s, e) => header.BackColor = accentColor;

            header.MouseEnter += onEnter; header.MouseLeave += onLeave;
            chevron.MouseEnter += onEnter; chevron.MouseLeave += onLeave;
            lblIcon.MouseEnter += onEnter; lblIcon.MouseLeave += onLeave;
            lblTitle.MouseEnter += onEnter; lblTitle.MouseLeave += onLeave;

            header.Controls.Add(lblTitle);
            header.Controls.Add(chevron);
            header.Controls.Add(lblIcon);
            header.Controls.Add(colorStrip);

            return header;
        }

        /// <summary>
        /// پنل سفید زیرمجموعه که دکمه‌های آکاردئون را نگه می‌دارد.
        /// </summary>
        public static FlowLayoutPanel CreateAccordionContainer(bool isExpanded)
        {
            return new FlowLayoutPanel
            {
                Width = 240,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = isExpanded,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 5)
            };
        }

        /// <summary>
        /// دکمه‌ی زیرمجموعه با فونت بزرگ، بولد و رنگ تیره برای خوانایی بالا.
        /// </summary>
        public static Button CreateAccordionButton(string key, string icon, string text, Color accentColor)
        {
            var hoverTint = GetTint(accentColor, 12);
            var pressTint = GetTint(accentColor, 20);

            var btn = new Button
            {
                Text = $"    {icon}  {text}",
                Width = 240,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = SidebarItemTextColor,
                Font = PersianFont(11f, FontStyle.Bold), // فونت بزرگ و بولد
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 1, 0, 1),
                Tag = key,
                Padding = new Padding(10, 0, 5, 0)
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.BorderColor = Color.FromArgb(240, 240, 240);
            btn.FlatAppearance.MouseOverBackColor = hoverTint;
            btn.FlatAppearance.MouseDownBackColor = pressTint;

            bool isHovering = false;
            btn.MouseEnter += (s, e) => { isHovering = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { isHovering = false; btn.Invalidate(); };

            btn.Paint += (s, e) => {
                if (isHovering)
                {
                    using var brush = new SolidBrush(accentColor);
                    e.Graphics.FillRectangle(brush, btn.Width - 4, 0, 4, btn.Height);
                }
            };

            return btn;
        }

        // ==========================================================
        // 🧩 متدهای کمکی
        // ==========================================================

        public static Button CreateSidebarButton(string icon, string text, Color bgColor)
        {
            var btn = new Button
            {
                Text = $"{icon}  {text}",
                Font = PersianFont(10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = bgColor,
                FlatStyle = FlatStyle.Flat,
                Height = 35,
                Width = 240,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 2)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = DarkenColor(bgColor, 20);
            btn.FlatAppearance.MouseDownBackColor = DarkenColor(bgColor, 40);
            return btn;
        }

        public static Button CreateSidebarMenuButton(string icon, string text, Color iconColor, string key)
        {
            return CreateAccordionButton(key, icon, text, iconColor);
        }

        public static Panel CreateWarmPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), BackColor = WarmPanelBackground };
            pnl.Paint += (s, e) => {
                ControlPaint.DrawBorder(e.Graphics, pnl.ClientRectangle,
                    WarmBorder, 1, ButtonBorderStyle.Solid, WarmBorder, 1, ButtonBorderStyle.Solid,
                    WarmBorder, 1, ButtonBorderStyle.Solid, WarmBorder, 1, ButtonBorderStyle.Solid);
            };
            return pnl;
        }

        public static Button CreateButton(string text, Color bgColor, int width = 130, int height = 40)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                BackColor = bgColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = PersianFont(11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(5)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = DarkenColor(bgColor, 20);
            return btn;
        }

        public static void AddField(TableLayoutPanel tlp, string labelText, Control control, int row, int labelCol = 0, int controlCol = 1, int controlSpan = 1)
        {
            var lbl = new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = PersianFont(12f), ForeColor = WarmText };
            control.Dock = DockStyle.Fill; control.Font = PersianFont(12f); control.Margin = new Padding(5, 8, 5, 5);
            if (control is TextBox tb) { tb.RightToLeft = RightToLeft.Yes; tb.TextAlign = HorizontalAlignment.Center; tb.BackColor = WarmInputBackground; tb.BorderStyle = BorderStyle.FixedSingle; }
            if (control is NumericUpDown num) { num.TextAlign = HorizontalAlignment.Center; num.BackColor = WarmInputBackground; num.BorderStyle = BorderStyle.FixedSingle; }
            if (control is ComboBox cb) { cb.RightToLeft = RightToLeft.Yes; cb.BackColor = WarmInputBackground; }
            tlp.Controls.Add(lbl, labelCol, row); tlp.Controls.Add(control, controlCol, row);
            if (controlSpan > 1) tlp.SetColumnSpan(control, controlSpan);
        }

        public static void StyleToggle(Control toggle) { if (toggle == null) return; toggle.Font = PersianFont(12f); toggle.ForeColor = WarmText; }
        public static void StyleLabel(Label lbl) { if (lbl == null) return; lbl.Font = PersianFont(12f); lbl.ForeColor = WarmText; }

        public static Color DarkenColor(Color color, int amount)
        {
            return Color.FromArgb(Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount));
        }

        public static Color GetTint(Color color, int intensityPercent)
        {
            float factor = Math.Max(0, Math.Min(100, intensityPercent)) / 100f;
            return Color.FromArgb((int)(255 - (255 - color.R) * factor), (int)(255 - (255 - color.G) * factor), (int)(255 - (255 - color.B) * factor));
        }

        public static void ShowError(string message, string title = "خطا") => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        public static void ShowSuccess(string message, string title = "موفق") => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        public static void ShowWarning(string message, string title = "توجه") => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        public static bool AskConfirm(string message, string title = "تایید") => MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}