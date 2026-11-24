using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class SettlementForm : Form
{
    private readonly EmployeeRepository _empRepository;
    private readonly LeaveRepository _leaveRepository;
    private readonly LoanRepository _loanRepository;
    private readonly SettlementRepository _settlementRepository;
    private readonly SettlementCalculatorService _calculator;
    private readonly SalarySettingsRepository _settingsRepository;
    private readonly EmployeeContractRepository _contractRepository;
    private readonly AttendanceRepository _attendanceRepository;

    private EmployeePickerField pickEmployee = null!;
    private Button btnCalculate = null!, btnSave = null!;
    private Label lblNet = null!, lblEmpInfo = null!;
    private ModernStatCard _stYears = null!, _stService = null!, _stLeave = null!, _stSeverance = null!, _stLoan = null!;

    private SettlementRecord? _currentSettlement;

    // ✅ FIX P2: جلوگیری از تداخل و کرش
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public SettlementForm(string connectionString)
    {
        _empRepository = new EmployeeRepository(connectionString);
        _leaveRepository = new LeaveRepository(connectionString);
        _loanRepository = new LoanRepository(connectionString);
        _settlementRepository = new SettlementRepository(connectionString);
        _calculator = new SettlementCalculatorService();
        _settingsRepository = new SalarySettingsRepository(connectionString);
        _contractRepository = new EmployeeContractRepository(connectionString);
        _attendanceRepository = new AttendanceRepository(connectionString);

        BuildUI();

        this.ApplyStandardHeader("تسویه حساب پرسنل", new Size(980, 640), isDialog: false);
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Shown += async (s, e) => await LoadEmployeesSafeAsync();
    }

    // ✅ FIX P2: آزادسازی منابع
    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "SettlementForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ─── کارت انتخاب کارمند (ذره‌بین) ───
        pickEmployee = new EmployeePickerField { DialogTitle = "انتخاب کارمند برای تسویه حساب", AccentColor = ModernTheme.Primary, IncludeInactive = false };
        pickEmployee.SelectedChanged += (s, e) => OnEmployeeChanged();

        lblEmpInfo = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(8, 0, 8, 0),
            Text = "برای شروع، با کلیک روی ذره‌بین کارمند موردنظر را جستجو و انتخاب کنید."
        };

        var empCard = new ModernCard("👤  کارمند", ModernTheme.Primary, 1, ModernUI.RowHeight, 34);
        empCard.Body.Controls.Add(ModernUI.Row("انتخاب کارمند", pickEmployee), 0, 0);
        empCard.Body.Controls.Add(lblEmpInfo, 0, 1);

        // ─── کارت‌های نتیجه ───
        var strip = ModernReport.CreateStatStrip(5, 92);
        _stYears = strip.Add("سال‌های کارکرد", ModernTheme.Primary);
        _stService = strip.Add("مبلغ سنوات", ModernTheme.Teal);
        _stLeave = strip.Add("بازخرید مرخصی", ModernTheme.Purple);
        _stSeverance = strip.Add("عیدی / پاداش پایان سال", ModernTheme.Orange);
        _stLoan = strip.Add("مانده وام (کسر می‌شود)", ModernTheme.Danger);
        ResetResults();

        // ─── کارت خالص تسویه ───
        var netCard = new ModernCard("", ModernTheme.Success, 1, 64)
        {
            FillColor = ModernTheme.SuccessSoft,
            BorderColor = ModernPaint.Tint(ModernTheme.Success, 40)
        };
        lblNet = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(17f, FontStyle.Bold),
            ForeColor = ModernPaint.Darken(ModernTheme.Success, 45),
            BackColor = Color.Transparent,
            Name = "lblNet"
        };
        netCard.Body.Controls.Add(lblNet, 0, 0);
        lblNet.Text = "خالص تسویه حساب:  ۰ ریال";

        // ─── دکمه‌ها ───
        btnCalculate = ModernButton.Create("🧮  محاسبه تسویه", ModernTheme.Primary, 190, 44);
        btnCalculate.Click += async (s, e) => await CalculateSafeAsync();

        btnSave = ModernButton.Create("ثبت نهایی و غیرفعال‌سازی", ModernTheme.Success, 240, 44);
        btnSave.Enabled = false;
        btnSave.Click += async (s, e) => await SaveSettlementSafeAsync();

        var btnClose = ModernButton.Cancel("بستن", 110, 44);
        btnClose.Click += (s, e) => Close();

        var bar = ModernForm.CreateButtonBar(btnCalculate, btnSave, btnClose);
        bar.Dock = DockStyle.Fill;

        // ─── چیدمان ───
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16, 12, 16, 0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, empCard.Height + 12));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, netCard.Height + 14));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        empCard.Dock = DockStyle.Fill;
        strip.Dock = DockStyle.Fill;
        netCard.Dock = DockStyle.Fill;
        root.Controls.Add(empCard, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(netCard, 0, 2);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 3);
        root.Controls.Add(bar, 0, 4);
        Controls.Add(root);
    }

    private void ResetResults()
    {
        _stYears.Text = "---";
        _stService.SetAmount(0, "ریال");
        _stLeave.SetAmount(0, "ریال");
        _stSeverance.SetAmount(0, "ریال");
        _stLoan.SetAmount(0, "ریال");
    }

    private void OnEmployeeChanged()
    {
        _currentSettlement = null;
        btnSave.Enabled = false;
        ResetResults();
        lblNet.Text = "خالص تسویه حساب:  ۰ ریال";

        var emp = pickEmployee.Selected;
        if (emp is null)
        {
            lblEmpInfo.Text = "برای شروع، با کلیک روی ذره‌بین کارمند موردنظر را جستجو و انتخاب کنید.";
            return;
        }
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(emp.Position)) parts.Add("سمت: " + emp.Position);
        if (!string.IsNullOrWhiteSpace(emp.DepartmentName)) parts.Add("واحد: " + emp.DepartmentName);
        if (emp.HireDate.HasValue) parts.Add("تاریخ استخدام: " + StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(emp.HireDate.Value)));
        lblEmpInfo.Text = parts.Count > 0 ? string.Join("   •   ", parts) : "اطلاعات تکمیلی ثبت نشده است.";
    }

    private async Task LoadEmployeesSafeAsync()
    {
        if (IsDisposed) return;

        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);
                if (IsDisposed) return;
                pickEmployee.SetEmployees(employees);
            }, "در حال بارگذاری لیست پرسنل...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "SettlementForm.LoadEmployees");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async Task CalculateSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return; // ✅ FIX: جلوگیری از دابل کلیک

        try
        {
            if (pickEmployee.SelectedId is null)
            {
                ModernMessageBox.ShowWarning("لطفاً ابتدا کارمند را انتخاب کنید.");
                return;
            }

            int empId = pickEmployee.SelectedId.Value;
            SettlementRecord? result = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var emp = await Task.Run(() => _empRepository.GetById(empId));
                if (emp is null) throw new InvalidOperationException("اطلاعات کارمند یافت نشد.");

                int approvedLeaves = await _leaveRepository.GetTotalApprovedLeaveDaysAsync(empId);
                var loans = await _loanRepository.GetLoansByEmployeeIdAsync(empId);
                decimal remainingLoan = loans.Where(l => l.IsActive).Sum(l => l.RemainingAmount);

                // تنظیمات حقوق سال جاری شمسی (برای مبنای سنوات و عیدی)؛ اگر نبود، فقط حقوق پایه
                int persianYear = new System.Globalization.PersianCalendar().GetYear(DateTime.Now);
                var salarySettings = await _settingsRepository.GetByYearAsync(persianYear);

                var workedByYear = await _attendanceRepository.GetWorkedDaysAsync(empId);
                var contracts = await _contractRepository.GetContractsByEmployeeIdAsync(empId);
                result = _calculator.CalculateSettlement(emp, approvedLeaves, remainingLoan, DateTime.Now, salarySettings, contracts,
                    workedByYear.TryGetValue(empId, out var wy) ? wy : null);
            }, "در حال محاسبه تسویه حساب...");

            // ✅ FIX: بررسی IsDisposed بعد از await
            if (!success || result is null || IsDisposed) return;

            _currentSettlement = result;

            _stYears.SetCount(result.YearsOfWork, "سال");
            _stService.SetAmount(result.ServiceBonus, "ریال");
            _stLeave.SetAmount(result.LeaveBonus, "ریال");
            _stSeverance.SetAmount(result.SeverancePay, "ریال");
            _stLoan.SetAmount(result.RemainingLoan, "ریال");

            lblNet.Text = "خالص تسویه حساب:  " + NumberHelper.FormatThousands(result.NetSettlement, usePersianDigits: true) + " ریال";

            btnSave.Enabled = true;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "SettlementForm.CalculateSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در محاسبه:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "SettlementForm"); }
        }
    }

    private async Task SaveSettlementSafeAsync()
    {
        if (_currentSettlement is null || IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return; // ✅ FIX: جلوگیری از دابل کلیک

        try
        {
            if (!ModernMessageBox.AskConfirm("آیا از ثبت تسویه حساب و غیرفعال کردن این کارمند مطمئن هستید؟", "ثبت تسویه"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _settlementRepository.SaveSettlementAsync(_currentSettlement);

                var emp = await Task.Run(() => _empRepository.GetById(_currentSettlement.EmployeeId));
                if (emp is not null)
                {
                    emp.IsActive = false;
                    await _empRepository.UpdateAsync(emp);
                }
            }, "در حال ثبت تسویه حساب...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("تسویه حساب با موفقیت ثبت شد و کارمند غیرفعال گردید.", isSuccess: true);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "SettlementForm.SaveSettlementSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ثبت تسویه:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "SettlementForm"); }
        }
    }
}
