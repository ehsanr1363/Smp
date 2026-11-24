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

public class CarInsuranceManagementForm : Form
{
    private readonly CarInsuranceRepository _repository;
    private readonly EmployeeRepository _empRepository;

    private EmployeePickerField pickEmployee = null!;
    private ComboBox cmbStartMonth = null!;
    private NumericUpDown numStartYear = null!, numInstallments = null!;
    private ModernTextBox txtTotalAmount = null!, txtDescription = null!, txtSearch = null!;
    private Label lblMonthlyInstallment = null!;
    private Button btnSave = null!, btnDelete = null!, btnNew = null!;
    private DataGridView dgvInsurances = null!;
    private ModernStatCard _statCount = null!, _statRemaining = null!, _statInstallment = null!;
    private List<EmployeeCarInsurance> _items = new();
    private List<Employee> _allEmployees = new();

    private EmployeeCarInsurance? _editingInsurance = null;
    private bool _isInitializing = true;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public CarInsuranceManagementForm(string connectionString)
    {
        _repository = new CarInsuranceRepository(connectionString);
        _empRepository = new EmployeeRepository(connectionString);

        BuildUI();
        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CarInsuranceManagementForm"); }
            try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CarInsuranceManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        int rh = ModernUI.RowHeight;

        // ─── کارت‌های خلاصه ───
        var strip = ModernReport.CreateStatStrip(3, 84);
        _statCount = strip.Add("بیمه خودرو فعال", ModernTheme.Primary);
        _statRemaining = strip.Add("جمع مانده‌ی بدهی", ModernTheme.Danger);
        _statInstallment = strip.Add("جمع اقساط ماهانه", ModernTheme.Success);

        // ─── ورودی‌ها ───
        pickEmployee = new EmployeePickerField { DialogTitle = "انتخاب کارمند برای بیمه خودرو", AccentColor = ModernTheme.Primary };

        txtTotalAmount = ModernUI.Money("ریال");
        txtTotalAmount.TextChanged += (s, e) => CalculateInstallment();

        numInstallments = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 10 };
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
            BackColor = ModernTheme.SuccessSoft
        };
        txtDescription = ModernUI.Input(placeholder: "توضیحات / پلاک خودرو (اختیاری)");

        // ─── کارت فرم ───
        var card = new ModernCard("✏️  ثبت / ویرایش بیمه خودرو", ModernTheme.Teal, 2, rh, rh, rh, rh);
        var body = card.Body;

        var rowEmp = ModernUI.Row("کارمند", pickEmployee);
        body.Controls.Add(rowEmp, 0, 0);
        body.SetColumnSpan(rowEmp, 2);

        body.Controls.Add(ModernUI.Row("مبلغ کل بیمه خودرو (ریال)", txtTotalAmount), 0, 1);
        body.Controls.Add(ModernUI.Row("تعداد اقساط (حداکثر ۱۰)", numInstallments), 1, 1);

        body.Controls.Add(ModernUI.Row("سال شروع کسر", numStartYear), 0, 2);
        body.Controls.Add(ModernUI.Row("ماه شروع کسر", cmbStartMonth), 1, 2);

        body.Controls.Add(ModernUI.Row("مبلغ محاسبه‌شده", lblMonthlyInstallment), 0, 3);
        body.Controls.Add(ModernUI.Row("توضیحات / پلاک خودرو", txtDescription), 1, 3);

        // ─── جستجو ───
        txtSearch = ModernUI.Input(placeholder: "🔍  جستجوی نام کارمند در فهرست...");
        txtSearch.Dock = DockStyle.Fill;
        txtSearch.Margin = new Padding(0, 3, 0, 3);
        txtSearch.TextChanged += (s, e) => ApplyFilter();

        // ─── جدول ───
        dgvInsurances = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        dgvInsurances.ApplyModernStyle();
        dgvInsurances.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "کارمند", FillWeight = 28, MinimumWidth = 190 });
        dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalAmount", DataPropertyName = "TotalAmount", HeaderText = "مبلغ کل", FillWeight = 15 });
        dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "MonthlyInstallment", DataPropertyName = "MonthlyInstallment", HeaderText = "مبلغ قسط", FillWeight = 15 });
        dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingAmount", DataPropertyName = "RemainingAmount", HeaderText = "مانده بدهی", FillWeight = 15 });
        dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartPeriod", DataPropertyName = "StartPeriod", HeaderText = "شروع کسر", FillWeight = 10 });
        dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات / پلاک", FillWeight = 20 });

        dgvInsurances.UseAiryLook(48);
        dgvInsurances.UseSelectionAccent(ModernTheme.Teal);
        dgvInsurances.UsePersonColumn("EmployeeName");
        dgvInsurances.UseTextAlignment("Description");

        dgvInsurances.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvInsurances.Columns[e.ColumnIndex].Name;
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
        dgvInsurances.SelectionChanged += (s, e) => { try { DgvInsurances_SelectionChanged(s, e); } catch (Exception ex) { LogHelper.LogException(ex, "CarInsuranceManagementForm.SelectionChanged"); } };

        // ─── دکمه‌ها ───
        btnSave = ModernButton.Create("➕  ثبت بیمه خودرو جدید", ModernTheme.Success, 200, 42);
        btnNew = ModernButton.Create("🔄  جدید / پاک‌سازی", ModernTheme.Neutral, 170, 42, ModernButtonStyle.Outline);
        btnDelete = ModernButton.Create("🗑  حذف انتخاب‌شده", ModernTheme.Danger, 170, 42, ModernButtonStyle.Soft);
        btnDelete.Enabled = false;

        btnSave.Click += async (s, e) => await SaveSafeAsync();
        btnNew.Click += (s, e) => ResetForm();
        btnDelete.Click += async (s, e) => await DeleteSafeAsync();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        var bar = ModernForm.CreateButtonBar(btnSave, btnNew, btnDelete);
        bar.Dock = DockStyle.Fill;

        // ─── چیدمان صفحه ───
        var gridHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Color.Transparent };
        gridHost.RowStyles.Add(new RowStyle(SizeType.Absolute, ModernUI.InputHeight + 10));
        gridHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        gridHost.Controls.Add(txtSearch, 0, 0);
        gridHost.Controls.Add(ModernReport.WrapGrid(dgvInsurances), 0, 1);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14, 10, 14, 0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, card.Height + 10));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        strip.Dock = DockStyle.Fill;
        card.Dock = DockStyle.Fill;
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);

        this.ApplyStandardHeader("مدیریت بیمه خودرو پرسنل", new Size(1180, 800), isDialog: false);
        ShowInTaskbar = false;
    }

    private void ApplyFilter()
    {
        if (dgvInsurances is null) return;
        string q = StringHelper.NormalizePersianText(txtSearch?.Text ?? "").Trim();
        IEnumerable<EmployeeCarInsurance> view = _items;
        if (q.Length > 0)
            view = _items.Where(x => StringHelper.NormalizePersianText(x.EmployeeName ?? "").Contains(q));

        bool prev = _isInitializing;
        _isInitializing = true;
        try
        {
            dgvInsurances.DataSource = view.ToList();
            dgvInsurances.ClearSelection();
            dgvInsurances.CurrentCell = null;
        }
        finally { _isInitializing = prev; }
        UpdateStats();
    }

    private void UpdateStats()
    {
        if (_statCount is null) return;
        _statCount.SetCount(_items.Count, "مورد");
        _statRemaining.SetAmount(_items.Sum(x => x.RemainingAmount), "ریال");
        _statInstallment.SetAmount(_items.Where(x => x.RemainingAmount > 0).Sum(x => x.MonthlyInstallment), "ریال");
    }

    private void SetEditMode(bool editing)
    {
        btnSave.Text = editing ? "💾  ذخیره تغییرات" : "➕  ثبت بیمه خودرو جدید";
        ((ModernButton)btnSave).Accent = editing ? ModernTheme.Primary : ModernTheme.Success;
    }

    private void CalculateInstallment()
    {
        decimal total = NumberHelper.ParseFormatted(txtTotalAmount.Text);
        int installments = (int)numInstallments.Value;
        if (total > 0 && installments > 0)
        {
            decimal monthly = NumberHelper.Round(total / installments, 0);
            lblMonthlyInstallment.Text = $"مبلغ هر قسط: {NumberHelper.FormatThousands(monthly, usePersianDigits: true)} ریال";
        }
        else lblMonthlyInstallment.Text = "مبلغ هر قسط: 0 ریال";
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
                _items = await _repository.GetActiveCarInsurancesAsync();
                ApplyFilter();
            }, "در حال بارگذاری اطلاعات...");
            if (!success || IsDisposed) return;
            btnDelete.Enabled = dgvInsurances.CurrentRow is not null;
        }
        finally { _isInitializing = false; try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CarInsuranceManagementForm"); } }
    }

    private void DgvInsurances_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isInitializing || IsDisposed) return;
        if (dgvInsurances.CurrentRow?.DataBoundItem is not EmployeeCarInsurance selected) return;
        _editingInsurance = selected;

        pickEmployee.SelectById(selected.EmployeeId);

        txtTotalAmount.Value = selected.TotalAmount;

        decimal calcInst = selected.MonthlyInstallment > 0 ? (int)Math.Round(selected.TotalAmount / selected.MonthlyInstallment) : 1;
        calcInst = Math.Max(numInstallments.Minimum, Math.Min(numInstallments.Maximum, calcInst));
        numInstallments.Value = calcInst;

        decimal startYear = selected.StartYear > 0 ? selected.StartYear : 1403;
        startYear = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, startYear));
        numStartYear.Value = startYear;

        if (selected.StartMonth >= 1 && selected.StartMonth <= 12) cmbStartMonth.SelectedItem = selected.StartMonth;
        txtDescription.Text = selected.Description ?? "";

        SetEditMode(true);
        btnDelete.Enabled = true;
    }

    private void ResetForm()
    {
        _editingInsurance = null;
        txtTotalAmount.Text = string.Empty;
        numInstallments.Value = 10;
        txtDescription.Text = string.Empty;
        pickEmployee.Clear();

        var pc = new PersianCalendar();
        numStartYear.Value = pc.GetYear(DateTime.Now);
        cmbStartMonth.SelectedItem = pc.GetMonth(DateTime.Now);

        SetEditMode(false);
        btnDelete.Enabled = false;

        if (dgvInsurances.Rows.Count > 0) { dgvInsurances.ClearSelection(); dgvInsurances.CurrentCell = null; }
        CalculateInstallment();
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            if (pickEmployee.SelectedId is null) { ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید."); return; }
            decimal totalAmount = NumberHelper.ParseFormatted(txtTotalAmount.Text);
            if (totalAmount <= 0) { ModernMessageBox.ShowWarning("مبلغ بیمه باید بزرگتر از صفر باشد."); return; }
            if (numStartYear.Value <= 0) { ModernMessageBox.ShowWarning("سال شروع را انتخاب کنید."); return; }
            if (cmbStartMonth.SelectedItem is null) { ModernMessageBox.ShowWarning("ماه شروع را انتخاب کنید."); return; }

            int empId = pickEmployee.SelectedId!.Value;
            int startYear = (int)numStartYear.Value;
            int startMonth = (int)cmbStartMonth.SelectedItem;
            decimal monthlyInstallment = NumberHelper.Round(totalAmount / (int)numInstallments.Value, 0);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingInsurance is null)
                {
                    var insurance = new EmployeeCarInsurance
                    {
                        EmployeeId = empId,
                        TotalAmount = totalAmount,
                        MonthlyInstallment = monthlyInstallment,
                        RemainingAmount = totalAmount,
                        StartYear = startYear,
                        StartMonth = startMonth,
                        StartDate = DateTime.Now,
                        Description = txtDescription.Text.Trim()
                    };
                    await _repository.AddAsync(insurance);
                }
                else
                {
                    decimal paidSoFar = _editingInsurance.TotalAmount - _editingInsurance.RemainingAmount;
                    if (paidSoFar < 0) paidSoFar = 0;
                    _editingInsurance.TotalAmount = totalAmount;
                    _editingInsurance.MonthlyInstallment = monthlyInstallment;
                    _editingInsurance.StartYear = startYear;
                    _editingInsurance.StartMonth = startMonth;
                    _editingInsurance.Description = txtDescription.Text.Trim();
                    _editingInsurance.RemainingAmount = Math.Max(0, totalAmount - paidSoFar);
                    await _repository.UpdateAsync(_editingInsurance);
                }
            }, "در حال ذخیره بیمه خودرو...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("بیمه خودرو با موفقیت ذخیره شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "CarInsuranceManagementForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CarInsuranceManagementForm"); } }
    }

    private async Task DeleteSafeAsync()
    {
        if (IsDisposed) return;
        if (dgvInsurances.CurrentRow?.DataBoundItem is not EmployeeCarInsurance selected) return;
        if (!ModernMessageBox.AskConfirm($"آیا از حذف بیمه خودرو «{selected.EmployeeName}» مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(selected.TotalAmount, usePersianDigits: true)} ریال\nمانده: {NumberHelper.FormatThousands(selected.RemainingAmount, usePersianDigits: true)} ریال", "حذف بیمه خودرو")) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.DeleteAsync(selected.Id); }, "در حال حذف...");
        if (success && !IsDisposed)
        {
            ToastHelper.Show("بیمه خودرو با موفقیت حذف شد.", isSuccess: true);
            ResetForm();
            _isInitializing = true;
            await LoadDataSafeAsync();
        }
    }
}
