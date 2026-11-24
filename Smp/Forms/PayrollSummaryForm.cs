using ClosedXML.Excel;
using Smp.Data;
using Smp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PayrollSummaryForm : Form
{
    private readonly PayrollRepository _repository;

    private ComboBox cmbYear = null!, cmbMonth = null!;
    private ModernButton btnLoad = null!, btnExport = null!;
    private DataGridView dgvDetails = null!;
    private DataTable? _currentData;

    private ModernStatCard statGross = null!, statInsEmp = null!, statInsEmpr = null!, statTax = null!, statNet = null!;

    public PayrollSummaryForm(string connectionString)
    {
        _repository = new PayrollRepository(connectionString);

        BuildUI();
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

    private void BuildUI()
    {
        // ═══════════ ۱. کارت‌های خلاصه ═══════════
        var strip = ModernReport.CreateStatStrip(5, 76);
        statGross   = strip.Add("جمع کل حقوق و مزایا", ModernTheme.Primary);
        statInsEmp  = strip.Add("بیمه سهم کارمند", ModernTheme.Orange);
        statInsEmpr = strip.Add("بیمه سهم کارفرما", ModernTheme.Purple);
        statTax     = strip.Add("جمع مالیات", ModernTheme.Danger);
        statNet     = strip.Add("خالص پرداختی", ModernTheme.Success);
        ResetLabels();
        strip.Dock = DockStyle.Fill;

        // ═══════════ ۲. نوار انتخاب سال و ماه ═══════════
        cmbYear = ModernUI.Combo();
        ComboBoxHelper.FillYears(cmbYear);

        cmbMonth = ModernUI.Combo();
        ComboBoxHelper.FillMonths(cmbMonth);

        btnLoad = ModernButton.Primary("نمایش داشبورد", 150, 40);
        btnLoad.Click += async (s, e) => await LoadDataSafeAsync();

        var toolbar = ModernListKit.BuildToolbar(
            (ModernListKit.FilterLabel("سال"), 0), (cmbYear, 130),
            (ModernListKit.FilterLabel("ماه"), 0), (cmbMonth, 150),
            (btnLoad, 170));

        // ═══════════ ۳. جدول (ستون‌ها از کوئری ساخته می‌شوند) ═══════════
        dgvDetails = ModernGrid.Create();
        dgvDetails.AutoGenerateColumns = true;
        dgvDetails.FillColumns();
        dgvDetails.UseAiryLook(44);
        dgvDetails.UseSelectionAccent(ModernTheme.Primary);
        dgvDetails.EnableThousandsFormatting(includeIntegers: true);

        // ═══════════ ۴. دکمه‌ی پایین ═══════════
        btnExport = ModernButton.Success("خروجی اکسل", 160, 40);
        btnExport.Enabled = false;
        btnExport.Click += async (s, e) => await ExportDataSafeAsync();
        var actions = ModernListKit.BuildActionBar(new Control[] { btnExport }, new Control[0]);

        ModernListKit.ComposeRows(this, "داشبورد مالی و بیمه",
            "جمع حقوق، بیمه و مالیات ثبت‌شده در فیش‌های حقوقی هر ماه", new Size(1200, 720),
            (strip, 88), (toolbar, 56), (ModernReport.WrapGrid(dgvDetails), 0), (actions, 64));
    }


    // ✅ FIX: استفاده از نام ستون‌های انگلیسی برگشتی از GetSavedPayrollsAsync
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
        }, "در حال بارگذاری اطلاعات...");

        if (!success || result is null) return;

        _currentData = result;

        if (_currentData.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("برای این ماه اطلاعاتی ذخیره نشده است.");
            ResetLabels();
            btnExport.Enabled = false;
            dgvDetails.DataSource = null;
        }
        else
        {
            dgvDetails.DataSource = _currentData;

            // ✅ FIX: نام ستون‌ها = نام‌های واقعی از دیتابیس (چون کوئری p.* است)
            decimal totalGross = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("TotalEarnings") ?? 0m);
            decimal totalInsEmp = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("InsuranceEmployee") ?? 0m);
            decimal totalInsEmpr = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("InsuranceEmployer") ?? 0m);
            decimal totalTax = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("IncomeTax") ?? 0m);
            decimal totalNet = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("NetPay") ?? 0m);

            statGross.SetAmount(totalGross, "ریال");
            statInsEmp.SetAmount(totalInsEmp, "ریال");
            statInsEmpr.SetAmount(totalInsEmpr, "ریال");
            statTax.SetAmount(totalTax, "ریال");
            statNet.SetAmount(totalNet, "ریال");

            btnExport.Enabled = true;
        }
    }

    private void ResetLabels()
    {
        statGross.SetAmount(0, "ریال"); statInsEmp.SetAmount(0, "ریال");
        statInsEmpr.SetAmount(0, "ریال"); statTax.SetAmount(0, "ریال"); statNet.SetAmount(0, "ریال");
    }

    private async Task ExportDataSafeAsync()
    {
        if (_currentData is null || _currentData.Rows.Count == 0) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"PayrollSummary_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx",
            Title = "ذخیره گزارش مالی",
            OverwritePrompt = true
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await Task.Yield();
            ExcelExportHelper.ExportDataGridViewToExcel(dgvDetails, sfd.FileName, "گزارش مالی");
        }, "در حال تولید فایل اکسل...");

        if (success)
        {
            ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
        }
    }
}
