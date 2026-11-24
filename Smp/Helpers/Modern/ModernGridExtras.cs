using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernGridExtras — افزونه‌های ظاهری جدول (کنار ModernGrid)
//
//     dgv.UsePersonColumn("FullName", "PersonnelCode");      ← آواتار + نام + کد زیر نام
//     dgv.UseBadges("Gender", new() { ["مرد"] = ModernTheme.Primary, ["زن"] = ModernTheme.Purple });
//     dgv.UseSelectionAccent();                               ← نوار رنگی کنار ردیف انتخاب‌شده
//     dgv.UseColumnChooser();                                 ← راست‌کلیک روی هدر: نمایش/پنهان ستون‌ها
//     dgv.UseTextAlignment("Position", "DepartmentName");     ← ستون‌های متنی راست‌چین
// ═══════════════════════════════════════════════════════════════════════
public static class ModernGridExtras
{
    private static readonly ConditionalWeakTable<DataGridView, object> _person = new();
    private static readonly ConditionalWeakTable<DataGridView, object> _badges = new();
    private sealed class AccentBox { public Color C; }
    private static readonly ConditionalWeakTable<DataGridView, AccentBox> _accent = new();
    private static readonly ConditionalWeakTable<DataGridView, object> _airy = new();
    private sealed class HeightBox { public int H; }
    private static readonly ConditionalWeakTable<DataGridView, HeightBox> _airyHeights = new();
    private static readonly ConditionalWeakTable<DataGridView, object> _chooser = new();

    private static readonly Color[] AvatarPalette =
    {
        ModernTheme.Primary, ModernTheme.Teal, ModernTheme.Purple, ModernTheme.Orange,
        ModernTheme.Brand, ModernTheme.Secondary, ModernTheme.Success
    };

