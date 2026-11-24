using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernListKit — اسکلت مشترک «صفحه‌ی لیست» مدرن
//
//  چیدمان از بالا به پایین:
//     هدر ← کارت‌های خلاصه ← نوار جستجو/فیلتر ← جدول ← صفحه‌بندی ← نوار دکمه‌ها
//
//  همه‌ی چیدمان‌ها با TableLayoutPanel ساخته شده‌اند (ستون ۰ = راست) تا در فرم‌های
//  Mirror شده جهت‌ها دقیقاً مثل بقیه‌ی کتابخانه‌ی Modern رفتار کنند.
// ═══════════════════════════════════════════════════════════════════════
public static class ModernListKit
{
    // ───────────── اسکلت صفحه ─────────────
    public static void Compose(Form form, string title, string subtitle, Size size,
                               Control stats, int statsHeight,
                               Control toolbar, Control gridHost,
                               Control pager, Control actions)
    {
        form.SuspendLayout();
        ModernForm.Setup(form, title, size, isDialog: false);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ShowInTaskbar = false;
        form.MinimumSize = new Size(1100, 560);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Margin = Padding.Empty,
            BackColor = ModernTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));                    // هدر
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, statsHeight + 20));       // کارت‌های خلاصه
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));                    // جستجو و فیلتر
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));                    // جدول
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));                    // صفحه‌بندی
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));                    // دکمه‌ها

        stats.Dock = DockStyle.Fill;
        toolbar.Dock = DockStyle.Fill;
        pager.Dock = DockStyle.Fill;
        actions.Dock = DockStyle.Fill;

        root.Controls.Add(ModernForm.CreateHeader(title, subtitle), 0, 0);
        root.Controls.Add(Pad(stats, new Padding(14, 12, 14, 8)), 0, 1);
        root.Controls.Add(Pad(toolbar, new Padding(14, 4, 14, 4)), 0, 2);
        root.Controls.Add(Pad(gridHost, new Padding(14, 0, 14, 0)), 0, 3);
        root.Controls.Add(Pad(pager, new Padding(14, 6, 14, 0)), 0, 4);
        root.Controls.Add(Pad(actions, new Padding(14, 8, 14, 12)), 0, 5);

        form.Controls.Add(root);
        form.ResumeLayout();
    }

    /// <summary>
    /// اسکلت ساده‌ی صفحه‌های ویرایشگر: هدر + ردیف‌های دلخواه. height=0 یعنی باقی‌مانده‌ی فضا (معمولاً جدول).
    /// </summary>
    public static void ComposeRows(Form form, string title, string subtitle, Size size,
                                   params (Control control, int height)[] rows)
    {
        form.SuspendLayout();
        ModernForm.Setup(form, title, size, isDialog: false);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ShowInTaskbar = false;
        form.MinimumSize = new Size(Math.Min(size.Width, 900), Math.Min(size.Height, 520));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows.Length + 1,
            Margin = Padding.Empty,
            BackColor = ModernTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.Controls.Add(ModernForm.CreateHeader(title, subtitle), 0, 0);

        for (int i = 0; i < rows.Length; i++)
        {
            var (c, h) = rows[i];
            root.RowStyles.Add(h > 0 ? new RowStyle(SizeType.Absolute, h) : new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(Pad(c, new Padding(14, 6, 14, 6)), 0, i + 1);
        }

        form.Controls.Add(root);
        form.ResumeLayout();
    }

    /// <summary>
    /// ModernGrid بعد از هر بایند، عنوان ستون‌ها را از نام ستون می‌نویسد (مثلاً «Name» ← «نام / شرکت»).
    /// این متد عنوان‌های فعلی ستون‌ها را ثبت می‌کند و بعد از هر بایند دوباره می‌گذارد. بعد از افزودن ستون‌ها صدا بزنید.
    /// </summary>
    public static void PinHeaders(DataGridView grid)
    {
        var pinned = grid.Columns.Cast<DataGridViewColumn>().Select(c => (col: c, header: c.HeaderText)).ToList();
        grid.DataBindingComplete += (s, e) =>
        {
            foreach (var p in pinned) p.col.HeaderText = p.header;
        };
    }

    /// <summary>بنر هشدار/اطلاع‌رسانی با گوشه‌ی گرد و رنگ معنایی (مثلاً ModernTheme.Danger برای هشدارهای مخرب)</summary>
    public static Panel Banner(string text, Color tone)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.Background, Padding = new Padding(18, 8, 18, 8) };
        panel.Paint += (s, e) =>
        {
            ModernPaint.EnableSmooth(e.Graphics);
            var r = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            if (r.Width < 8 || r.Height < 8) return;
            using var path = ModernPaint.RoundedRect(r, 12);
            using (var br = new SolidBrush(ModernPaint.Tint(tone, 10))) e.Graphics.FillPath(br, path);
            using var pen = new Pen(ModernPaint.Tint(tone, 45), 1.5f);
            e.Graphics.DrawPath(pen, path);
        };
        panel.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernPaint.Darken(tone, 80),
            BackColor = Color.Transparent
        });
        return panel;
    }

    private static Panel Pad(Control inner, Padding padding)
    {
        var p = new Panel { Dock = DockStyle.Fill, Padding = padding, BackColor = Color.Transparent, Margin = Padding.Empty };
        inner.Dock = DockStyle.Fill;
        p.Controls.Add(inner);
        return p;
    }

    // ───────────── نوار جستجو / فیلتر ─────────────
    /// <summary>
    /// هر آیتم یک ستون است؛ آیتم اول در راست‌ترین جا. widths: عرض هر آیتم (0 = خودکار به‌اندازه‌ی محتوا).
    /// ورودی‌های تک‌خطی هم‌قد InputHeight و وسط‌چین عمودی می‌شوند.
    /// </summary>
    public static TableLayoutPanel BuildToolbar(params (Control control, int width)[] items)
    {
        // width: >0 عرض ثابت ، 0 به‌اندازه‌ی محتوا ، <0 باقی‌مانده‌ی فضا
        bool hasFlex = items.Any(i => i.width < 0);
        var t = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = items.Length + (hasFlex ? 0 : 1),
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (int i = 0; i < items.Length; i++)
        {
            var (c, w) = items[i];
            t.ColumnStyles.Add(w < 0 ? new ColumnStyle(SizeType.Percent, 100F)
                             : w > 0 ? new ColumnStyle(SizeType.Absolute, w)
                                     : new ColumnStyle(SizeType.AutoSize));

            if (c is ModernInputHost host)
            {
                host.Height = ModernUI.InputHeight;
                host.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                host.Margin = new Padding(0, 0, 8, 0);
            }
            else if (c is ComboBox)
            {
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                c.Margin = new Padding(0, 0, 8, 0);
            }
            else if (c is Button)
            {
                c.Anchor = AnchorStyles.None;
                c.Margin = new Padding(0, 0, 8, 0);
            }
            else
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 0, 4, 0);
            }
            t.Controls.Add(c, i, 0);
        }
        if (!hasFlex) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));   // فاصله‌ی انعطاف‌پذیر
        return t;
    }

    /// <summary>لیبل کوچک کنار فیلتر (راست‌چین)</summary>
    public static Label FilterLabel(string text)
        => ModernUI.Caption(text, 10.5f, true, ModernTheme.TextMuted);

    // ───────────── صفحه‌بندی ─────────────
    public static TableLayoutPanel BuildPager(out Label info,
        out ModernButton first, out ModernButton prev, out ModernButton next, out ModernButton last)
    {
        var t = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // ۰: متن «صفحه x از y» (راست)
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));     // ۱: فاصله
        for (int i = 0; i < 4; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));

        info = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(4, 0, 16, 0),
            Text = string.Empty
        };

        first = PagerButton("اول");
        prev = PagerButton("قبلی");
        next = PagerButton("بعدی");
        last = PagerButton("آخر");

        t.Controls.Add(info, 0, 0);
        t.Controls.Add(first, 2, 0);   // از راست: اول ، قبلی ، بعدی ، آخر
        t.Controls.Add(prev, 3, 0);
        t.Controls.Add(next, 4, 0);
        t.Controls.Add(last, 5, 0);
        return t;
    }

    private static ModernButton PagerButton(string text)
    {
        var b = ModernButton.Create(text, ModernTheme.Neutral, 78, 32, ModernButtonStyle.Outline);
        b.Width = 78;
        b.Height = 32;
        b.Anchor = AnchorStyles.None;
        b.Margin = new Padding(3, 0, 3, 0);
        return b;
    }

    // ───────────── نوار دکمه‌ها ─────────────
    /// <summary>دکمه‌های اصلی از راست، دکمه‌های ثانویه (اکسل و ...) در انتهای چپ؛ هر گروه به ترتیب از راست به چپ.</summary>
    public static TableLayoutPanel BuildActionBar(IEnumerable<Control> primary, IEnumerable<Control> secondary)
    {
        var p = primary.ToList();
        var s = secondary.ToList();

        var t = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = p.Count + s.Count + 1,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        int col = 0;
        foreach (var b in p) { Place(t, b, col++); }
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));   // فاصله‌ی انعطاف‌پذیر وسط
        col++;
        foreach (var b in s) { Place(t, b, col++); }
        return t;
    }

    private static void Place(TableLayoutPanel t, Control b, int col)
    {
        // ستون‌های دکمه‌ها AutoSize هستند؛ ستون فاصله قبلاً Percent اضافه شده
        if (t.ColumnStyles.Count <= col) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        b.Anchor = AnchorStyles.None;
        b.Margin = new Padding(4, 0, 4, 0);
        t.Controls.Add(b, col, 0);
    }

    // ───────────── رنگ‌بندی مفهومی بج‌ها ─────────────
    /// <summary>محل مصرف / نوع پرداخت: اداری سبز، پروژه آبی، پیمانکار فیروزه‌ای، متفرقه قرمز، سایر خاکستری</summary>
    public static Color ScopeTone(string? text)
    {
        string s = text ?? string.Empty;
        if (s.Contains("متفرقه")) return ModernTheme.Danger;
        if (s.Contains("اداری")) return ModernTheme.Success;
        if (s.Contains("پیمانکار")) return ModernTheme.Teal;
        if (s.Contains("پروژه")) return ModernTheme.Primary;
        return ModernTheme.Neutral;
    }

    /// <summary>وضعیت چک و وضعیت صورت‌وضعیت</summary>
    public static Color StatusTone(string? text)
    {
        string s = text ?? string.Empty;
        if (s.Contains("برگشتی")) return ModernTheme.Danger;
        if (s.Contains("باطل")) return ModernTheme.Neutral;
        if (s.Contains("پاس") || s.Contains("صورت وضعیت شده")) return ModernTheme.Success;
        if (s.Contains("در جریان") || s.Contains("در انتظار")) return ModernTheme.Warning;
        return ModernTheme.Neutral;
    }

    /// <summary>روش پرداخت: انتقال بنفش، کارمزد/سود نارنجی، بقیه خاکستری</summary>
    public static Color MethodTone(string? text)
    {
        string s = text ?? string.Empty;
        if (s.Contains("انتقال")) return ModernTheme.Purple;
        if (s.Contains("کارمزد") || s.Contains("سود")) return ModernTheme.Orange;
        return ModernTheme.Neutral;
    }

    /// <summary>دیکشنری بج‌ها را با مقدارهای واقعی صفحه‌ی جاری پر می‌کند (همان نمونه‌ای که UseBadges گرفته)</summary>
    public static void FillTones(Dictionary<string, Color> map, IEnumerable<string?> values, Func<string?, Color> tone)
    {
        map.Clear();
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            string key = v.Trim();
            if (!map.ContainsKey(key)) map[key] = tone(key);
        }
    }
}
