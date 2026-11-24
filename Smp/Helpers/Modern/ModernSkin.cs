using System;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernSkin — مدرن‌سازی خودکار فرم‌های قدیمی
//  هنگام باز شدن هر فرم (ModernForm.Setup) درخت کنترل‌ها را می‌گردد و کنترل‌های خام را مدرن می‌کند:
//    • TextBox / NumericUpDown  → داخل قاب گرد ModernInputHost (خود کنترل همان می‌ماند)
//    • DataGridView             → ModernGrid.ApplyModernStyle (تنظیمات فرم مثل ReadOnly/MultiSelect حفظ می‌شود)
//    • TabControl               → تب‌های سفید با خط آبی
//    • ComboBox / Button / GroupBox → استایل تخت و هماهنگ با تم
//  هر مرحله جداگانه try/catch دارد؛ در بدترین حالت همان کنترل خام می‌ماند و فرم خراب نمی‌شود.
//  برای معاف کردن یک کنترل: control.Tag = "NoSkin"
// ═══════════════════════════════════════════════════════════════════════
public static class ModernSkin
{
    private static readonly ConditionalWeakTable<Form, object> _hooked = new();
    private const string GridTag = "ModernStyleApplied";

    /// <summary>اسکین به‌صورت خودکار هنگام Load فرم اعمال می‌شود</summary>
    public static void Attach(Form form)
    {
        if (form is null || _hooked.TryGetValue(form, out _)) return;
        _hooked.Add(form, new object());
        form.Load += (s, e) => Apply(form);
    }

    public static void Apply(Control root)
    {
        if (root is null || root.IsDisposed) return;
        root.SuspendLayout();
        try { Walk(root); }
        catch (Exception __ex) { /* اسکین هرگز نباید فرم را خراب کند */ Smp.Helpers.LogHelper.LogException(__ex, "ModernSkin"); }
        finally { root.ResumeLayout(true); }
    }

    // ───────────── شبکه‌ی ایمنی: هر جدولی در هر فرمی (حتی بدون Setup) مدرن شود ─────────────
    private static readonly System.Diagnostics.Stopwatch _sweepClock = System.Diagnostics.Stopwatch.StartNew();
    private static long _lastSweep;
    private static bool _sweepHooked;

    /// <summary>یک‌بار در Program.Main صدا زده شود؛ هنگام Idle برنامه، همه‌ی فرم‌های باز را برای جدول‌های مدرن‌نشده می‌گردد</summary>
    public static void EnableGlobalGridSweep()
    {
        if (_sweepHooked) return;
        _sweepHooked = true;
        Application.Idle += (s, e) =>
        {
            long now = _sweepClock.ElapsedMilliseconds;
            if (now - _lastSweep < 400) return;          // سبک بماند
            _lastSweep = now;
            try
            {
                foreach (Form f in Application.OpenForms.Cast<Form>().ToArray())
                {
                    if (f is null || f.IsDisposed || !f.IsHandleCreated) continue;
                    // فرمی که هیچ‌وقت از ModernForm.Setup رد نشده: کل کنترل‌های خامش یک‌بار مدرن می‌شود
                    if (ShouldFullSkin(f) && !_hooked.TryGetValue(f, out _) && !_fullSwept.TryGetValue(f, out _))
                    {
                        _fullSwept.Add(f, new object());
                        Apply(f);
                    }
                    SkinAllGrids(f);   // جدول‌هایی که دیرتر ساخته می‌شوند هم گرفته شوند
                }
            }
            catch (Exception ex) { Smp.Helpers.LogHelper.LogException(ex, "ModernSkin.Sweep"); }
        };
    }

    private static readonly ConditionalWeakTable<Form, object> _fullSwept = new();

    // فرم‌های سفارشی/چاپی/ورود که ظاهر خودشان را دارند و نباید دست بخورند
    private static readonly System.Collections.Generic.HashSet<string> _noFullSkin = new()
    {
        "LoginForm", "MainForm", "ChangePasswordForm", "ContractPrintForm", "StatementPrintForm",
        "ChequePrintForm", "TankhahPrintForm", "PayslipForm", "PaymentOrderForm", "PersonnelDecreeForm",
        "GraphicalDashboardForm", "LiquidityDashboardForm", "ProfitLossDashboardForm", "ProjectProfitLossDashboardForm"
    };

