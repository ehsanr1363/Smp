using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernGrid — ظاهر و رفتار مدرن DataGridView
//  (جایگزین GridHelper؛ نام متدهای افزونه‌ای عیناً حفظ شده تا فرم‌ها تغییر نکنند)
//
//     dgv.ApplyModernStyle();                         ← همان قبلی، ظاهر جدید
//     dgv.EnableThousandsFormatting(true);
//     dgv.HighlightNegatives("Balance", "Net");       ← منفی‌ها قرمز
//     dgv.ColorizeText("Status", new() { ["فعال"] = ModernTheme.Success });
// ═══════════════════════════════════════════════════════════════════════
public static class ModernGrid
{
    private static readonly string[] DefaultHiddenColumns =
    {
        "Id", "PartyId", "NationalId", "TerminationDate", "EmployeeId", "CompanyId", "DepartmentId",
        "ClientContractId", "Password", "Photo"
    };

    private const string AppliedTag = "ModernStyleApplied";
    private static readonly ConditionalWeakTable<DataGridView, object> _styled = new();

    /// <summary>آیا این جدول قبلاً مدرن شده است؟ (مستقل از Tag که فرم‌ها گاهی عوضش می‌کنند)</summary>
    public static bool IsStyled(DataGridView dgv)
        => dgv is not null && (_styled.TryGetValue(dgv, out _) || (dgv.Tag is string t && t == AppliedTag));

    /// <summary>ایجاد یک DataGridView آماده با ظاهر مدرن</summary>
    public static DataGridView Create(params string[] columnsToHide)
    {
        var dgv = new DataGridView { Dock = DockStyle.Fill };
        dgv.ApplyModernStyle(columnsToHide);
        return dgv;
    }

