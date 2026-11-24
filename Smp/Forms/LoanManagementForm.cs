using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class LoanManagementForm : Form
{
    private readonly LoanRepository _repository;
    private readonly EmployeeRepository _empRepository;

    private EmployeePickerField pickEmployee = null!;
    private int? _selectedEmployeeId = null;
    private List<Employee> _allEmployees = new();
    private List<EmployeeLoan> _loans = new();

    private ComboBox cmbLoanType = null!, cmbStartMonth = null!;
    private NumericUpDown numStartYear = null!, numInstallments = null!;
    private ModernTextBox txtAmount = null!, txtDescription = null!, txtSearch = null!;
    private Label lblMonthlyInstallment = null!;
    private Button btnSave = null!, btnDelete = null!, btnNew = null!, btnReactivate = null!;
    private DataGridView dgvLoans = null!;
    private ModernStatCard _statCount = null!, _statRemaining = null!, _statInstallment = null!;
    private ModernCard _inputCard = null!;
    private CheckBox chkShowSettled = null!;

    private EmployeeLoan? _editingLoan = null;
    private bool _isInitializing = true;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public LoanManagementForm(string connectionString)
    {
        _repository = new LoanRepository(connectionString);
        _empRepository = new EmployeeRepository(connectionString);

        BuildUI();
        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LoanManagementForm"); }
            try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LoanManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        ModernForm.Setup(this, "مدیریت وام و مساعده پرسنل", new Size(1180, 840), isDialog: false);
        ShowInTaskbar = false;
        int rh = ModernUI.RowHeight;

        // ─── کارت‌های خلاصه ───
        var strip = ModernReport.CreateStatStrip(3, 84);
        _statCount = strip.Add("وام و مساعده‌ی فعال", ModernTheme.Primary);
        _statRemaining = strip.Add("جمع مانده‌ی بدهی", ModernTheme.Danger);
        _statInstallment = strip.Add("جمع اقساط ماهانه", ModernTheme.Success);

        // ─── ورودی‌ها ───
        pickEmployee = new EmployeePickerField { DialogTitle = "انتخاب کارمند برای وام / مساعده", AccentColor = ModernTheme.Primary };
        pickEmployee.SelectedChanged += (s, e) => _selectedEmployeeId = pickEmployee.SelectedId;

        cmbLoanType = ModernUI.Combo();
        cmbLoanType.Items.AddRange(new object[] { "وام", "مساعده" });
        cmbLoanType.SelectedIndex = 0;
        cmbLoanType.SelectedIndexChanged += (s, e) => OnLoanTypeChanged();

        txtAmount = ModernUI.Money("ریال");
        txtAmount.TextChanged += (s, e) => CalculateInstallment();

        numInstallments = new NumericUpDown { Minimum = 1, Maximum = 120, Value = 12 };
        numInstallments.ValueChanged += (s, e) => CalculateInstallment();

        var pc = new PersianCalendar();
        numStartYear = new NumericUpDown { Minimum = 1400, Maximum = 1450, Value = pc.GetYear(DateTime.Now) };

        cmbStartMonth = ModernUI.Combo();
        for (int i = 1; i <= 12; i++) cmbStartMonth.Items.Add(i);
        cmbStartMonth.SelectedItem = pc.GetMonth(DateTime.Now);

        lblMonthlyInstallment = new Label
        {
            Text = "مبلغ هر قسط: 0 ریال",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f, FontStyle.Bold),
            ForeColor = ModernPaint.Darken(ModernTheme.Success, 45),
            BackColor = ModernTheme.SuccessSoft,
            Margin = new Padding(5, 4, 5, 4)
        };
        txtDescription = ModernUI.Input(placeholder: "توضیحات (اختیاری)");

        // ─── کارت فرم ───
        _inputCard = new ModernCard("✏️  ثبت / ویرایش وام یا مساعده", ModernTheme.Orange, 2, rh, rh, rh, rh, rh);
        var body = _inputCard.Body;

        var rowEmp = ModernUI.Row("کارمند", pickEmployee);
        body.Controls.Add(rowEmp, 0, 0);
        body.SetColumnSpan(rowEmp, 2);

        body.Controls.Add(ModernUI.Row("نوع", cmbLoanType), 0, 1);
        body.Controls.Add(ModernUI.Row("مبلغ کل", txtAmount), 1, 1);

        body.Controls.Add(ModernUI.Row("تعداد اقساط", numInstallments), 0, 2);
        body.Controls.Add(ModernUI.Row("مبلغ محاسبه‌شده", lblMonthlyInstallment), 1, 2);

        body.Controls.Add(ModernUI.Row("سال شروع کسر", numStartYear), 0, 3);
        body.Controls.Add(ModernUI.Row("ماه شروع کسر", cmbStartMonth), 1, 3);

        var rowDesc = ModernUI.Row("توضیحات", txtDescription);
        body.Controls.Add(rowDesc, 0, 4);
        body.SetColumnSpan(rowDesc, 2);

        // ─── جستجوی جدول ───
        txtSearch = ModernUI.Input(placeholder: "🔍  جستجوی نام کارمند در فهرست...");
        txtSearch.TextChanged += (s, e) => ApplyLoanFilter();
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
        txtSearch.Dock = DockStyle.Fill;
        txtSearch.Margin = new Padding(0, 3, 0, 3);
        searchRow.Controls.Add(txtSearch, 0, 0);

        // نمایش وام/مساعده‌های تسویه‌شده (مانده صفر) در کنار موارد فعال
        chkShowSettled = ModernUI.Check("فقط تسویه‌شده‌ها", ModernTheme.Orange);
        chkShowSettled.Width = 220;
        chkShowSettled.Dock = DockStyle.Fill;
        chkShowSettled.CheckedChanged += (s, e) => { if (!_isInitializing) { ResetForm(); ApplyLoanFilter(); } };
        searchRow.Controls.Add(chkShowSettled, 1, 0);

        // ─── جدول ───
        dgvLoans = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        dgvLoans.ApplyModernStyle();
        dgvLoans.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "کارمند", FillWeight = 28, MinimumWidth = 190 });
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "LoanType", DataPropertyName = "LoanType", HeaderText = "نوع", FillWeight = 10 });
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalAmount", DataPropertyName = "TotalAmount", HeaderText = "مبلغ کل", FillWeight = 15 });
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "MonthlyInstallment", DataPropertyName = "MonthlyInstallment", HeaderText = "مبلغ قسط", FillWeight = 14 });
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingAmount", DataPropertyName = "RemainingAmount", HeaderText = "مانده", FillWeight = 15 });
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartPeriod", DataPropertyName = "StartPeriod", HeaderText = "شروع کسر", FillWeight = 10 });
        dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 18 });

        dgvLoans.UseAiryLook(48);
        dgvLoans.UseSelectionAccent(ModernTheme.Orange);
        dgvLoans.UsePersonColumn("EmployeeName");
        dgvLoans.UseTextAlignment("Description");
        dgvLoans.UseBadges("LoanType", new Dictionary<string, Color>
        {
            ["وام"] = ModernTheme.Primary,
            ["مساعده"] = ModernTheme.Orange
        });

        dgvLoans.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvLoans.Columns[e.ColumnIndex].Name;
            if ((colName == "TotalAmount" || colName == "MonthlyInstallment" || colName == "RemainingAmount") && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
                if (colName == "RemainingAmount" && e.CellStyle is not null)
                {
                    e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
                    e.CellStyle.ForeColor = dec > 0 ? ModernTheme.DangerText : ModernTheme.Success;
                }
            }
        };
        dgvLoans.SelectionChanged += (s, e) => { try { DgvLoans_SelectionChanged(s, e); } catch (Exception ex) { LogHelper.LogException(ex, "LoanManagementForm.SelectionChanged"); } };

        // ─── دکمه‌ها ───
        btnSave = ModernButton.Create("➕  ثبت وام جدید", ModernTheme.Success, 170, 42);
        btnNew = ModernButton.Create("🔄  جدید / پاک‌سازی", ModernTheme.Neutral, 170, 42, ModernButtonStyle.Outline);
        btnDelete = ModernButton.Create("🗑  حذف انتخاب‌شده", ModernTheme.Danger, 170, 42, ModernButtonStyle.Soft);
        btnDelete.Enabled = false;
        btnReactivate = ModernButton.Create("↩️  بازگرداندن مانده", ModernTheme.Orange, 190, 42, ModernButtonStyle.Soft);
        btnReactivate.Enabled = false;
        btnReactivate.Click += async (s, e) => await ReactivateSafeAsync();

        btnSave.Click += async (s, e) => await SaveSafeAsync();
        btnNew.Click += (s, e) => ResetForm();
        btnDelete.Click += async (s, e) => await DeleteSafeAsync();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        var bar = ModernForm.CreateButtonBar(btnSave, btnNew, btnDelete, btnReactivate);
        bar.Dock = DockStyle.Fill;

        // ─── چیدمان صفحه ───
        var gridHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Color.Transparent };
        gridHost.RowStyles.Add(new RowStyle(SizeType.Absolute, ModernUI.InputHeight + 10));
        gridHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        gridHost.Controls.Add(searchRow, 0, 0);
        gridHost.Controls.Add(ModernReport.WrapGrid(dgvLoans), 0, 1);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14, 10, 14, 0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, _inputCard.Height + 10));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        strip.Dock = DockStyle.Fill;
        _inputCard.Dock = DockStyle.Fill;
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(_inputCard, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);

        this.ApplyStandardHeader("مدیریت وام و مساعده پرسنل", new Size(1180, 840), isDialog: false);
        ShowInTaskbar = false;
    }

    private void ApplyLoanFilter()
    {
        if (dgvLoans is null) return;
        string q = StringHelper.NormalizePersianText(txtSearch?.Text ?? "").Trim();
        // بدون تیک: فقط فعال‌ها — با تیک: فقط تسویه‌شده‌ها (هیچ‌وقت هر دو با هم)
        bool settledMode = chkShowSettled is { Checked: true };
        IEnumerable<EmployeeLoan> view = _loans.Where(l => IsSettled(l) == settledMode);
        if (q.Length > 0)
            view = view.Where(l => StringHelper.NormalizePersianText(l.EmployeeName ?? "").Contains(q));

        bool prev = _isInitializing;
        _isInitializing = true;
        try
        {
            dgvLoans.DataSource = view.ToList();
            dgvLoans.ClearSelection();
            dgvLoans.CurrentCell = null;
        }
        finally { _isInitializing = prev; }
        UpdateStats();
    }

    // تسویه‌شده = مانده صفر یا غیرفعال (مثلاً کسرشده در فیش حقوقی ذخیره‌شده)
    private static bool IsSettled(EmployeeLoan l) => !l.IsActive || l.RemainingAmount <= 0;

    private void UpdateStats()
    {
        if (_statCount is null) return;
        // آمار فقط موارد فعال را می‌شمارد؛ تسویه‌شده‌ها (در صورت نمایش) در آمار نیستند
        var active = _loans.Where(l => l.IsActive && l.RemainingAmount > 0).ToList();
        _statCount.SetCount(active.Count, "مورد");
        _statRemaining.SetAmount(active.Sum(l => l.RemainingAmount), "ریال");
        _statInstallment.SetAmount(active.Sum(l => l.MonthlyInstallment), "ریال");
    }

    private void SetEditMode(bool editing)
    {
        btnSave.Text = editing ? "💾  ذخیره تغییرات" : "➕  ثبت وام جدید";
        ((ModernButton)btnSave).Accent = editing ? ModernTheme.Primary : ModernTheme.Success;
    }

    private void OnLoanTypeChanged()
    {
        bool isAdvance = cmbLoanType.SelectedItem?.ToString() == "مساعده";
        numInstallments.Enabled = !isAdvance;
        if (isAdvance) numInstallments.Value = 1;
        CalculateInstallment();
    }

    private void CalculateInstallment()
    {
        decimal total = NumberHelper.ParseFormatted(txtAmount.Text);
        bool isAdvance = cmbLoanType.SelectedItem?.ToString() == "مساعده";
        int installments = (int)numInstallments.Value;

        if (total > 0)
        {
            if (isAdvance) lblMonthlyInstallment.Text = $"کسر یک‌جا: {NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال";
            else if (installments > 0)
            {
                decimal monthly = NumberHelper.Round(total / installments, 0);
                lblMonthlyInstallment.Text = $"مبلغ هر قسط: {NumberHelper.FormatThousands(monthly, usePersianDigits: true)} ریال";
            }
        }
        else lblMonthlyInstallment.Text = isAdvance ? "کسر یک‌جا: 0 ریال" : "مبلغ هر قسط: 0 ریال";
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;
        try
        {
            _isInitializing = true;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                _allEmployees = await _empRepository.GetEmployeesWithDetailsAsync("", false);
                pickEmployee.SetEmployees(_allEmployees);

                _loans = await _repository.GetAllLoansAsync();
                ApplyLoanFilter();
            }, "در حال بارگذاری اطلاعات...");
            if (!success || IsDisposed) return;
            btnDelete.Enabled = dgvLoans.CurrentRow is not null;
        }
        finally { _isInitializing = false; try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LoanManagementForm"); } }
    }

    private void DgvLoans_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isInitializing || IsDisposed) return;
        if (dgvLoans.CurrentRow?.DataBoundItem is not EmployeeLoan selectedLoan) { _editingLoan = null; btnDelete.Enabled = false; return; }
        _editingLoan = selectedLoan;

        var emp = _allEmployees.FirstOrDefault(x => x.Id == selectedLoan.EmployeeId);
        if (emp is not null) pickEmployee.SelectById(emp.Id);
        else { pickEmployee.Clear(); _selectedEmployeeId = null; }

        cmbLoanType.SelectedItem = selectedLoan.LoanType;
        txtAmount.Value = selectedLoan.TotalAmount;

        decimal calcInst = selectedLoan.MonthlyInstallment > 0 ? (int)Math.Round(selectedLoan.TotalAmount / selectedLoan.MonthlyInstallment) : 1;
        calcInst = Math.Max(numInstallments.Minimum, Math.Min(numInstallments.Maximum, calcInst));
        numInstallments.Value = calcInst;

        decimal startYear = selectedLoan.StartYear > 0 ? selectedLoan.StartYear : 1403;
        startYear = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, startYear));
        numStartYear.Value = startYear;

        if (selectedLoan.StartMonth >= 1 && selectedLoan.StartMonth <= 12) cmbStartMonth.SelectedItem = selectedLoan.StartMonth;
        txtDescription.Text = selectedLoan.Description;

        SetEditMode(true);
        btnDelete.Enabled = true;
        btnReactivate.Enabled = selectedLoan.RemainingAmount <= 0;   // فقط برای موارد تسویه‌شده
    }

    private void ResetForm()
    {
        _editingLoan = null;
        _selectedEmployeeId = null;
        pickEmployee.Clear();

        cmbLoanType.SelectedIndex = 0;
        txtAmount.Text = string.Empty;
        numInstallments.Value = 12;
        numInstallments.Enabled = true;
        txtDescription.Text = string.Empty;

        var pc = new PersianCalendar();
        numStartYear.Value = pc.GetYear(DateTime.Now);
        cmbStartMonth.SelectedItem = pc.GetMonth(DateTime.Now);

        SetEditMode(false);
        btnDelete.Enabled = false;
        btnReactivate.Enabled = false;

        if (dgvLoans.Rows.Count > 0) { dgvLoans.ClearSelection(); dgvLoans.CurrentCell = null; }
        CalculateInstallment();
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            if (!_selectedEmployeeId.HasValue) { ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید."); return; }
            decimal totalAmount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (totalAmount <= 0) { ModernMessageBox.ShowWarning("مبلغ باید بزرگتر از صفر باشد."); return; }
            if (numStartYear.Value <= 0) { ModernMessageBox.ShowWarning("سال شروع را انتخاب کنید."); return; }
            if (cmbStartMonth.SelectedItem is null) { ModernMessageBox.ShowWarning("ماه شروع را انتخاب کنید."); return; }

            string loanType = cmbLoanType.SelectedItem?.ToString() ?? "وام";
            bool isAdvance = loanType == "مساعده";
            int empId = _selectedEmployeeId.Value;
            int startYear = (int)numStartYear.Value;
            int startMonth = (int)cmbStartMonth.SelectedItem;
            decimal monthlyInstallment = isAdvance ? totalAmount : NumberHelper.Round(totalAmount / (int)numInstallments.Value, 0);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingLoan is null)
                {
                    var loan = new EmployeeLoan
                    {
                        EmployeeId = empId,
                        LoanType = loanType,
                        TotalAmount = totalAmount,
                        MonthlyInstallment = monthlyInstallment,
                        RemainingAmount = totalAmount,
                        StartYear = startYear,
                        StartMonth = startMonth,
                        LoanDate = DateTime.Now,
                        Description = txtDescription.Text.Trim()
                    };
                    await _repository.AddLoanAsync(loan);
                }
                else
                {
                    decimal paidSoFar = _editingLoan.TotalAmount - _editingLoan.RemainingAmount;
                    if (paidSoFar < 0) paidSoFar = 0;
                    _editingLoan.LoanType = loanType;
                    _editingLoan.TotalAmount = totalAmount;
                    _editingLoan.MonthlyInstallment = monthlyInstallment;
                    _editingLoan.StartYear = startYear;
                    _editingLoan.StartMonth = startMonth;
                    _editingLoan.Description = txtDescription.Text.Trim();
                    _editingLoan.RemainingAmount = Math.Max(0, totalAmount - paidSoFar);
                    await _repository.UpdateLoanAsync(_editingLoan);
                }
            }, "در حال ذخیره...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"{loanType} با موفقیت ذخیره شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "LoanManagementForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره وام:\n" + ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LoanManagementForm"); } }
    }

    private async Task ReactivateSafeAsync()
    {
        if (IsDisposed) return;
        if (_editingLoan is null) { ModernMessageBox.ShowWarning("لطفاً ابتدا یک رکورد را از جدول انتخاب کنید."); return; }
        if (!ModernMessageBox.AskConfirm(
                $"مانده‌ی {_editingLoan.LoanType} «{_editingLoan.EmployeeName}» به مبلغ کل ({NumberHelper.FormatThousands(_editingLoan.TotalAmount, usePersianDigits: true)} ریال) برگردانده و دوباره فعال می‌شود.\n\n" +
                "این کار را فقط وقتی بزنید که فیش حقوقی مربوطه حذف شده ولی مانده برنگشته است.\n\nادامه می‌دهید؟",
                "بازگرداندن مانده")) return;

        bool success = false;
        string errorMessage = "";
        try
        {
            success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.ReactivateLoanAsync(_editingLoan.Id); }, "در حال بازگردانی...");
        }
        catch (Exception ex) { errorMessage = ex.Message; }

        if (success && !IsDisposed)
        {
            ToastHelper.Show("مانده برگردانده شد و مورد دوباره فعال است.", isSuccess: true);
            ResetForm();
            _isInitializing = true;
            await LoadDataSafeAsync();
        }
        else if (!string.IsNullOrEmpty(errorMessage) && !IsDisposed) ModernMessageBox.ShowError(errorMessage, "عدم امکان بازگردانی");
    }

    private async Task DeleteSafeAsync()
    {
        if (IsDisposed) return;
        if (_editingLoan is null) { ModernMessageBox.ShowWarning("لطفاً ابتدا یک رکورد را از جدول انتخاب کنید."); return; }
        if (!ModernMessageBox.AskConfirm($"آیا از حذف {_editingLoan.LoanType} «{_editingLoan.EmployeeName}» مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(_editingLoan.TotalAmount, usePersianDigits: true)} ریال\nمانده: {NumberHelper.FormatThousands(_editingLoan.RemainingAmount, usePersianDigits: true)} ریال", "حذف")) return;

        bool success = false;
        string errorMessage = "";
        try
        {
            success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.DeleteLoanAsync(_editingLoan.Id); }, "در حال حذف...");
        }
        catch (Exception ex) { errorMessage = ex.Message; }

        if (success && !IsDisposed)
        {
            ToastHelper.Show("با موفقیت حذف شد.", isSuccess: true);
            ResetForm();
            _isInitializing = true;
            await LoadDataSafeAsync();
        }
        else if (!string.IsNullOrEmpty(errorMessage) && !IsDisposed) ModernMessageBox.ShowError(errorMessage, "عدم امکان حذف");
    }
}