    private static bool ShouldFullSkin(Form f)
    {
        if (Skip(f)) return false;
        var t = f.GetType();
        return (t.Namespace ?? "").StartsWith("Smp.Forms", StringComparison.Ordinal) && !_noFullSkin.Contains(t.Name);
    }

    /// <summary>فقط جدول‌ها (نه بقیه کنترل‌ها) را در کل درخت — شامل تب‌ها و UserControlها — مدرن می‌کند</summary>
    public static void SkinAllGrids(Control root)
    {
        if (root is null || root.IsDisposed || Skip(root)) return;
        foreach (Control c in root.Controls.Cast<Control>().ToArray())
        {
            if (c.IsDisposed || Skip(c)) continue;
            if (c is DataGridView g)
            {
                if (ModernGrid.IsStyled(g)) continue;
                try
                {
                    SkinGrid(g);
                    // ردیف‌هایی که قبل از اسکین ساخته شده‌اند هم‌قد ردیف جدید شوند
                    int h = g.RowTemplate.Height;
                    foreach (DataGridViewRow r in g.Rows) if (r.Height != h) r.Height = h;
                }
                catch (Exception ex) { Smp.Helpers.LogHelper.LogException(ex, "ModernSkin.SkinAllGrids"); }
                continue;
            }
            if (c.HasChildren) SkinAllGrids(c);
        }
    }

    private static bool Skip(Control c) => c.Tag is string t && t == "NoSkin";

    private static void Walk(Control parent)
    {
        foreach (var child in parent.Controls.Cast<Control>().ToArray())
        {
            if (child.IsDisposed || Skip(child)) continue;
            try
            {
                switch (child)
                {
                    case ModernInputHost:
                    case ModernComboBox:
                    case ModernButton:
                    case ModernCheckBox:
                    case ModernRadioButton:
                    case ModernListBox:
                        continue;                                   // قبلاً مدرن شده‌اند (داخلشان را نگرد)

                    case TextBox tb:
                        if (!(tb.Parent is ModernInputHost)) WrapInPlace(tb);
                        continue;

                    case NumericUpDown nud:
                        WrapInPlace(nud);
                        continue;                                   // کنترل‌های داخلی NumericUpDown را نگرد

                    case DataGridView grid:
                        SkinGrid(grid);
                        continue;

                    case TabControl tabs:
                        SkinTabs(tabs);
                        foreach (TabPage page in tabs.TabPages) Walk(page);
                        continue;

                    case ComboBox cb:
                        cb.FlatStyle = FlatStyle.Flat;
                        cb.BackColor = ModernTheme.InputFill;
                        continue;

                    case Button btn:
                        SkinButton(btn);
                        continue;

                    case GroupBox gb:
                        gb.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
                        gb.ForeColor = ModernTheme.TextStrong;
                        Walk(gb);
                        continue;

                    case DateTimePicker:
                    case MaskedTextBox:
                    case RichTextBox:
                        continue;

                    default:
                        if (child.HasChildren) Walk(child);
                        break;
                }
            }
            catch (Exception __ex) { /* کنترل خام بماند */ Smp.Helpers.LogHelper.LogException(__ex, "ModernSkin"); }
        }
    }