    public static void ApplyModernStyle(this DataGridView dgv, params string[] columnsToHide)
    {
        if (dgv is null) return;

        // جلوگیری از اعمال دوباره (و Subscribe تکراری)
        if (IsStyled(dgv))
        {
            ApplyHiddenAndTranslate(dgv, columnsToHide);
            return;
        }
        _styled.Add(dgv, new object());
        if (dgv.Tag is null) dgv.Tag = AppliedTag;

        EnableDoubleBuffering(dgv);

        dgv.EnableHeadersVisualStyles = false;
        dgv.BackgroundColor = ModernTheme.Background;
        dgv.BorderStyle = BorderStyle.None;
        dgv.GridColor = ModernTheme.GridLine;
        // خطوط پیش‌فرض گرید (تمام‌عرض و پررنگ) خاموش می‌شوند؛ جداکننده‌ی ظریف ردیف‌ها در RowPostPaint رسم می‌شود
        dgv.CellBorderStyle = DataGridViewCellBorderStyle.None;
        dgv.RowPostPaint += (s, e) => PaintRowSeparator(dgv, e);
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        dgv.ReadOnly = true;
        dgv.RowHeadersVisible = false;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToResizeRows = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.MultiSelect = false;
        dgv.RightToLeft = RightToLeft.Yes;
        dgv.Font = ModernTheme.GetFont(11f);
        dgv.RowTemplate.Height = 38;

        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

        var head = dgv.ColumnHeadersDefaultCellStyle;
        head.BackColor = ModernTheme.GridHeaderBack;
        head.ForeColor = ModernTheme.GridHeaderText;
        head.SelectionBackColor = ModernTheme.GridHeaderBack;
        head.SelectionForeColor = ModernTheme.GridHeaderText;
        head.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        head.Alignment = DataGridViewContentAlignment.MiddleCenter;
        head.WrapMode = DataGridViewTriState.True;
        head.Padding = new Padding(4, 5, 4, 5);

        var cell = dgv.DefaultCellStyle;
        cell.BackColor = ModernTheme.Surface;
        cell.ForeColor = ModernTheme.Text;
        cell.SelectionBackColor = ModernTheme.GridSelection;
        cell.SelectionForeColor = ModernTheme.TextStrong;
        cell.Alignment = DataGridViewContentAlignment.MiddleCenter;
        cell.Padding = new Padding(4, 0, 4, 0);

        dgv.AlternatingRowsDefaultCellStyle.BackColor = ModernTheme.SurfaceAlt;
        dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = ModernTheme.GridSelection;
        dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = ModernTheme.TextStrong;

        // هاور سطر
        int hot = -1;
        dgv.CellMouseEnter += (s, e) => SetHot(dgv, ref hot, e.RowIndex);
        dgv.MouseLeave += (s, e) => SetHot(dgv, ref hot, -1);
        // رنگ هاور از طریق CellFormatting اعمال می‌شود (با رسم دستی سلول‌ها هم هماهنگ می‌ماند)
        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex == hot && e.CellStyle is not null &&
                e.RowIndex < dgv.RowCount && !dgv.Rows[e.RowIndex].Selected &&
                e.CellStyle.BackColor == dgv.DefaultCellStyle.BackColor)
                e.CellStyle.BackColor = ModernTheme.GridHover;
        };

        // ناوبری با Enter در گرید
        EnterNavigationHelper.EnableForGrid(dgv);

        dgv.DataError += (s, e) =>
        {
            e.ThrowException = false;
            e.Cancel = true;
        };

        dgv.DataBindingComplete += (s, e) => ApplyHiddenAndTranslate(dgv, columnsToHide);

        // ظاهر پیش‌فرض همه‌ی جدول‌ها: سربرگ سبک، ردیف‌های بلند بدون راه‌راه، انتخاب ملایم با نوار رنگی
        // (فرم‌ها می‌توانند بعداً UseAiryLook(h) / UseSelectionAccent(color) را برای تنظیم ارتفاع و رنگ صدا بزنند)
        dgv.UseAiryLook(44);
        dgv.UseSelectionAccent();
    }

    /// <summary>جداکننده‌ی بسیار ظریف و کوتاه‌شده (با حاشیه از دو طرف) زیر هر ردیف؛ ردیف آخر ندارد</summary>
    private static void PaintRowSeparator(DataGridView dgv, DataGridViewRowPostPaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount - 1) return;
        if ((dgv.Rows[e.RowIndex].State & DataGridViewElementStates.Selected) != 0) return;   // ردیف انتخاب‌شده خودش پس‌زمینه دارد
        int y = e.RowBounds.Bottom - 1;
        int left = 14, right = dgv.ClientSize.Width - 14;
        if (right <= left) return;
        using var pen = new Pen(Color.FromArgb(140, ModernTheme.GridLine), 1f);
        e.Graphics.DrawLine(pen, left, y, right, y);
    }

    private static void SetHot(DataGridView dgv, ref int hot, int row)
    {
        if (row == hot) return;
        int old = hot;
        hot = row;
        if (old >= 0 && old < dgv.RowCount) dgv.InvalidateRow(old);
        if (row >= 0 && row < dgv.RowCount) dgv.InvalidateRow(row);
    }

    /// <summary>DoubleBuffered برای DataGridView پیش‌فرض خاموش و protected است؛ با Reflection روشن می‌شود (حذف سوسو)</summary>
    private static void EnableDoubleBuffering(DataGridView dgv)
    {
        try
        {
            typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(dgv, true, null);
        }
        catch (Exception __ex) { /* در بدترین حالت فقط کمی سوسو دارد */ Smp.Helpers.LogHelper.LogException(__ex, "ModernGrid"); }
    }

    private static void ApplyHiddenAndTranslate(DataGridView dgv, string[] columnsToHide)
    {
        var allHide = DefaultHiddenColumns.Concat(columnsToHide ?? Array.Empty<string>()).Distinct();
        foreach (var colName in allHide)
            if (dgv.Columns.Contains(colName))
                dgv.Columns[colName].Visible = false;

        foreach (DataGridViewColumn col in dgv.Columns)
        {
            string key = !string.IsNullOrWhiteSpace(col.DataPropertyName) ? col.DataPropertyName : col.Name;
            string translated = TranslationHelper.GetColumnName(key);
            // فقط وقتی ترجمه‌ی واقعی وجود دارد؛ وگرنه هدر فارسی که فرم تعیین کرده با نام انگلیسی ستون بازنویسی می‌شد
            if (!string.IsNullOrWhiteSpace(translated) && !string.Equals(translated, key, StringComparison.Ordinal))
                col.HeaderText = translated;
        }

        try
        {
            // در حالت Fill تغییر دستی عرض، FillWeight ستون‌ها را به‌هم می‌ریزد (ستون‌ها کج و متن ... می‌شد)
            if (dgv.AutoSizeColumnsMode != DataGridViewAutoSizeColumnsMode.Fill)
                dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
            dgv.AutoResizeColumnHeadersHeight();
        }
        catch (Exception __ex) { /* گرید خالی */ Smp.Helpers.LogHelper.LogException(__ex, "ModernGrid"); }
    }

    /// <summary>ستون‌ها کل عرض را پر کنند (برای لیست‌های کم‌ستون)؛ متن راست‌چین با حاشیه</summary>
    public static void FillColumns(this DataGridView dgv)
    {
        if (dgv is null) return;
        dgv.RightToLeft = RightToLeft.Yes;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.DefaultCellStyle.Padding = new Padding(5, 0, 5, 0);
        dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(5, 5, 5, 5);
    }

    // ───────────── فرمت اعداد (هزارگان + ارقام فارسی) ─────────────
    private static readonly ConditionalWeakTable<DataGridView, object> _thousandsAll = new();
    private static readonly ConditionalWeakTable<DataGridView, object> _thousandsCols = new();
    private static readonly ConditionalWeakTable<DataGridView, object> _negatives = new();
    private static readonly ConditionalWeakTable<DataGridView, object> _colorize = new();

    private static bool TryFormatNumber(object? value, bool includeIntegers, bool includeDouble, out string text)
    {
        text = string.Empty;
        switch (value)
        {
            case decimal d:
                text = NumberHelper.FormatThousands(d, usePersianDigits: true);
                return true;
            case double db when includeDouble:
                text = NumberHelper.FormatThousands((decimal)db, usePersianDigits: true);
                return true;
            case int i when includeIntegers:
                text = NumberHelper.FormatThousands(i, usePersianDigits: true);
                return true;
            case long l when includeIntegers:
                text = NumberHelper.FormatThousands(l, usePersianDigits: true);
                return true;
            default:
                return false;
        }
    }

    public static void EnableThousandsFormatting(this DataGridView dgv, bool includeIntegers = false)
    {
        if (dgv is null) return;
        if (_thousandsAll.TryGetValue(dgv, out _)) return;
        _thousandsAll.Add(dgv, new object());

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (TryFormatNumber(e.Value, includeIntegers, false, out string text))
            {
                e.Value = text;
                e.FormattingApplied = true;
            }
        };
    }

    public static void EnableThousandsFormattingFor(this DataGridView dgv, params string[] columnNames)
    {
        if (dgv is null || columnNames is null || columnNames.Length == 0) return;
        if (_thousandsCols.TryGetValue(dgv, out _)) return;
        _thousandsCols.Add(dgv, new object());

        var names = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = dgv.Columns[e.ColumnIndex];
            if (!names.Contains(col.Name) && !names.Contains(col.DataPropertyName ?? string.Empty)) return;

            if (TryFormatNumber(e.Value, true, true, out string text))
            {
                e.Value = text;
                e.FormattingApplied = true;
            }
        };
    }

    public static void TranslateHeaders(this DataGridView dgv, Dictionary<string, string> translations)
    {
        if (dgv is null || translations is null) return;
        foreach (var item in translations)
            if (dgv.Columns.Contains(item.Key))
                dgv.Columns[item.Key].HeaderText = item.Value;
    }

    // ───────────── رنگ‌بندی مفهومی ─────────────

    /// <summary>مقادیر منفی ستون‌های نام‌برده قرمز و (اختیاری) مثبت‌ها سبز نمایش داده شوند</summary>
    public static void HighlightNegatives(this DataGridView dgv, params string[] columnNames)
        => HighlightSigned(dgv, false, columnNames);

    public static void HighlightSigned(this DataGridView dgv, bool positiveGreen, params string[] columnNames)
    {
        if (dgv is null || columnNames is null || columnNames.Length == 0) return;
        if (_negatives.TryGetValue(dgv, out _)) return;
        _negatives.Add(dgv, new object());

        var names = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);
        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = dgv.Columns[e.ColumnIndex];
            if (!names.Contains(col.Name) && !names.Contains(col.DataPropertyName ?? string.Empty)) return;

            decimal? v = e.Value switch
            {
                decimal d => d,
                double db => (decimal)db,
                int i => i,
                long l => l,
                _ => (decimal?)null
            };
            if (v is null || e.CellStyle is null) return;

            if (v < 0) e.CellStyle.ForeColor = ModernTheme.Danger;
            else if (positiveGreen && v > 0) e.CellStyle.ForeColor = Color.FromArgb(22, 163, 74);
        };
    }

    /// <summary>رنگ متن (و پس‌زمینه‌ی کم‌رنگ) سلول‌ها بر اساس مقدار متنی؛ مثل وضعیت: «فعال» → سبز</summary>
    public static void ColorizeText(this DataGridView dgv, string columnName, Dictionary<string, Color> map, bool softBackground = true)
    {
        if (dgv is null || map is null || map.Count == 0) return;
        if (_colorize.TryGetValue(dgv, out _)) return;
        _colorize.Add(dgv, new object());

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = dgv.Columns[e.ColumnIndex];
            if (!string.Equals(col.Name, columnName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(col.DataPropertyName, columnName, StringComparison.OrdinalIgnoreCase)) return;

            string key = Convert.ToString(e.Value) ?? string.Empty;
            if (map.TryGetValue(key.Trim(), out Color c) && e.CellStyle is not null)
            {
                e.CellStyle.ForeColor = ModernPaint.Darken(c, 40);
                if (softBackground) e.CellStyle.BackColor = ModernPaint.Tint(c, 14);
            }
        };
    }
}
