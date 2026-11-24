using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class YearEndBonusForm : Form
{
    private readonly string _connectionString;
    private readonly EmployeeRepository _empRepository;
    private readonly SalarySettingsRepository _settingsRepository;
    private readonly YearEndBonusRepository _bonusRepository;
    private readonly YearEndBonusService _bonusService;

    private ComboBox cmbYear = null!;
    private Label lblEmpty = null!;
    private Button btnCalculate = null!, btnSave = null!;
    private DataGridView dgvResults = null!;
    private List<YearEndBonusRecord>? _currentResults;
    private ModernStatCard _stCount = null!, _stBonus = null!, _stTax = null!, _stNet = null!;

    public YearEndBonusForm(string connectionString)
    {
        _connectionString = connectionString;
        _empRepository = new EmployeeRepository(connectionString);
        _settingsRepository = new SalarySettingsRepository(connectionString);
        _bonusRepository = new YearEndBonusRepository(connectionString);
        _bonusService = new YearEndBonusService();

        BuildUI();

        this.ApplyStandardHeader("محاسبه عیدی و پاداش پایان سال", new Size(1180, 720), isDialog: false);
        ShowInTaskbar = false;

        // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن)
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        // مقداردهی اولیه سال جاری شمسی
        LoadDefaults();
    }

    /// <summary>
    /// عرض ستون‌ها: حالت Fill با حداقل عرض مطمئن؛ اگر پنجره کوچک بود به‌جای بریده‌شدن، اسکرول افقی می‌آید.
    /// </summary>
    private void FitResultColumns()
    {
        if (dgvResults is null || dgvResults.Columns.Count == 0) return;

        // name → (وزن Fill, حداقل عرض پیکسل)
        var spec = new (string Name, float Weight, int MinWidth)[]
        {
            ("EmployeeName", 28f, 270),   // آواتار + نام کامل
            ("DaysWorked",   10f,  95),
            ("MonthsWorked", 10f,  95),
            ("BaseSalary",   17f, 160),
            ("BonusAmount",  17f, 160),
            ("Tax",          15f, 150),
            ("NetBonus",     17f, 160),
        };

        dgvResults.SuspendLayout();
        dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (var (name, weight, minWidth) in spec)
        {
            if (!dgvResults.Columns.Contains(name)) continue;
            var col = dgvResults.Columns[name];
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            col.FillWeight = weight;
            col.MinimumWidth = minWidth;
            col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        }
        dgvResults.ScrollBars = ScrollBars.Both;
        dgvResults.ResumeLayout();
    }

    private void LoadDefaults()
    {
        var pc = new PersianCalendar();
        int currentYear = pc.GetYear(DateTime.Now);

        if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
        else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
    }

    private void BuildUI()
    {
        // ---- ورودی‌ها ----
        cmbYear = ModernUI.Combo();
        ComboBoxHelper.FillYears(cmbYear);
        cmbYear.Anchor = AnchorStyles.None;
        cmbYear.Size = new Size(130, ModernUI.InputHeight);

        var lblYear = new Label
        {
            Text = "سال مالی", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.TextMuted, BackColor = Color.Transparent
        };

        btnCalculate = ModernButton.Create("🧮  محاسبه عیدی همه پرسنل", ModernTheme.Success, 230, 36);
        btnCalculate.Anchor = AnchorStyles.None;
        btnCalculate.Click += BtnCalculate_Click;

        btnSave = ModernButton.Soft("💾  ذخیره در دیتابیس", ModernTheme.Primary, 190, 36);
        btnSave.Anchor = AnchorStyles.None;
        btnSave.Enabled = false;
        btnSave.Click += BtnSave_Click;

        // ---- نوار بالا: ستون ۰ = راست‌ترین ----
        var bar = new BufferedTable
        {
            Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
            RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));    // برچسب سال
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));   // سال
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));   // محاسبه
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));    // فاصله
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));   // ذخیره
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        bar.Controls.Add(lblYear, 0, 0);
        bar.Controls.Add(cmbYear, 1, 0);
        bar.Controls.Add(btnCalculate, 2, 0);
        bar.Controls.Add(btnSave, 4, 0);

        var topCard = new ModernCard("", ModernTheme.Primary, 1) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
        topCard.Body.Padding = new Padding(14, 6, 20, 6);
        topCard.Body.Controls.Add(bar, 0, 0);

        // ---- کارت‌های خلاصه ----
        var strip = ModernReport.CreateStatStrip(4, 96);
        _stCount = strip.Add("تعداد پرسنل", ModernTheme.Primary, "دارای کارکرد در سال");
        _stBonus = strip.Add("جمع مبلغ عیدی", ModernTheme.Teal, "قبل از کسر مالیات");
        _stTax = strip.Add("جمع مالیات", ModernTheme.Danger, "مالیات عیدی");
        _stNet = strip.Add("جمع خالص عیدی", ModernTheme.Success, "مبلغ قابل پرداخت");
        foreach (var c in new[] { _stCount, _stBonus, _stTax, _stNet }) c.ValueFontSize = 14f;
        UpdateStats(null);

        // ---- جدول ----
        dgvResults = new DataGridView { Dock = DockStyle.Fill };
        dgvResults.ApplyModernStyle("EmployeeId", "Year");
        dgvResults.EnableThousandsFormatting(includeIntegers: true);
        dgvResults.UseAiryLook(46);
        dgvResults.UseSelectionAccent(ModernTheme.Success);
        dgvResults.UsePersonColumn("EmployeeName");
        // عرض ستون‌ها بعد از هر بایند (حالت DisplayedCells عرض آواتار و ارقام فارسی را کم‌تر اندازه می‌گرفت)
        dgvResults.DataBindingComplete += (s, e) => FitResultColumns();
        dgvResults.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle is null) return;
            string col = dgvResults.Columns[e.ColumnIndex].Name;
            if (col == "NetBonus") { e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold); e.CellStyle.ForeColor = ModernTheme.Success; }
            else if (col == "Tax") e.CellStyle.ForeColor = ModernTheme.DangerText;
        };

        // پیام حالت خالی
        lblEmpty = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = ModernTheme.Surface,
            Text = "🎁\n\nسال مالی را انتخاب کنید و روی «محاسبه عیدی همه پرسنل» بزنید.\nپس از بررسی نتیجه، می‌توانید آن را در دیتابیس ذخیره کنید."
        };
        var gridHost = ModernReport.WrapGrid(dgvResults);
        gridHost.Controls.Add(lblEmpty);
        lblEmpty.BringToFront();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16, 14, 16, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        strip.Dock = DockStyle.Fill;
        strip.Padding = Padding.Empty;
        strip.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(topCard, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        Controls.Add(root);
    }

    private void UpdateStats(List<YearEndBonusRecord>? list)
    {
        _stCount.SetCount(list?.Count ?? 0, "نفر");
        _stBonus.SetAmount(list?.Sum(r => r.BonusAmount) ?? 0, "ریال");
        _stTax.SetAmount(list?.Sum(r => r.Tax) ?? 0, "ریال");
        _stNet.SetAmount(list?.Sum(r => r.NetBonus) ?? 0, "ریال");
    }

    // ==========================================================
    // 🧮 محاسبه عیدی (Async + موازی)
    // ==========================================================
    private async void BtnCalculate_Click(object? sender, EventArgs e)
    {
        if (cmbYear.SelectedItem is null)
        {
            ModernMessageBox.ShowWarning("لطفاً سال مالی را انتخاب کنید.");
            return;
        }

        int year = Convert.ToInt32(cmbYear.SelectedItem);
        btnSave.Enabled = false;

        List<YearEndBonusRecord>? calculatedResults = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            // ۱. دریافت تنظیمات
            var settings = await _settingsRepository.GetByYearAsync(year);
            if (settings is null)
                throw new InvalidOperationException($"تنظیمات حقوق برای سال {year} یافت نشد. ابتدا از منوی «تنظیمات حقوق سالانه» مبالغ را وارد کنید.");

            // پله‌های مالیاتی (برای معافیت مالیاتی عیدی = پله‌ی اول جدول)
            if (settings.TaxBrackets is null || settings.TaxBrackets.Count == 0)
                settings.TaxBrackets = new PayrollCalculatorService().GetTaxBracketsSafe(year);

            // ۲. دریافت پرسنل فعال
            var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);
            var workedByEmp = await new AttendanceRepository(_connectionString).GetWorkedDaysAsync(null, year);

            // ۳. محاسبات سنگین در پس‌زمینه
            calculatedResults = await Task.Run(() =>
            {
                var list = new List<YearEndBonusRecord>();
                foreach (var emp in employees)
                {
                    // فقط پرسنل قراردادی / ماهانه (روزمزد و ساعتی مشمول عیدی نیستند)
                    if (!YearEndBonusService.IsEligible(emp)) continue;
                    // جمع کارکرد ثبت‌شده‌ی این پرسنل در حضور و غیاب سال انتخابی (بدون ثبت = ۰ روز)
                    int worked = (workedByEmp.TryGetValue(emp.Id, out var yd) && yd.TryGetValue(year, out var d)) ? d : 0;
                    var result = _bonusService.CalculateBonus(emp, year, settings, null, worked);
                    if (result.DaysWorked > 0) // اگر در آن سال کارکرد داشته
                    {
                        list.Add(result);
                    }
                }
                return list;
            });
        }, "در حال محاسبه عیدی و پاداش...");

        if (!success || calculatedResults is null) return;

        _currentResults = calculatedResults;
        lblEmpty.Visible = _currentResults.Count == 0;
        if (_currentResults.Count == 0)
            lblEmpty.Text = $"📭\n\nبرای سال {StringHelper.ConvertToPersianDigits(year.ToString())} پرسنلی با کارکرد یافت نشد.";
        dgvResults.DataSource = null;
        dgvResults.DataSource = _currentResults;
        UpdateStats(_currentResults);

        if (dgvResults.Columns.Contains("NetBonus")) dgvResults.Columns["NetBonus"].HeaderText = "خالص عیدی";
        if (dgvResults.Columns.Contains("BonusAmount")) dgvResults.Columns["BonusAmount"].HeaderText = "مبلغ عیدی";

        btnSave.Enabled = true;
        ToastHelper.Show("محاسبات انجام شد. حالا می‌توانید ذخیره کنید.", isSuccess: true);
    }

    // ==========================================================
    // 💾 ذخیره در دیتابیس (Async)
    // ==========================================================
    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_currentResults is null || _currentResults.Count == 0) return;

        if (!ModernMessageBox.AskConfirm("آیا از ذخیره عیدی و پاداش برای این پرسنل مطمئن هستید؟", "ذخیره"))
            return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _bonusRepository.SaveBonusesBatchAsync(_currentResults);
        }, "در حال ذخیره در دیتابیس...");

        if (success)
        {
            ToastHelper.Show("عیدی و پاداش پایان سال با موفقیت ذخیره شد.", isSuccess: true);
            btnSave.Enabled = false; // جلوگیری از ذخیره مجدد
        }
    }
}
