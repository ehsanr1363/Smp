using Smp.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>صفحه‌ی خوش‌آمد: کارت خوش‌آمد + میانبرهای رنگی دسته‌بندی‌شده (فقط بخش‌هایی که کاربر به آن‌ها دسترسی دارد)</summary>
public class MainWelcomePanel : Panel
{
    public event Action<string>? ShortcutClicked;

    private sealed record Tile(string Key, string Icon, string Text);
    private sealed record Section(string Title, Color Color, Tile[] Tiles);

    private const int TileH = 60, Gap = 10, Pad = 16;
    private const int HdrH = 28, HdrGap = 6, SecGap = 12, ColGap = 28, MaxTileW = 280;
    private static readonly int[] HeroHeights = { 128, 104 };
    private static readonly int[] TileHeights = { 60, 56, 52, 48 };
    private static readonly int[] MinTileWidths = { 200, 175 };

    private readonly HeroCard _hero;
    private readonly List<(Label Header, List<ShortcutTile> Tiles)> _sections = new();

    public MainWelcomePanel(string company, string appTitle, string? userName)
    {
        Dock = DockStyle.Fill;
        BackColor = ModernTheme.Background;
        AutoScroll = true;
        DoubleBuffered = true;

        _hero = new HeroCard(company, appTitle, userName) { Height = 128 };
        Controls.Add(_hero);

        var sections = new[]
        {
            new Section("نقدینگی", ModernTheme.Warning, new[]
            {
                new Tile("QuickClientReceipt", "📥", "ثبت دریافت از کارفرما"),
                new Tile("CashTransaction", "💸", "ثبت حواله / نقدی"),
                new Tile("CashCheckEdit", "📄", "ثبت چک جدید"),
                new Tile("CashTransactionList", "📋", "لیست تراکنش‌های نقدی"),
                new Tile("AccountTransfer", "🔄", "انتقال بین حساب‌ها"),
                new Tile("OtherReceipt", "📥", "ثبت دریافت متفرقه"),
                new Tile("BankAccounts", "🏦", "حساب‌های بانکی"),
            }),
            new Section("تنخواه", ModernTheme.Warning, new[]
            {
                new Tile("TankhahManagement", "💰", "مدیریت تنخواه‌ها"),
                new Tile("TankhahCharge", "💳", "شارژ / انتقال به تنخواه"),
                new Tile("TankhahSettle", "↩️", "تسویه تنخواه (برگشت به بانک)"),
                new Tile("TankhahPerformanceReport", "📈", "گزارش عملکرد تنخواه"),
            }),
            new Section("داشبوردها و گزارش‌ها", ModernTheme.Purple, new[]
            {
                new Tile("ManagementDashboard", "📊", "داشبورد مدیریتی"),
                new Tile("ContractsDashboard", "📄", "داشبورد قراردادها"),
                new Tile("LiquidityDashboard", "💵", "داشبورد نقدینگی"),
                new Tile("ProfitLoss", "💰", "سود و زیان پروژه‌ها"),
                new Tile("GlobalSearch", "🔍", "جستجوی سراسری مالی"),
            }),
            new Section("اموال", ModernTheme.Purple, new[]
            {
                new Tile("AssetManagement", "🏷️", "مدیریت اموال"),
            }),
            new Section("قراردادها", ModernTheme.Orange, new[]
            {
                new Tile("ClientContracts", "🏢", "قراردادهای کارفرما"),
                new Tile("ContractorContracts", "📝", "قراردادهای پیمانکار"),
                new Tile("PartyManagement", "👥", "طرفین قرارداد"),
            }),
            new Section("پرسنل و حقوق", ModernTheme.Success, new[]
            {
                new Tile("PersonnelList", "📋", "لیست پرسنل"),
                new Tile("AttendanceForm", "📅", "ثبت کارکرد ماهانه"),
                new Tile("PayrollProcess", "🧮", "پردازش حقوق"),
                new Tile("PayrollHistory", "📑", "تاریخچه و گزارش حقوق"),
            }),
        };

        foreach (var sec in sections)
        {
            var tiles = new List<ShortcutTile>();
            foreach (var t in sec.Tiles)
            {
                bool allowed;
                try { allowed = AuthorizationHelper.IsMenuAllowed(t.Key); } catch (Exception __lex) { Smp.Helpers.LogHelper.LogException(__lex, "MainWelcomePanel.IsMenuAllowed"); allowed = false; }
                if (!allowed) continue;
                var tile = new ShortcutTile(t.Icon, t.Text, sec.Color) { Size = new Size(200, TileH) };
                string key = t.Key;
                tile.Click += (s, e) => ShortcutClicked?.Invoke(key);
                tiles.Add(tile);
                Controls.Add(tile);
            }
            if (tiles.Count == 0) continue;

            var header = new Label
            {
                Text = sec.Title,
                AutoSize = false,
                Height = HdrH,
                Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(
                    sec.Color.R * 6 / 10, sec.Color.G * 6 / 10, sec.Color.B * 6 / 10),
                TextAlign = ContentAlignment.MiddleLeft,
                RightToLeft = RightToLeft.Yes,
                BackColor = Color.Transparent
            };
            var color = sec.Color;
            header.Paint += (s, e) =>
            {
                using var br = new SolidBrush(color);
                e.Graphics.FillRectangle(br, 0, 6, 4, header.Height - 12);   // خط رنگی کنار عنوان (سمت چپ)
            };
            header.Padding = new Padding(12, 0, 0, 0);
            Controls.Add(header);
            _sections.Add((header, tiles));
        }

        Resize += (s, e) => LayoutContent();
        LayoutContent();
    }

