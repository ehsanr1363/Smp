using Smp.Data;
using Smp.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PayrollHistoryForm : Form
{
    private readonly PayrollRepository _repository;

    private ComboBox cmbYear = null!, cmbMonth = null!;
    private Button btnLoad = null!, btnExport = null!, btnDeleteRow = null!, btnDeleteMonth = null!;
    private DataGridView dgvHistory = null!;
    private DataTable? _currentData;
    private ModernStatCard _stCount = null!, _stEarnings = null!, _stDeductions = null!, _stNet = null!;
    private HistorySectionTitle _title = null!;

    // دوره‌ای که الآن در جدول نمایش داده می‌شود (نه لزوماً مقدار فعلی کمبوها)
    private bool _loaded;
    private int _loadedYear, _loadedMonth;

    // ─── ترتیب و گروه‌بندی ستون‌ها (فقط روی صفحه؛ خروجی اکسل همان قبلی است) ───
    private static readonly string[] EarningCols =
    {
        "BaseSalary", "YearsBonus", "AttractionAllowance", "ResponsibilityAllowance", "ManagementAllowance",
        "TechnicalAllowance", "HousingAllowance", "GroceryAllowance", "ChildAllowance", "MarriageAllowance",
        "TransportationAllowance", "OtherAllowances", "OvertimePay", "TotalEarnings"
    };

    private static readonly string[] DeductionCols =
    {
        "AbsenceHours", "AbsenceDeduction", "InsuranceEmployee", "IncomeTax", "LoanDeduction", "AdvanceDeduction",
        "SupplementaryInsuranceDeduction", "CarInsuranceDeduction", "OtherDeductions", "TotalDeductions"
    };

    private static readonly string[] ColumnOrder =
        new[] { "EmployeeName", "PersonnelCode", "PersonnelType", "Position", "WorkingDays", "OvertimeHours" }
        .Concat(EarningCols).Concat(DeductionCols)
        .Concat(new[] { "NetPay", "InsuranceEmployer", "InsuranceBase", "TaxableBase", "NationalCode", "InsuranceNumber", "ShebaNumber", "Year", "Month" })
        .ToArray();

    private static readonly Dictionary<string, int> ColumnWidths = new()
    {
        ["EmployeeName"] = 250, ["PersonnelCode"] = 96, ["PersonnelType"] = 112, ["Position"] = 150,
        ["WorkingDays"] = 100, ["OvertimeHours"] = 104,
        ["TotalEarnings"] = 130, ["TotalDeductions"] = 130, ["NetPay"] = 144,
        ["NationalCode"] = 120, ["InsuranceNumber"] = 120, ["ShebaNumber"] = 240,
        ["Year"] = 72, ["Month"] = 72
    };

    public PayrollHistoryForm(string connectionString)
    {
        _repository = new PayrollRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("تاریخچه و گزارش حقوق ماهانه", new Size(1240, 760));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);

        LoadDefaults();
    }

    private void LoadDefaults()
    {
        var pc = new PersianCalendar();
        int currentYear = pc.GetYear(DateTime.Now);
        int currentMonth = pc.GetMonth(DateTime.Now);

        if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
        else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

        if (cmbMonth.Items.Contains(currentMonth)) cmbMonth.SelectedItem = currentMonth;
        else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // UI
    // ═══════════════════════════════════════════════════════════════
    private void BuildUI()
    {
        // ─── انتخاب دوره ───
        cmbYear = ModernUI.Combo();
        ComboBoxHelper.FillYears(cmbYear);
        cmbYear.Width = 100;
        cmbYear.Margin = new Padding(2, 0, 14, 0);

        cmbMonth = ModernUI.Combo();
        ComboBoxHelper.FillMonths(cmbMonth);
        cmbMonth.Width = 90;
        cmbMonth.Margin = new Padding(2, 0, 0, 0);

        // لیبل هم‌ارتفاع با کمبو و وسط‌چین تا دقیقاً هم‌تراز شوند
        Label PeriodLabel(string t)
        {
            var font = ModernTheme.GetFont(10f, FontStyle.Bold);
            int w = TextRenderer.MeasureText(t, font).Width + 8;
            return new Label
            {
                Text = t, AutoSize = false, Size = new Size(w, ModernTheme.InputHeight),
                TextAlign = ContentAlignment.MiddleCenter, Font = font,
                ForeColor = ModernTheme.TextMuted, Margin = new Padding(0, 0, 2, 0), BackColor = Color.Transparent
            };
        }

        // ─── دکمه‌ها ───
        btnLoad = ToolButton("نمایش لیست", ModernTheme.Primary, ModernButtonStyle.Filled, PayrollIcon.Search);
        btnLoad.Click += async (s, e) => await LoadDataSafeAsync();

        btnExport = ToolButton("خروجی اکسل", ModernTheme.Success, ModernButtonStyle.Soft, PayrollIcon.Export);
        btnExport.Enabled = false;
        btnExport.Click += async (s, e) => await ExportDataSafeAsync();

        btnDeleteMonth = ToolButton("حذف فیش‌های این ماه", ModernTheme.Danger, ModernButtonStyle.Soft, PayrollIcon.Trash);
        btnDeleteMonth.Enabled = false;
        btnDeleteMonth.Click += BtnDeleteMonth_Click;

        btnDeleteRow = ToolButton("حذف فیش انتخاب‌شده", ModernTheme.Danger, ModernButtonStyle.Soft, PayrollIcon.Trash);
        btnDeleteRow.Enabled = false;
        btnDeleteRow.Click += BtnDeleteRow_Click;

        FlowLayoutPanel Group(params Control[] items)
        {
            var f = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft, RightToLeft = RightToLeft.Yes,
                Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent,
                Anchor = AnchorStyles.None
            };
            foreach (var c in items) f.Controls.Add(c);
            return f;
        }

        Panel Divider() => new Panel
        {
            Width = 1, Height = 28, BackColor = ModernTheme.Border,
            Margin = new Padding(16, 0, 16, 0), Anchor = AnchorStyles.None
        };

        // راست → چپ:  [دوره] | [نمایش، اکسل] ........ [حذف ماه]
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
            RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        bar.Controls.Add(Group(PeriodLabel("سال"), cmbYear, PeriodLabel("ماه"), cmbMonth), 0, 0);
        bar.Controls.Add(Divider(), 1, 0);
        bar.Controls.Add(Group(btnLoad, btnExport), 2, 0);
        bar.Controls.Add(Group(btnDeleteMonth), 4, 0);

        var topCard = new ModernCard("", ModernTheme.Primary, 1) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        topCard.Body.Controls.Add(bar, 0, 0);

        // ─── کارت‌های خلاصه ───
        var strip = ModernReport.CreateStatStrip(4, 88);
        _stCount = strip.Add("تعداد فیش", ModernTheme.Primary, "");
        _stEarnings = strip.Add("جمع مزایا", ModernTheme.Teal, "حقوق و مزایا + اضافه‌کار");
        _stDeductions = strip.Add("جمع کسورات", ModernTheme.Danger, "بیمه، مالیات، وام و سایر کسورات");
        _stNet = strip.Add("جمع خالص پرداختی", ModernTheme.Success, "");
        foreach (var card in new[] { _stCount, _stEarnings, _stDeductions, _stNet }) card.ValueFontSize = 14f;
        strip.Dock = DockStyle.Fill;
        strip.Margin = new Padding(0, 0, 0, 10);

        // ─── سرتیتر لیست + دکمه‌ی حذف فیش انتخاب‌شده (قبلاً در نوار جدای پایین بود) ───
        _title = new HistorySectionTitle { Dock = DockStyle.Fill };

        var listHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes,
            Margin = Padding.Empty, Padding = new Padding(2, 0, 2, 8), BackColor = Color.Transparent
        };
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        listHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        btnDeleteRow.Anchor = AnchorStyles.None;
        listHeader.Controls.Add(_title, 0, 0);
        listHeader.Controls.Add(btnDeleteRow, 1, 0);

        // ─── جدول ───
        dgvHistory = new DataGridView { Dock = DockStyle.Fill };
        dgvHistory.ApplyModernStyle();
        dgvHistory.UseAiryLook(48);
        dgvHistory.UseSelectionAccent(ModernTheme.Primary);
        dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgvHistory.ColumnHeadersHeight = 50;
        dgvHistory.UsePersonColumn("EmployeeName", "PersonnelCode");
        dgvHistory.UseBadges("PersonnelType", new Dictionary<string, Color>
        {
            ["روزمزد"] = ModernTheme.Orange,
            ["ساعتی"] = ModernTheme.Teal,
            ["قراردادی"] = ModernTheme.Primary,
            ["ماهانه"] = ModernTheme.Primary
        });
        dgvHistory.UseColumnChooser();
        dgvHistory.SelectionChanged += (s, e) => btnDeleteRow.Enabled = dgvHistory.CurrentRow is not null;

        // ستون‌ها با هر بارگذاری از نو ساخته می‌شوند؛ ترتیب، عرض و رنگ سربرگ هر بار دوباره اعمال می‌شود
        dgvHistory.DataBindingComplete += (s, e) => PrepareColumns();

        dgvHistory.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string colName = dgvHistory.Columns[e.ColumnIndex].Name;

            if (e.Value is DateTime dt)
            {
                e.Value = DateHelper.ToPersianDate(dt);
                e.FormattingApplied = true;
            }
            else if (colName == "OvertimeHours" && e.Value is decimal ot)
            {
                // ساعت اعشاری → ساعت:دقیقه (مثلاً ۷٫۵ → ۰۷:۳۰)
                int totalMinutes = (int)Math.Round(Math.Abs(ot) * 60m, MidpointRounding.AwayFromZero);
                e.Value = StringHelper.ConvertToPersianDigits($"{totalMinutes / 60:00}:{totalMinutes % 60:00}");
                e.FormattingApplied = true;
            }
            else if ((colName == "WorkingDays" || colName == "AbsenceHours") && e.Value is decimal qty)
            {
                e.Value = StringHelper.ConvertToPersianDigits(qty.ToString("0.##"));
                e.FormattingApplied = true;
            }
            else if ((colName == "Year" || colName == "Month") && e.Value is int ym)
            {
                // سال و ماه بدون جداکننده‌ی هزارگان (قبلاً سال به‌صورت «۱٬۴۰۵» نمایش داده می‌شد)
                e.Value = StringHelper.ConvertToPersianDigits(ym.ToString());
                e.FormattingApplied = true;
            }
            else if (e.Value is decimal dec)
            {
                // مقدارهای صفر کم‌رنگ می‌شوند تا اعداد واقعی بیشتر دیده شوند
                if (dec == 0m && e.CellStyle is not null && colName != "NetPay")
                    e.CellStyle.ForeColor = ModernTheme.Placeholder;
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
            else if (e.Value is int i)
            {
                e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true);
                e.FormattingApplied = true;
            }

            if (e.CellStyle is not null)
            {
                if (colName == "NetPay") { e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold); e.CellStyle.ForeColor = ModernPaint.Darken(ModernTheme.Success, 70); }
                else if (colName == "TotalDeductions") { e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold); e.CellStyle.ForeColor = ModernTheme.DangerText; }
                else if (colName == "TotalEarnings") e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
            }
        };

        // حالت خالی: به‌جای جدول سفید، توضیح و راهنما نمایش داده می‌شود
        dgvHistory.Paint += (s, e) =>
        {
            if (dgvHistory.Rows.Count == 0) PaintEmptyState(dgvHistory, e.Graphics);
        };

        UpdateStats(null);
        UpdateTitle();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14, 12, 14, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));    // نوار ابزار
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 98F));    // کارت‌های خلاصه
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));    // سرتیتر لیست
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // جدول
        root.Controls.Add(topCard, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(listHeader, 0, 2);
        root.Controls.Add(ModernReport.WrapGrid(dgvHistory), 0, 3);
        this.Controls.Add(root);
    }

    /// <summary>دکمه‌ی نوار ابزار: متن + آیکن ترسیمی، عرض متناسب با متن</summary>
    private static ModernButton ToolButton(string text, Color accent, ModernButtonStyle style, PayrollIcon icon)
    {
        var b = ModernButton.Create(text, accent, 400, 36, style);
        Color iconColor = style == ModernButtonStyle.Filled ? Color.White : ModernPaint.Darken(accent, 40);
        b.Icon = PayrollIcons.Make(icon, iconColor);
        b.IconSize = 16;
        b.Height = 36;
        b.Width = TextRenderer.MeasureText(text, b.Font).Width + 16 + 8 + 36;
        b.Margin = new Padding(4, 0, 4, 0);
        return b;
    }

    private void PrepareColumns()
    {
        if (dgvHistory.Columns.Count == 0) return;

        int idx = 0;
        foreach (var name in ColumnOrder)
            if (dgvHistory.Columns.Contains(name)) dgvHistory.Columns[name]!.DisplayIndex = idx++;

        foreach (DataGridViewColumn col in dgvHistory.Columns)
        {
            col.Width = ColumnWidths.TryGetValue(col.Name, out int w) ? w : 116;
            col.MinimumWidth = 60;
        }

        if (dgvHistory.Columns.Contains("EmployeeName"))
            dgvHistory.Columns["EmployeeName"]!.Frozen = true;

        dgvHistory.ColumnHeadersHeight = 50;

        // سربرگ رنگی: مزایا (سبز) / کسورات (قرمز) / خالص پرداختی (آبی)
        void Tint(IEnumerable<string> names, Color back, Color fore)
        {
            foreach (var n in names)
            {
                if (!dgvHistory.Columns.Contains(n)) continue;
                var st = dgvHistory.Columns[n]!.HeaderCell.Style;
                st.BackColor = back; st.SelectionBackColor = back;
                st.ForeColor = fore; st.SelectionForeColor = fore;
            }
        }
        Tint(EarningCols, ModernPaint.Blend(Color.White, ModernTheme.Success, 0.09f), ModernPaint.Darken(ModernTheme.Success, 90));
        Tint(DeductionCols, ModernPaint.Blend(Color.White, ModernTheme.Danger, 0.08f), ModernTheme.DangerText);
        Tint(new[] { "NetPay" }, ModernPaint.Blend(Color.White, ModernTheme.Primary, 0.16f), ModernPaint.Darken(ModernTheme.Primary, 80));

        if (dgvHistory.Columns.Contains("NetPay"))
            dgvHistory.Columns["NetPay"]!.DefaultCellStyle.BackColor = ModernPaint.Blend(Color.White, ModernTheme.Success, 0.07f);
    }

    private string PeriodText()
    {
        int y = _loadedYear, m = _loadedMonth;
        if (!_loaded && cmbYear.SelectedItem is not null && cmbMonth.SelectedItem is not null)
        {
            y = Convert.ToInt32(cmbYear.SelectedItem);
            m = Convert.ToInt32(cmbMonth.SelectedItem);
        }
        return $"{TranslationHelper.GetMonthName(m)} {StringHelper.ConvertToPersianDigits(y.ToString())}";
    }

    private void UpdateTitle()
    {
        if (!_loaded)
        {
            _title.Title = "لیست فیش‌های حقوقی";
            _title.Sub = "";
            return;
        }
        int n = _currentData?.Rows.Count ?? 0;
        _title.Title = $"فیش‌های حقوقی {PeriodText()}";
        _title.Sub = n == 0 ? "" : $"{StringHelper.ConvertToPersianDigits(n.ToString())} فیش";
    }

    private void PaintEmptyState(DataGridView dgv, Graphics g)
    {
        int top = dgv.ColumnHeadersHeight;
        var area = new Rectangle(0, top, dgv.ClientSize.Width, Math.Max(0, dgv.ClientSize.Height - top - 24));
        if (area.Width < 200 || area.Height < 150) return;

        string title = _loaded
            ? $"برای {PeriodText()} فیشی ذخیره نشده است"
            : "هنوز فهرستی بارگذاری نشده است";
        string hint = _loaded
            ? "حقوق این ماه را از فرم «پردازش حقوق ماهانه» محاسبه و ذخیره کنید."
            : "سال و ماه را انتخاب کنید و دکمه‌ی «نمایش لیست» را بزنید.";

        g.SmoothingMode = SmoothingMode.AntiAlias;
        int d = 76;
        int cx = area.X + area.Width / 2;
        int cy = area.Y + area.Height / 2 - 34;
        var circle = new Rectangle(cx - d / 2, cy - d / 2, d, d);
        using (var br = new SolidBrush(ModernPaint.Blend(Color.White, ModernTheme.Primary, 0.12f)))
            g.FillEllipse(br, circle);

        // آیکن لیست داخل دایره
        using (var pen = new Pen(ModernPaint.Darken(ModernTheme.Primary, 20), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            for (int i = 0; i < 3; i++)
            {
                int y = cy - 11 + i * 11;
                g.DrawLine(pen, cx - 4, y, cx + 14, y);
                g.DrawLine(pen, cx - 15, y, cx - 12, y);
            }
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.Top;
        var titleRect = new Rectangle(area.X + 20, circle.Bottom + 16, area.Width - 40, 28);
        TextRenderer.DrawText(g, title, ModernTheme.GetFont(12.5f, FontStyle.Bold), titleRect, ModernTheme.TextStrong, flags);
        var hintRect = new Rectangle(area.X + 20, titleRect.Bottom + 4, area.Width - 40, 44);
        TextRenderer.DrawText(g, hint, ModernTheme.GetFont(10f), hintRect, ModernTheme.TextMuted, flags);
    }

    // ═══════════════════════════════════════════════════════════════
    // داده
    // ═══════════════════════════════════════════════════════════════
    private static decimal SumColumn(DataTable dt, string col)
        => dt.Columns.Contains(col)
            ? dt.AsEnumerable().Sum(r => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]))
            : 0m;

    private void UpdateStats(DataTable? dt)
    {
        _stCount.SetCount(dt?.Rows.Count ?? 0, "فیش");
        _stCount.SubText = _loaded ? PeriodText() : "";
        _stEarnings.SetAmount(dt is null ? 0 : SumColumn(dt, "TotalEarnings"), "ریال");
        _stDeductions.SetAmount(dt is null ? 0 : SumColumn(dt, "TotalDeductions"), "ریال");
        _stNet.SetAmount(dt is null ? 0 : SumColumn(dt, "NetPay"), "ریال");
        _stNet.SubText = _loaded ? "مبلغ قابل پرداخت به پرسنل" : "";
    }

    private async Task LoadDataSafeAsync()
    {
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null)
        {
            ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
            return;
        }

        int year = Convert.ToInt32(cmbYear.SelectedItem);
        int month = Convert.ToInt32(cmbMonth.SelectedItem);

        DataTable? result = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            result = await _repository.GetSavedPayrollsAsync(year, month);
        }, "در حال بارگذاری لیست حقوق...");

        if (!success || result is null) return;

        _currentData = result;
        _loaded = true;
        _loadedYear = year;
        _loadedMonth = month;

        if (_currentData.Rows.Count == 0)
        {
            // پیام توضیحی را خود جدول (حالت خالی) نشان می‌دهد؛ فقط یک اعلان کوتاه
            ToastHelper.Show("برای این ماه هیچ فیش حقوقی ذخیره نشده است.", isSuccess: false);
            btnExport.Enabled = false;
            btnDeleteMonth.Enabled = false;
            dgvHistory.DataSource = null;
            UpdateStats(null);
        }
        else
        {
            dgvHistory.DataSource = _currentData;
            UpdateStats(_currentData);
            btnExport.Enabled = true;
            btnDeleteMonth.Enabled = true;
        }

        UpdateTitle();
        dgvHistory.Invalidate();
    }

    private async Task ExportDataSafeAsync()
    {
        if (_currentData is null || _currentData.Rows.Count == 0) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"Payroll_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx",
            Title = "ذخیره فایل اکسل حقوق",
            OverwritePrompt = true
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await Task.Yield();
            ExcelExportHelper.ExportDataGridViewToExcel(dgvHistory, sfd.FileName, "لیست حقوق");
        }, "در حال تولید فایل اکسل...");

        if (success)
        {
            ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
        }
    }

    private async void BtnDeleteRow_Click(object? sender, EventArgs e)
    {
        if (dgvHistory.CurrentRow is null) return;

        var payrollId = Convert.ToInt32(dgvHistory.CurrentRow.Cells["Id"].Value);
        string empName = dgvHistory.CurrentRow.Cells["EmployeeName"].Value?.ToString() ?? "";

        if (!ModernMessageBox.AskConfirm(
            $"آیا از حذف فیش حقوقی «{empName}» مطمئن هستید؟\n\n" +
            "توجه: با حذف این فیش، مبالغ کسر شده بابت وام و بیمه مجدداً به مانده بدهی کارمند اضافه می‌شوند.",
            "حذف فیش حقوقی"))
            return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _repository.DeletePayrollAsync(payrollId);
        }, "در حال حذف فیش...");

        if (success)
        {
            ToastHelper.Show("فیش حقوقی با موفقیت حذف شد.", isSuccess: true);
            await LoadDataSafeAsync();
        }
    }

    private async void BtnDeleteMonth_Click(object? sender, EventArgs e)
    {
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null) return;

        int year = Convert.ToInt32(cmbYear.SelectedItem);
        int month = Convert.ToInt32(cmbMonth.SelectedItem);
        string monthName = TranslationHelper.GetMonthName(month);

        if (!ModernMessageBox.AskConfirm(
            $"⚠️ هشدار جدی!\n\n" +
            $"آیا از حذف کلیه فیش‌های حقوقی {monthName} {year} مطمئن هستید؟\n\n" +
            "توجه: با حذف این فیش‌ها، مبالغ کسر شده بابت وام و بیمه مجدداً به مانده بدهی کارمندان اضافه می‌شوند.",
            "حذف گروهی فیش‌ها"))
            return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _repository.DeletePayrollsForMonthAsync(year, month);
        }, "در حال حذف گروهی فیش‌ها...");

        if (success)
        {
            ToastHelper.Show("تمام فیش‌های این ماه با موفقیت حذف شدند.", isSuccess: true);
            await LoadDataSafeAsync();
        }
    }
}

/// <summary>سرتیتر بخش: عنوان درشت در راست و توضیح کم‌رنگ کنار آن</summary>
internal sealed class HistorySectionTitle : Control
{
    private string _title = string.Empty;
    private string _sub = string.Empty;

    public HistorySectionTitle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        RightToLeft = RightToLeft.Yes;
    }

    public string Title { get => _title; set { _title = value ?? string.Empty; Invalidate(); } }
    public string Sub { get => _sub; set { _sub = value ?? string.Empty; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernPaint.BehindColor(this));

        const TextFormatFlags measure = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var flags = TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        var titleFont = ModernTheme.GetFont(12f, FontStyle.Bold);
        int tw = TextRenderer.MeasureText(g, _title, titleFont, new Size(int.MaxValue, Height), measure).Width;
        int right = Width - 4;
        TextRenderer.DrawText(g, _title, titleFont, new Rectangle(0, 0, right, Height), ModernTheme.TextStrong, flags);

        if (_sub.Length > 0)
        {
            int subRight = right - tw - 14;
            if (subRight > 20)
                TextRenderer.DrawText(g, _sub, ModernTheme.GetFont(10f), new Rectangle(0, 0, subRight, Height), ModernTheme.TextMuted, flags);
        }
    }
}