    private static bool Matches(DataGridView dgv, int colIndex, ICollection<string> names)
    {
        if (colIndex < 0 || colIndex >= dgv.Columns.Count) return false;
        var col = dgv.Columns[colIndex];
        return names.Contains(col.Name, StringComparer.OrdinalIgnoreCase) ||
               names.Contains(col.DataPropertyName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    // ───────────── ۱) ستون «فرد»: آواتار حروف اول + نام + متن کوچک زیرش ─────────────
    /// <summary>
    /// ستون titleColumn (مثلاً FullName) به‌صورت آواتار + نام و subColumn (مثلاً PersonnelCode) به‌صورت متن کوچک زیر نام کشیده می‌شود.
    /// ستون subColumn را خودتان پنهان کنید.
    /// </summary>
    public static void UsePersonColumn(this DataGridView dgv, string titleColumn, string? subColumn = null, string? subPrefix = "کد: ")
    {
        if (dgv is null || _person.TryGetValue(dgv, out _)) return;
        _person.Add(dgv, new object());

        dgv.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || !Matches(dgv, e.ColumnIndex, new[] { titleColumn })) return;
            e.Handled = true;
            // پس‌زمینه، انتخاب و خطوط را خود گرید می‌کشد؛ فقط «محتوا» دستی کشیده می‌شود
            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

            string title = Convert.ToString(e.Value) ?? string.Empty;
            string sub = string.Empty;
            if (!string.IsNullOrEmpty(subColumn) && dgv.Columns.Contains(subColumn))
            {
                var v = Convert.ToString(dgv.Rows[e.RowIndex].Cells[subColumn].Value);
                if (!string.IsNullOrWhiteSpace(v)) sub = subPrefix + StringHelper.ConvertToPersianDigits(v);
            }

            var g = e.Graphics;
            ModernPaint.EnableSmooth(g);
            var b = e.CellBounds;
            int d = 32;                                              // قطر آواتار
            var avatar = new Rectangle(b.Right - d - 10, b.Top + (b.Height - d) / 2, d, d);

            Color c = AvatarPalette[Math.Abs(title.GetHashCode()) % AvatarPalette.Length];
            using (var br = new SolidBrush(ModernPaint.Tint(c, 18))) g.FillEllipse(br, avatar);
            TextRenderer.DrawText(g, Initials(title), ModernTheme.GetFont(9.5f, FontStyle.Bold), avatar,
                ModernPaint.Darken(c, 40),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            var textRect = new Rectangle(b.Left + 6, b.Top, avatar.Left - b.Left - 14, b.Height);
            var flags = TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            Color tc = selected ? ModernTheme.TextStrong : ModernTheme.Text;

            if (sub.Length > 0)
            {
                var top = new Rectangle(textRect.X, textRect.Y + 4, textRect.Width, textRect.Height / 2 - 2);
                var bot = new Rectangle(textRect.X, textRect.Y + textRect.Height / 2 - 1, textRect.Width, textRect.Height / 2 - 3);
                TextRenderer.DrawText(g, title, ModernTheme.GetFont(10.5f, FontStyle.Bold), top, tc, flags | TextFormatFlags.Bottom);
                TextRenderer.DrawText(g, sub, ModernTheme.GetFont(8.5f), bot, ModernTheme.TextMuted, flags | TextFormatFlags.Top);
            }
            else
            {
                TextRenderer.DrawText(g, title, ModernTheme.GetFont(10.5f, FontStyle.Bold), textRect, tc, flags | TextFormatFlags.VerticalCenter);
            }
        };
    }

    private static string Initials(string fullName)
    {
        var parts = fullName.Split(new[] { ' ', '\u200c' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "؟";
        if (parts.Length == 1) return parts[0].Substring(0, 1);
        return parts[0].Substring(0, 1) + "\u200c" + parts[parts.Length - 1].Substring(0, 1);
    }

    // ───────────── ۲) بج‌های رنگی (قرص کوچک) به‌جای متن ساده ─────────────
    /// <summary>مقدار متنی سلول‌های این ستون‌ها داخل یک قرص رنگی کشیده می‌شود؛ مقدارهای بی‌رنگ‌نشده، خاکستری می‌شوند.</summary>
    public static void UseBadges(this DataGridView dgv, string columnName, Dictionary<string, Color> map, Color? fallback = null)
        => UseBadges(dgv, new[] { columnName }, map, fallback);

    public static void UseBadges(this DataGridView dgv, string[] columnNames, Dictionary<string, Color> map, Color? fallback = null)
    {
        if (dgv is null || columnNames is null || columnNames.Length == 0) return;
        var key = new object();
        var names = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);
        Color fb = fallback ?? ModernTheme.Neutral;

        dgv.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || !Matches(dgv, e.ColumnIndex, names)) return;

            string text = Convert.ToString(e.FormattedValue) ?? string.Empty;
            e.Handled = true;
            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
            if (string.IsNullOrWhiteSpace(text) || text == "---") return;

            map.TryGetValue(text.Trim(), out Color c);
            if (c.IsEmpty) c = fb;

            var g = e.Graphics;
            ModernPaint.EnableSmooth(g);
            var font = ModernTheme.GetFont(9f, FontStyle.Bold);
            var sz = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, 0),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.RightToLeft);
            int w = Math.Min(e.CellBounds.Width - 10, sz.Width + 22), h = 24;
            var pill = new Rectangle(e.CellBounds.Left + (e.CellBounds.Width - w) / 2,
                                     e.CellBounds.Top + (e.CellBounds.Height - h) / 2, w, h);
            using (var path = ModernPaint.RoundedRect(pill, h / 2))
            using (var br = new SolidBrush(ModernPaint.Tint(c, 16)))
                g.FillPath(br, path);
            TextRenderer.DrawText(g, text, font, pill, ModernPaint.Darken(c, 55),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
        };
    }

    // ───────────── ۳) نوار رنگی کنار ردیف انتخاب‌شده (به‌جای بلوک صورتی کامل) ─────────────
    public static void UseSelectionAccent(this DataGridView dgv, Color? color = null)
    {
        if (dgv is null) return;
        Color accent = color ?? ModernTheme.Brand;
        // اگر قبلاً (مثلاً به‌صورت پیش‌فرض) ثبت شده، فقط رنگ عوض می‌شود تا هندلر دوباره اضافه نشود
        if (_accent.TryGetValue(dgv, out var existing))
        {
            existing.C = accent;
            dgv.DefaultCellStyle.SelectionBackColor = ModernPaint.Tint(accent, 8);
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = ModernPaint.Tint(accent, 8);
            dgv.Invalidate();
            return;
        }
        var box = new AccentBox { C = accent };
        _accent.Add(dgv, box);

        // انتخاب ملایم‌تر (کرم خیلی روشن) + نوار ۴ پیکسلی در لبه‌ی راست
        dgv.DefaultCellStyle.SelectionBackColor = ModernPaint.Tint(accent, 8);
        dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = ModernPaint.Tint(accent, 8);

        dgv.RowPostPaint += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount || !dgv.Rows[e.RowIndex].Selected) return;
            using var br = new SolidBrush(box.C);
            e.Graphics.FillRectangle(br, dgv.ClientSize.Width - 4, e.RowBounds.Top + 4, 4, e.RowBounds.Height - 8);
        };
    }

    // ───────────── ۴) ستون‌های متنی راست‌چین؛ بقیه وسط ─────────────
    public static void UseTextAlignment(this DataGridView dgv, params string[] textColumns)
    {
        if (dgv is null || textColumns is null) return;
        dgv.DataBindingComplete += (s, e) =>
        {
            foreach (var name in textColumns)
                if (dgv.Columns.Contains(name))
                    dgv.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        };
    }

    // ───────────── ۵) راست‌کلیک روی هدر = انتخاب ستون‌های قابل‌نمایش ─────────────
    /// <summary>
    /// کاربر می‌تواند ستون‌ها را روشن/خاموش کند (جلوگیری از اسکرول افقی).
    /// defaultVisible: اگر داده شود فقط همین ستون‌ها در ابتدا دیده می‌شوند (بقیه از منو قابل‌روشن‌شدن‌اند).
    /// </summary>
    public static void UseColumnChooser(this DataGridView dgv, string[]? defaultVisible = null, params string[] neverShow)
    {
        if (dgv is null || _chooser.TryGetValue(dgv, out _)) return;
        _chooser.Add(dgv, new object());

        var never = new HashSet<string>(neverShow ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        // انتخاب کاربر نگه داشته می‌شود تا با هر بارگذاری مجدد (ساخت دوباره‌ی ستون‌ها) از بین نرود
        HashSet<string>? visible = defaultVisible is null ? null : new HashSet<string>(defaultVisible, StringComparer.OrdinalIgnoreCase);

        dgv.DataBindingComplete += (s, e) =>
        {
            if (dgv.Columns.Count == 0) return;
            if (visible is null)
                visible = new HashSet<string>(dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewColumn c in dgv.Columns)
                c.Visible = visible.Contains(c.Name) && !never.Contains(c.Name);
        };

        dgv.ColumnHeaderMouseClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var menu = new ContextMenuStrip
            {
                RightToLeft = RightToLeft.Yes,
                Font = ModernTheme.GetFont(10f),
                ShowCheckMargin = true,
                ShowImageMargin = false
            };
            foreach (DataGridViewColumn c in dgv.Columns)
            {
                if (never.Contains(c.Name) || string.IsNullOrWhiteSpace(c.HeaderText)) continue;
                var item = new ToolStripMenuItem(c.HeaderText) { Checked = c.Visible, CheckOnClick = true };
                var col = c;
                item.CheckedChanged += (_, __) =>
                {
                    col.Visible = item.Checked;
                    if (item.Checked) visible?.Add(col.Name); else visible?.Remove(col.Name);
                    dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
                };
                menu.Items.Add(item);
            }
            menu.Closing += (_, ev) =>
            {
                // با هر تیک منو بسته نشود؛ فقط با کلیک بیرون
                if (ev.CloseReason == ToolStripDropDownCloseReason.ItemClicked) ev.Cancel = true;
            };
            menu.Show(Cursor.Position);
        };
    }

    // ───────────── ۶) سربرگ سبک‌تر و ردیف‌های راحت‌تر ─────────────
    /// <summary>هدر کم‌رنگ با خط زیرین، ردیف‌های بلندتر، بدون راه‌راه</summary>
    public static void UseAiryLook(this DataGridView dgv, int rowHeight = 46)
    {
        if (dgv is null) return;
        dgv.RowTemplate.Height = rowHeight;
        foreach (DataGridViewRow r0 in dgv.Rows) if (r0.Height != rowHeight) r0.Height = rowHeight;
        if (_airy.TryGetValue(dgv, out _)) { _airyHeights.GetOrCreateValue(dgv).H = rowHeight; return; }   // فقط ارتفاع به‌روز شود
        _airy.Add(dgv, new object());
        _airyHeights.GetOrCreateValue(dgv).H = rowHeight;
        dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgv.ColumnHeadersHeight = 42;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = ModernTheme.Surface;
        dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = ModernTheme.Surface;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = ModernTheme.TextMuted;
        dgv.ColumnHeadersDefaultCellStyle.Font = ModernTheme.GetFont(9.5f, FontStyle.Bold);
        dgv.AlternatingRowsDefaultCellStyle.BackColor = ModernTheme.Surface;   // بدون راه‌راه
        dgv.BackgroundColor = ModernTheme.Surface;

        // خط ظریف زیر هدر
        dgv.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.GridLine, 1f);
            int y = dgv.ColumnHeadersHeight - 1;
            e.Graphics.DrawLine(pen, 14, y, dgv.ClientSize.Width - 14, y);
        };
        dgv.DataBindingComplete += (s, e) =>
        {
            int h = _airyHeights.TryGetValue(dgv, out var hb) ? hb.H : rowHeight;
            foreach (DataGridViewRow r in dgv.Rows) if (r.Height != h) r.Height = h;
        };
    }
}