    /// <summary>
    /// چینش هوشمند: بخش‌ها در چند ستون کنار هم قرار می‌گیرند و اندازه‌ی کاشی‌ها/کارت خوش‌آمد
    /// طوری انتخاب می‌شود که کل محتوا بدون اسکرول در پنجره جا شود. اگر هیچ حالتی جا نشد،
    /// فشرده‌ترین حالت اعمال و اسکرول فعال می‌شود.
    /// </summary>
    private void LayoutContent()
    {
        int W = Width, H = Height;
        if (W <= 0 || H <= 0 || _sections.Count == 0 && W <= 0) return;

        (int hero, int th, int minW, int k, int total)? best = null;
        (int hero, int th, int minW, int k, int total)? chosen = null;

        foreach (int hero in HeroHeights)
        foreach (int th in TileHeights)
        foreach (int minW in MinTileWidths)
        for (int k = 1; k <= 3; k++)
        {
            int total = Plan(W, k, th, minW, hero, apply: false);
            if (best is null || total < best.Value.total) best = (hero, th, minW, k, total);
            if (chosen is null && total <= H) chosen = (hero, th, minW, k, total);
        }

        var pick = chosen ?? best!.Value;
        SuspendLayout();
        bool fits = chosen is not null;
        if (fits) { AutoScrollMinSize = Size.Empty; AutoScroll = false; }
        else AutoScroll = true;
        Plan(W, pick.k, pick.th, pick.minW, pick.hero, apply: true);
        if (!fits) AutoScrollMinSize = new Size(0, pick.total);
        ResumeLayout(false);
    }

    /// <summary>محاسبه (و در صورت apply اعمال) چینش؛ ارتفاع کل مورد نیاز را برمی‌گرداند.</summary>
    private int Plan(int W, int k, int th, int minW, int heroH, bool apply)
    {
        int usable = Math.Max(minW, W - 2 * Pad);
        int colW = (usable - (k - 1) * ColGap) / k;
        int c = Math.Max(1, (colW + Gap) / (minW + Gap));
        int tw = Math.Min(MaxTileW, (colW - (c - 1) * Gap) / c);

        int top = Pad + heroH + 14;
        var colH = new int[k];
        if (apply) _hero.SetBounds(Pad, Pad, usable, heroH);

        foreach (var (header, tiles) in _sections)
        {
            int target = 0;
            for (int i = 1; i < k; i++) if (colH[i] < colH[target]) target = i;

            int right = W - Pad - target * (colW + ColGap);
            int y = top + colH[target];
            int rows = (tiles.Count + c - 1) / c;

            if (apply)
            {
                header.SetBounds(right - colW, y, colW, HdrH);
                for (int i = 0; i < tiles.Count; i++)
                {
                    int row = i / c, col = i % c;
                    tiles[i].SetBounds(right - tw - col * (tw + Gap), y + HdrH + HdrGap + row * (th + Gap), tw, th);
                }
            }
            colH[target] += HdrH + HdrGap + rows * (th + Gap) - Gap + SecGap;
        }
        return top + (colH.Length == 0 ? 0 : colH.Max()) + Pad - SecGap;
    }

    // ───────────── کارت خوش‌آمد ─────────────
    private sealed class HeroCard : Control
    {
        private readonly string _company, _appTitle, _greeting, _date;