    // ───────────── قاب گرد دور TextBox / NumericUpDown (جای‌گذاری دقیق در همان سلول) ─────────────
    private static void WrapInPlace(Control inner)
    {
        var parent = inner.Parent;
        if (parent is null || parent is ModernInputHost) return;
        if (parent is ComboBox || parent is NumericUpDown || parent is DataGridView) return;

        var dock = inner.Dock;
        var anchor = inner.Anchor;
        var margin = inner.Margin;
        var bounds = inner.Bounds;
        int tab = inner.TabIndex;
        int z = parent.Controls.GetChildIndex(inner);
        bool multi = inner is TextBox t && t.Multiline;

        var tlp = parent as TableLayoutPanel;
        TableLayoutPanelCellPosition pos = default;
        int colSpan = 1, rowSpan = 1;
        if (tlp is not null)
        {
            pos = tlp.GetPositionFromControl(inner);
            colSpan = tlp.GetColumnSpan(inner);
            rowSpan = tlp.GetRowSpan(inner);
        }
        var flow = parent as FlowLayoutPanel;
        bool flowBreak = flow is not null && flow.GetFlowBreak(inner);

        // ورودی‌های تک‌خطی کوتاه، به ارتفاع استاندارد می‌رسند (وسط‌چین، بدون جابه‌جایی بقیه)
        if (!multi && dock == DockStyle.None && bounds.Height < ModernTheme.InputHeight - 4)
        {
            int extra = ModernTheme.InputHeight - bounds.Height;
            bounds.Y -= extra / 2;
            bounds.Height = ModernTheme.InputHeight;
        }

        parent.SuspendLayout();
        try
        {
            parent.Controls.Remove(inner);
            var host = new ModernInputHost(inner) { Margin = margin, TabIndex = tab };
            if (dock != DockStyle.None) host.Dock = dock;
            else { host.Bounds = bounds; host.Anchor = anchor; }

            if (tlp is not null && pos.Column >= 0 && pos.Row >= 0)
            {
                tlp.Controls.Add(host, pos.Column, pos.Row);
                if (colSpan > 1) tlp.SetColumnSpan(host, colSpan);
                if (rowSpan > 1) tlp.SetRowSpan(host, rowSpan);
            }
            else parent.Controls.Add(host);

            parent.Controls.SetChildIndex(host, Math.Min(z, parent.Controls.Count - 1));
            if (flowBreak && flow is not null) flow.SetFlowBreak(host, true);
        }
        finally { parent.ResumeLayout(true); }
    }

    // ───────────── جدول: ظاهر مدرن بدون از دست رفتن تنظیمات فرم ─────────────
    private static void SkinGrid(DataGridView g)
    {
        if (ModernGrid.IsStyled(g)) return;

        bool ro = g.ReadOnly, multi = g.MultiSelect, addRows = g.AllowUserToAddRows, delRows = g.AllowUserToDeleteRows;
        var selMode = g.SelectionMode;
        var autoSize = g.AutoSizeColumnsMode;
        var editMode = g.EditMode;
        bool rowHeaders = g.RowHeadersVisible;

        g.ApplyModernStyle();

        // ApplyModernStyle همه‌چیز را فقط‌خواندنی می‌کند؛ جدول‌های قابل‌ویرایش فرم‌های قدیمی همان‌طور بمانند
        g.ReadOnly = ro;
        g.MultiSelect = multi;
        g.AllowUserToAddRows = addRows;
        g.AllowUserToDeleteRows = delRows;
        g.SelectionMode = selMode;
        g.EditMode = editMode;
        if (!ro) g.RowHeadersVisible = rowHeaders;
        if (autoSize != DataGridViewAutoSizeColumnsMode.None) g.AutoSizeColumnsMode = autoSize;
    }

    // ───────────── تب‌ها ─────────────
    private static void SkinTabs(TabControl tabs)
    {
        if (tabs.DrawMode != TabDrawMode.OwnerDrawFixed) ModernForm.StyleTabControl(tabs);
        foreach (TabPage p in tabs.TabPages)
        {
            p.UseVisualStyleBackColor = false;
            p.BackColor = ModernTheme.Background;
        }
    }

    // ───────────── دکمه‌ی خام باقی‌مانده: حداقل تخت و هم‌رنگ تم ─────────────
    private static void SkinButton(Button b)
    {
        // دکمه‌های شفاف (آیکنی/همبرگری) دست‌نخورده بمانند
        if (b.BackColor == Color.Transparent) { b.Cursor = Cursors.Hand; return; }

        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.Cursor = Cursors.Hand;
        b.UseVisualStyleBackColor = false;
        if (b.BackColor == SystemColors.Control) b.BackColor = ModernTheme.Primary;

        // متن سفید فقط روی رنگ‌های تیره؛ روی رنگ‌های روشن متن تیره
        var c = b.BackColor;
        double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        b.ForeColor = lum < 0.62 ? Color.White : ModernTheme.TextStrong;
        b.Font = ModernTheme.GetFont(9.5f, FontStyle.Bold);
        b.FlatAppearance.MouseOverBackColor = ModernPaint.Darken(c, 12);
        b.FlatAppearance.MouseDownBackColor = ModernPaint.Darken(c, 25);
    }
}
