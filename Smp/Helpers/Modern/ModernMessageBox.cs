using System;
using System.Drawing;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class ModernMessageBox
{
    public static DialogResult Show(string message, string title = "سیستم مدیریت", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
    {
        // اطمینان از اجرا در ترد اصلی رابط کاربری
        if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired)
        {
            return (DialogResult)Application.OpenForms[0].Invoke(new Func<DialogResult>(() => Show(message, title, buttons, icon)));
        }

        const int FormWidth = 500;
        const int SidePad = 24;
        const int IconColumn = 64;
        const int ButtonBarHeight = 72;
        const int MaxMessageHeight = 360;

        Color accent = icon == MessageBoxIcon.Error ? ModernTheme.Danger
                     : icon == MessageBoxIcon.Warning ? ModernTheme.Warning
                     : ModernTheme.Primary;
        Color buttonColor = icon == MessageBoxIcon.Error ? ModernTheme.Danger
                          : icon == MessageBoxIcon.Warning ? ModernTheme.Brand
                          : ModernTheme.Primary;

        Font messageFont = ModernTheme.GetFont(11f);
        int textWidth = FormWidth - 2 * SidePad - IconColumn;
        int measured = TextRenderer.MeasureText(message ?? string.Empty, messageFont, new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix).Height + 6;
        bool needsScroll = measured > MaxMessageHeight;
        int contentHeight = Math.Max(52, Math.Min(measured, MaxMessageHeight));

        using var form = new Form
        {
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            Text = title,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true,
            ShowInTaskbar = false,
            KeyPreview = true,
            ClientSize = new Size(FormWidth, 4 + 22 + contentHeight + 14 + ButtonBarHeight)
        };

        // ── نوار رنگی بالا ──
        var strip = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accent };

        // ── نوار دکمه‌ها (اولین دکمه در راست) ──
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = ButtonBarHeight,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(SidePad - 4, 15, SidePad - 4, 10),
            BackColor = ModernTheme.Background
        };
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var btnPrimary = ModernButton.Create(
            (buttons == MessageBoxButtons.OKCancel || buttons == MessageBoxButtons.OK) ? "تایید" : "بله",
            buttonColor, 110, 38);
        btnPrimary.DialogResult = buttons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Yes;
        bar.Controls.Add(btnPrimary);

        ModernButton? btnSecondary = null;
        if (buttons == MessageBoxButtons.OKCancel || buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel)
        {
            btnSecondary = ModernButton.Cancel(buttons == MessageBoxButtons.OKCancel ? "انصراف" : "خیر", 110, 38);
            btnSecondary.DialogResult = DialogResult.Cancel;
            bar.Controls.Add(btnSecondary);
        }

        // ── بدنه: آیکون (راست) + متن ──
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(SidePad, 22, SidePad, 14), BackColor = Color.White };

        var badge = new MessageBadge(icon, accent) { Dock = DockStyle.Right, Width = IconColumn };

        Control messageControl;
        if (needsScroll)
        {
            // پیام خیلی بلند: متن قابل اسکرول و قابل انتخاب
            messageControl = new TextBox
            {
                Text = message,
                Font = messageFont,
                ForeColor = ModernTheme.Text,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ScrollBars = ScrollBars.Vertical,
                RightToLeft = RightToLeft.Yes,
                TabStop = false
            };
        }
        else
        {
            messageControl = new Label
            {
                Text = message,
                Font = messageFont,
                ForeColor = ModernTheme.Text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,   // فرم Mirror شده: متن سمت راست، کنار آیکون
                RightToLeft = RightToLeft.Yes,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };
        }

        body.Controls.Add(messageControl);   // Fill (اول)
        body.Controls.Add(badge);            // Right

        form.Controls.Add(body);
        form.Controls.Add(bar);
        form.Controls.Add(strip);

        form.AcceptButton = btnPrimary;
        form.CancelButton = btnSecondary ?? (IButtonControl)btnPrimary;

        // Ctrl+C: کپی کردن متن پیام
        form.KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C && !needsScroll)
            {
                try { Clipboard.SetText(message ?? string.Empty); } catch { }
                e.Handled = true;
            }
        };

        return form.ShowDialog();
    }

    /// <summary>نشان گرد رنگی کنار پیام (به جای آیکون‌های قدیمی ویندوز)</summary>
    private sealed class MessageBadge : Control
    {
        private readonly string _glyph;
        private readonly Color _accent;

        public MessageBadge(MessageBoxIcon icon, Color accent)
        {
            _accent = accent;
            _glyph = icon == MessageBoxIcon.Error ? "✕"
                   : icon == MessageBoxIcon.Warning ? "!"
                   : icon == MessageBoxIcon.Question ? "؟"
                   : "i";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            ModernPaint.EnableSmooth(g);
            g.Clear(BackColor);

            const int d = 44;
            var circle = new Rectangle((Width - d) / 2, 2, d, d);
            using (var fill = new SolidBrush(ModernPaint.Tint(_accent, 14))) g.FillEllipse(fill, circle);
            using (var pen = new Pen(_accent, 2f)) g.DrawEllipse(pen, circle);

            TextRenderer.DrawText(g, _glyph, ModernTheme.GetFont(17f, FontStyle.Bold), circle, _accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    public static void ShowError(string message, string title = "خطا")
    {
        Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static void ShowWarning(string message, string title = "هشدار")
    {
        Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void ShowInfo(string message, string title = "اطلاع")
    {
        Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void ShowSuccess(string message, string title = "موفقیت")
    {
        Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool AskConfirm(string message, string title = "تایید")
    {
        return Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    public static bool ShowQuestion(string message, string title = "سوال")
    {
        return Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