        public HeroCard(string company, string appTitle, string? userName)
        {
            _company = company; _appTitle = appTitle;
            int h = DateTime.Now.Hour;
            string part = h < 12 ? "صبح بخیر" : h < 17 ? "ظهر بخیر" : h < 20 ? "عصر بخیر" : "شب بخیر";
            _greeting = string.IsNullOrWhiteSpace(userName) ? part : $"{part}، {userName}";
            string day = DateTime.Now.DayOfWeek switch
            {
                DayOfWeek.Saturday => "شنبه", DayOfWeek.Sunday => "یکشنبه", DayOfWeek.Monday => "دوشنبه",
                DayOfWeek.Tuesday => "سه‌شنبه", DayOfWeek.Wednesday => "چهارشنبه", DayOfWeek.Thursday => "پنجشنبه",
                _ => "جمعه"
            };
            _date = $"{day} {DateHelper.ToPersianDate(DateTime.Now)}";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            RightToLeft = RightToLeft.Yes;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundRect(r, 16);

            using (var br = new LinearGradientBrush(r, ModernPaint.Tint(ModernTheme.Primary, 22), ModernPaint.Tint(ModernTheme.Purple, 14), LinearGradientMode.Horizontal))
                g.FillPath(br, path);
            using (var pen = new Pen(ModernPaint.Tint(ModernTheme.Primary, 35), 1.2f))
                g.DrawPath(pen, path);

            // دایره‌های تزئینی سمت چپ
            var state = g.Save();
            g.SetClip(path);
            using (var b1 = new SolidBrush(Color.FromArgb(40, ModernTheme.Purple)))
                g.FillEllipse(b1, -40, -50, 190, 190);
            using (var b2 = new SolidBrush(Color.FromArgb(34, ModernTheme.Primary)))
                g.FillEllipse(b2, 70, Height - 60, 150, 150);
            g.Restore(state);

            var flags = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPadding;
            int w = Math.Max(100, Width - 56);
            bool compact = Height < 116;
            int yG = compact ? 8 : 14, yC = compact ? 32 : 42, hC = compact ? 34 : 40, yA = compact ? 70 : 84;
            TextRenderer.DrawText(g, _greeting, ModernTheme.GetFont(11f), new Rectangle(28, yG, w, 24), ModernTheme.Text, flags);
            TextRenderer.DrawText(g, _company, ModernTheme.GetFont(compact ? 17f : 20f, FontStyle.Bold), new Rectangle(28, yC, w, hC), Color.FromArgb(40, 50, 90), flags);
            TextRenderer.DrawText(g, _appTitle, ModernTheme.GetFont(10.5f), new Rectangle(28, yA, w, 22), ModernTheme.Text, flags);
            TextRenderer.DrawText(g, _date, ModernTheme.GetFont(10f, FontStyle.Bold), new Rectangle(28, yG, 240, 24), Color.FromArgb(70, 80, 120), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPadding);
        }
    }

    // ───────────── کاشی میانبر ─────────────
    private sealed class ShortcutTile : Control
    {
        private readonly string _icon, _text;
        private readonly Color _color;
        private bool _hover, _down;

        public ShortcutTile(string icon, string text, Color color)
        {
            _icon = icon; _text = text; _color = color;
            Cursor = Cursors.Hand;
            RightToLeft = RightToLeft.Yes;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = RoundRect(r, 14);

            Color fill = _down ? ModernPaint.Tint(_color, 24) : _hover ? ModernPaint.Tint(_color, 14) : Color.White;
            using (var br = new SolidBrush(fill)) g.FillPath(br, path);

            // نوار رنگی سمت راست
            var state = g.Save();
            g.SetClip(path);
            using (var bar = new SolidBrush(_color)) g.FillRectangle(bar, r.Right - 6, r.Top, 7, r.Height);
            g.Restore(state);

            using (var pen = new Pen(_hover ? _color : ModernTheme.Border, _hover ? 1.6f : 1f)) g.DrawPath(pen, path);

            // دایره‌ی آیکون
            int d = Math.Min(36, Height - 14);
            var circle = new Rectangle(r.Right - 14 - d, (Height - d) / 2, d, d);
            using (var cb = new SolidBrush(ModernPaint.Tint(_color, 20))) g.FillEllipse(cb, circle);
            using (var f = new Font("Segoe UI Emoji", 13f))
                TextRenderer.DrawText(g, _icon, f, circle, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var textRect = new Rectangle(r.Left + 8, r.Top + 3, circle.Left - r.Left - 14, r.Height - 6);
            TextRenderer.DrawText(g, _text, ModernTheme.GetFont(10.5f, FontStyle.Bold), textRect, ModernTheme.Text,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
