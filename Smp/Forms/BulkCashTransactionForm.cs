using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>
/// ثبت گروهی حواله‌ها — ردیفی: هر ردیف یک حواله‌ی کامل است (نوع پرداخت، پروژه/قرارداد، ماهیت، دسته، گیرنده، مبلغ، شرح).
/// حساب بانکی و تاریخ بین همه‌ی ردیف‌ها مشترک است.
/// </summary>
public class BulkCashTransactionForm : Form
{
    private const int InitialRows = 10;

    private readonly string _connectionString;
    private readonly CashTransactionRepository _transRepo;
    private readonly BankAccountRepository _bankRepo;
    private readonly ClientContractRepository _clientContractRepo;
    private readonly ContractorContractRepository _contractorContractRepo;
    private readonly LookupRepository _lookupRepo;

    private ComboBox cmbAccount = null!;
    private TextBox txtDate = null!;
    private DataGridView dgvRows = null!;
    private Label lblTotal = null!;
    private Button btnSave = null!;

    private List<CompanyBankAccount> _accounts = new();
    private List<ClientContract> _clientContracts = new();
    private List<ContractorContract> _contractorContracts = new();
    private List<string> _projectCostTypes = new();
    private List<string> _adminCostTypes = new();
    private List<string> _categories = new();

    private List<ComboItem> _clientItems = new();       // «--- بدون اتصال ---» + قراردادهای کارفرما
    private List<ComboItem> _contractorItems = new();   // «--- انتخاب کنید ---» + قراردادهای پیمانکار
    private readonly List<ComboItem> _noneItems = new() { new ComboItem { Id = 0, Display = "—" } };

    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private bool _loaded = false;
    private bool _configuring = false;

    private sealed class ComboItem
    {
        public int Id { get; set; }
        public string Display { get; set; } = "";
        public int? ClientContractId { get; set; }
    }

    private sealed class ScopeItem
    {
        public string Key { get; set; } = "";
        public string Display { get; set; } = "";
    }

    private static readonly List<ScopeItem> Scopes = new()
    {
        new ScopeItem { Key = PaymentScopes.Administrative,      Display = "هزینه‌های اداری" },
        new ScopeItem { Key = PaymentScopes.ProjectExpense,      Display = "هزینه‌های پروژه" },
        new ScopeItem { Key = PaymentScopes.DirectPurchase,      Display = "خرید مستقیم برای پروژه" },
        new ScopeItem { Key = PaymentScopes.InformalContractor,  Display = "پیمانکار بدون قرارداد" },
        new ScopeItem { Key = PaymentScopes.ContractorContract,  Display = "پیمانکار با قرارداد" },
        new ScopeItem { Key = "Payroll",                         Display = "هزینه حقوق و مساعده" },
    };

    // ستون‌ها
    private const string ColNo = "No", ColScope = "Scope", ColProject = "Project", ColCost = "Cost", ColCategory = "Category",
                         ColRecipient = "Recipient", ColAmount = "Amount", ColDesc = "Desc";

    public BulkCashTransactionForm(string connectionString)
    {
        _connectionString = connectionString;
        _transRepo = new CashTransactionRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);
        _clientContractRepo = new ClientContractRepository(connectionString);
        _contractorContractRepo = new ContractorContractRepository(connectionString);
        _lookupRepo = new LookupRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("ثبت گروهی حواله‌ها", new Size(1300, 640));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        Shown += async (s, e) => await LoadDataAsync();
    }

    // ───────────────────────── UI ─────────────────────────
    private void BuildUI()
    {
        int FH = ModernUI.FieldHeight;

        // کارت مشترک: حساب + تاریخ
        var cardMain = new ModernCard("🏦 اطلاعات مشترک همه‌ی حواله‌ها", ModernTheme.Primary, 2, FH);
        cmbAccount = ModernUI.Combo();
        cmbAccount.DisplayMember = "DisplayTitle"; cmbAccount.ValueMember = "Id";

        var dateBox = ModernUI.Input();
        dateBox.TextAlign = HorizontalAlignment.Center;
        txtDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

        cardMain.Body.Controls.Add(ModernUI.Field("حساب / صندوق", cmbAccount), 0, 0);
        cardMain.Body.Controls.Add(ModernUI.Field("تاریخ", dateBox), 1, 0);

        // جدول ردیفی
        dgvRows = ModernGrid.Create();
        dgvRows.AutoGenerateColumns = false;
        dgvRows.ReadOnly = false;
        dgvRows.AllowUserToAddRows = false;
        dgvRows.AllowUserToDeleteRows = false;
        dgvRows.MultiSelect = true;
        dgvRows.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgvRows.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvRows.RowTemplate.Height = 40;
        dgvRows.EditMode = DataGridViewEditMode.EditOnEnter;

        dgvRows.Columns.Add(new DataGridViewTextBoxColumn { Name = ColNo, HeaderText = "#", ReadOnly = true, FillWeight = 4, MinimumWidth = 40 });
        dgvRows.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = ColScope, HeaderText = "نوع پرداخت", FillWeight = 14, MinimumWidth = 150, FlatStyle = FlatStyle.Flat,
            DataSource = Scopes, DisplayMember = nameof(ScopeItem.Display), ValueMember = nameof(ScopeItem.Key),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
        });
        dgvRows.Columns.Add(new DataGridViewComboBoxColumn { Name = ColProject, HeaderText = "پروژه / قرارداد", FillWeight = 17, MinimumWidth = 170, FlatStyle = FlatStyle.Flat });
        dgvRows.Columns.Add(new DataGridViewComboBoxColumn { Name = ColCost, HeaderText = "ماهیت هزینه", FillWeight = 10, MinimumWidth = 110, FlatStyle = FlatStyle.Flat });
        dgvRows.Columns.Add(new DataGridViewComboBoxColumn { Name = ColCategory, HeaderText = "دسته‌بندی", FillWeight = 8, MinimumWidth = 100, FlatStyle = FlatStyle.Flat });
        dgvRows.Columns.Add(new DataGridViewTextBoxColumn { Name = ColRecipient, HeaderText = "گیرنده (جستجو در تفصیلی‌ها 🔍)", FillWeight = 15, MinimumWidth = 160, ReadOnly = true });
        dgvRows.Columns[ColRecipient].DefaultCellStyle.ForeColor = ModernTheme.Primary;
        dgvRows.Columns[ColRecipient].DefaultCellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        dgvRows.Columns[ColRecipient].ToolTipText = "کلیک یا شروع تایپ: جستجو و انتخاب از جدول تفصیلی‌ها  |  Delete: پاک کردن";
        dgvRows.Columns.Add(new DataGridViewTextBoxColumn { Name = ColAmount, HeaderText = "مبلغ (ریال)", FillWeight = 11, MinimumWidth = 120 });
        dgvRows.Columns.Add(new DataGridViewTextBoxColumn { Name = ColDesc, HeaderText = "توضیحات", FillWeight = 17, MinimumWidth = 140 });
        foreach (var c in new[] { ColNo, ColAmount })
            dgvRows.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvRows.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgvRows.IsCurrentCellDirty && dgvRows.CurrentCell is DataGridViewComboBoxCell)
                dgvRows.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        dgvRows.CellValueChanged += DgvRows_CellValueChanged;
        dgvRows.CellClick += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgvRows.Columns[e.ColumnIndex].Name == ColRecipient) PickRecipient(dgvRows.Rows[e.RowIndex], null); };
        dgvRows.KeyDown += (s, e) =>
        {
            if (dgvRows.CurrentCell is null || dgvRows.CurrentCell.OwningColumn.Name != ColRecipient) return;
            var row = dgvRows.CurrentCell.OwningRow;
            if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.F4) { e.Handled = true; PickRecipient(row, null); }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) { e.Handled = true; SetRecipient(row, null); }
        };
        dgvRows.KeyPress += (s, e) =>
        {
            if (dgvRows.CurrentCell is null || dgvRows.CurrentCell.OwningColumn.Name != ColRecipient) return;
            if (char.IsControl(e.KeyChar)) return;
            e.Handled = true;
            PickRecipient(dgvRows.CurrentCell.OwningRow, e.KeyChar.ToString());   // شروع تایپ = جستجو با همان حروف
        };
        dgvRows.CellEndEdit += DgvRows_CellEndEdit;
        dgvRows.DataError += (s, e) => { e.ThrowException = false; };   // مقدار ناسازگار کمبو (مثلاً بعد از تغییر نوع) خطا نشان ندهد

        var gridHost = ModernReport.WrapGrid(dgvRows);

        // نوار پایین: جمع + دکمه‌ها
        lblTotal = new Label
        {
            AutoSize = false, Width = 420, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleRight,
            Font = ModernTheme.GetFont(11f, FontStyle.Bold), ForeColor = ModernTheme.TextStrong, RightToLeft = RightToLeft.Yes
        };

        var btnAdd = ModernButton.Soft("➕ افزودن ردیف", ModernTheme.Primary, 150);
        btnAdd.Click += (s, e) => AddRow(copyFromPrevious: true);
        var btnCopy = ModernButton.Outline("📑 تکرار ردیف انتخابی", ModernTheme.Neutral, 190);
        btnCopy.Click += (s, e) => DuplicateSelectedRow();
        var btnDelete = ModernButton.Danger("🗑 حذف ردیف", 130);
        btnDelete.Click += (s, e) => DeleteSelectedRows();
        btnSave = ModernButton.Success("💾 ثبت گروهی حواله‌ها", 220, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();

        var bar = ModernForm.CreateButtonBar(btnSave, btnAdd, btnCopy, btnDelete);
        var footer = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.Background };
        footer.Controls.Add(bar);
        footer.Controls.Add(lblTotal);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardMain.Height + 8));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(cardMain, 0, 0);
        root.Controls.Add(gridHost, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
    }

    // ───────────────────────── بارگذاری ─────────────────────────
    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var accountsTask = _bankRepo.GetActiveAsync();
                var clientContractsTask = Task.Run(() => _clientContractRepo.GetAll());
                var contractorContractsTask = Task.Run(() => _contractorContractRepo.GetAll());
                var projectCostTypesTask = _lookupRepo.GetByCategoryAsync("CostType");
                var adminCostTypesTask = _lookupRepo.GetByCategoryAsync("AdministrativeCostType");
                var categoriesTask = _lookupRepo.GetByCategoryAsync("TransactionCategory");

                await Task.WhenAll(accountsTask, clientContractsTask, contractorContractsTask, projectCostTypesTask, adminCostTypesTask, categoriesTask);
                if (IsDisposed) return;

                _accounts = accountsTask.Result; _clientContracts = clientContractsTask.Result; _contractorContracts = contractorContractsTask.Result;
                _projectCostTypes = projectCostTypesTask.Result.Select(i => i.Value).ToList();
                if (_projectCostTypes.Count == 0) _projectCostTypes.AddRange(new[] { "خرید کالا", "خرید خدمات", "حمل و نقل", "سایر" });
                else if (!_projectCostTypes.Contains("حمل و نقل")) _projectCostTypes.Add("حمل و نقل");

                _adminCostTypes = adminCostTypesTask.Result.Select(i => i.Value).ToList();
                if (_adminCostTypes.Count == 0) _adminCostTypes.AddRange(new[] { "اجاره", "آب و برق", "تلفن و اینترنت", "لوازم مصرفی", "پذیرایی", "حقوق و دستمزد", "حمل و نقل", "تعمیر و نگهداری", "سایر" });

                _categories = categoriesTask.Result.Select(i => i.Value).Where(c => c.Trim() != "انتقال بین حساب‌ها").ToList();
                if (_categories.Count == 0) _categories.AddRange(new[] { "عملیاتی", "اداری", "پرسنلی", "هزینه بانکی", "سایر" });
                if (!_categories.Contains("پرسنلی")) _categories.Add("پرسنلی");

                cmbAccount.DataSource = _accounts;
                if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;

                _clientItems = new List<ComboItem> { new() { Id = 0, Display = "--- بدون اتصال ---" } };
                _clientItems.AddRange(_clientContracts.Select(c => new ComboItem { Id = c.Id, Display = $"{c.ContractNumber} - {c.Subject}" }));

                _contractorItems = new List<ComboItem> { new() { Id = 0, Display = "--- انتخاب کنید ---" } };
                _contractorItems.AddRange(_contractorContracts.Select(c => new ComboItem { Id = c.Id, Display = $"{c.DisplayName} - قرارداد {c.ContractNumber}", ClientContractId = c.ClientContractId }));

                ((DataGridViewComboBoxColumn)dgvRows.Columns[ColCategory]).DataSource = _categories;
                _loaded = true;

                for (int i = 0; i < InitialRows; i++) AddRow(copyFromPrevious: false);
            }, "در حال بارگذاری اطلاعات...");

            if (!IsDisposed && dgvRows.Rows.Count > 0)
            {
                dgvRows.CurrentCell = dgvRows.Rows[0].Cells[ColAmount];
                dgvRows.Focus();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "BulkCashTransactionForm.LoadDataAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
    }

    // ───────────────────────── مدیریت ردیف‌ها ─────────────────────────
    private void AddRow(bool copyFromPrevious)
    {
        if (!_loaded || IsDisposed) return;

        string scope = PaymentScopes.Administrative;
        object? project = null, cost = null, category = null;
        if (copyFromPrevious && dgvRows.Rows.Count > 0)
        {
            var prev = dgvRows.Rows[dgvRows.Rows.Count - 1];
            scope = prev.Cells[ColScope].Value?.ToString() ?? scope;
            project = prev.Cells[ColProject].Value;
            cost = prev.Cells[ColCost].Value;
            category = prev.Cells[ColCategory].Value;
        }

        int idx = dgvRows.Rows.Add();
        var row = dgvRows.Rows[idx];
        _configuring = true;
        try { row.Cells[ColScope].Value = scope; }
        finally { _configuring = false; }
        ConfigureRow(row, keepProject: project, keepCost: cost, keepCategory: category);
        RenumberRows();
        RefreshTotal();

        if (copyFromPrevious) { dgvRows.CurrentCell = row.Cells[ColRecipient]; }
    }

    private void DuplicateSelectedRow()
    {
        var src = dgvRows.CurrentRow;
        if (src is null) { ModernMessageBox.ShowWarning("ابتدا یک ردیف را انتخاب کنید."); return; }

        int idx = dgvRows.Rows.Add();
        var row = dgvRows.Rows[idx];
        _configuring = true;
        try { row.Cells[ColScope].Value = src.Cells[ColScope].Value; }
        finally { _configuring = false; }
        ConfigureRow(row, src.Cells[ColProject].Value, src.Cells[ColCost].Value, src.Cells[ColCategory].Value);
        SetRecipient(row, src.Tag as RecipientInfo);
        row.Cells[ColAmount].Value = src.Cells[ColAmount].Value;
        row.Cells[ColDesc].Value = src.Cells[ColDesc].Value;
        RenumberRows();
        RefreshTotal();
        dgvRows.CurrentCell = row.Cells[ColAmount];
    }

    private void DeleteSelectedRows()
    {
        var rows = dgvRows.SelectedCells.Cast<DataGridViewCell>().Select(c => c.OwningRow).Distinct().ToList();
        if (rows.Count == 0 && dgvRows.CurrentRow is not null) rows.Add(dgvRows.CurrentRow);
        if (rows.Count == 0) return;
        dgvRows.EndEdit();
        foreach (var r in rows.OrderByDescending(r => r.Index)) dgvRows.Rows.Remove(r);
        if (dgvRows.Rows.Count == 0) AddRow(copyFromPrevious: false);
        RenumberRows();
        RefreshTotal();
    }

    private void RenumberRows()
    {
        for (int i = 0; i < dgvRows.Rows.Count; i++)
            dgvRows.Rows[i].Cells[ColNo].Value = NumberHelper.FormatThousands(i + 1, usePersianDigits: true);
    }

    private void DgvRows_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_configuring || e.RowIndex < 0 || e.ColumnIndex < 0 || !_loaded) return;
        string col = dgvRows.Columns[e.ColumnIndex].Name;
        if (col == ColScope) ConfigureRow(dgvRows.Rows[e.RowIndex]);
        else if (col == ColAmount) RefreshTotal();
    }

    private void DgvRows_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (dgvRows.Columns[e.ColumnIndex].Name != ColAmount) return;

        var cell = dgvRows.Rows[e.RowIndex].Cells[ColAmount];
        string text = cell.Value?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(text)) { RefreshTotal(); return; }
        decimal amount = NumberHelper.ParseFormatted(text);
        cell.Value = amount > 0 ? NumberHelper.FormatThousands(amount, usePersianDigits: true) : "";
        RefreshTotal();
    }

    /// <summary>کمبوهای «پروژه/قرارداد»، «ماهیت هزینه» و «دسته‌بندی» ردیف را بر اساس نوع پرداخت می‌چیند</summary>
    private void ConfigureRow(DataGridViewRow row, object? keepProject = null, object? keepCost = null, object? keepCategory = null)
    {
        string scope = row.Cells[ColScope].Value?.ToString() ?? PaymentScopes.Administrative;
        bool isAdmin = scope == PaymentScopes.Administrative;
        bool isProject = scope == PaymentScopes.ProjectExpense;
        bool isContractor = scope == PaymentScopes.ContractorContract;
        bool isPayroll = scope == "Payroll";

        _configuring = true;
        try
        {
            // پروژه / قرارداد
            var pCell = (DataGridViewComboBoxCell)row.Cells[ColProject];
            // هزینه اداری: پروژه غیرفعال | هزینه پروژه: پروژه (قرارداد کارفرما) فعال | ماهیت هزینه در هر دو فعال
            List<ComboItem> items = isContractor ? _contractorItems : isAdmin ? _noneItems : _clientItems;
            pCell.DataSource = null;
            pCell.DisplayMember = nameof(ComboItem.Display);
            pCell.ValueMember = nameof(ComboItem.Id);
            pCell.DataSource = items;
            pCell.ReadOnly = isAdmin;
            pCell.Style.BackColor = pCell.ReadOnly ? ModernTheme.InputDisabled : Color.Empty;
            pCell.Value = keepProject is not null && items.Any(i => Equals(i.Id, keepProject)) ? keepProject : items[0].Id;

            // ماهیت هزینه
            var cCell = (DataGridViewComboBoxCell)row.Cells[ColCost];
            var costs = (isAdmin ? _adminCostTypes
                : isProject ? _projectCostTypes.Union(_adminCostTypes).ToList()
                : _projectCostTypes).ToList();
            cCell.DataSource = null;
            cCell.DataSource = costs;
            string? defCost = isAdmin ? null
                : scope == PaymentScopes.DirectPurchase ? "خرید کالا"
                : scope == PaymentScopes.InformalContractor ? "خرید خدمات"
                : isPayroll ? "حقوق و دستمزد"
                : isContractor ? "خدمات پیمانکاری" : null;
            if (keepCost is not null && costs.Contains(keepCost.ToString()!)) cCell.Value = keepCost;
            else if (defCost is not null && costs.Contains(defCost)) cCell.Value = defCost;
            else if (costs.Count > 0) cCell.Value = costs[0];

            // دسته‌بندی
            var gCell = (DataGridViewComboBoxCell)row.Cells[ColCategory];
            gCell.DataSource = null;
            gCell.DataSource = _categories;
            string? defCat = isPayroll ? "پرسنلی" : null;
            if (defCat is not null && _categories.Contains(defCat)) gCell.Value = defCat;
            else if (keepCategory is not null && _categories.Contains(keepCategory.ToString()!)) gCell.Value = keepCategory;
            else if (_categories.Count > 0) gCell.Value = _categories[0];
        }
        finally { _configuring = false; }
    }

    private sealed class RecipientInfo { public string Code = ""; public string Name = ""; }

    /// <summary>انتخاب گیرنده از جدول تفصیلی‌ها (با جستجو)</summary>
    private void PickRecipient(DataGridViewRow row, string? initialSearch)
    {
        if (IsDisposed) return;
        using var form = new TafsiliSelectionForm(_connectionString, initialSearch);
        if (form.ShowDialog(this) != DialogResult.OK || IsDisposed) return;
        SetRecipient(row, new RecipientInfo { Code = form.SelectedCode, Name = form.SelectedName });
    }

    private static void SetRecipient(DataGridViewRow row, RecipientInfo? info)
    {
        row.Tag = info;
        row.Cells[ColRecipient].Value = info is null ? "" : (string.IsNullOrWhiteSpace(info.Name) ? info.Code : $"{info.Code} - {info.Name}");
    }

    private void RefreshTotal()
    {
        decimal total = 0; int count = 0;
        foreach (DataGridViewRow row in dgvRows.Rows)
        {
            string t = row.Cells[ColAmount].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(t)) continue;
            decimal a = NumberHelper.ParseFormatted(t);
            if (a > 0) { total += a; count++; }
        }
        lblTotal.Text = $"جمع: {NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال  |  {NumberHelper.FormatThousands(count, usePersianDigits: true)} حواله";
    }

    // ───────────────────────── ثبت ─────────────────────────
    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            dgvRows.EndEdit();
            if (cmbAccount.SelectedValue is null) { ModernMessageBox.ShowWarning("حساب را انتخاب کنید."); return; }
            DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
            if (date is null) { ModernMessageBox.ShowWarning("تاریخ نامعتبر است."); return; }

            int accountId = (int)cmbAccount.SelectedValue;
            string? accountTitle = _accounts.FirstOrDefault(a => a.Id == accountId)?.Title;

            var transactions = new List<CashTransaction>();
            foreach (DataGridViewRow row in dgvRows.Rows)
            {
                string amountStr = row.Cells[ColAmount].Value?.ToString() ?? "";
                var recipientInfo = row.Tag as RecipientInfo;
                string recipient = recipientInfo is null ? "" : (string.IsNullOrWhiteSpace(recipientInfo.Name) ? recipientInfo.Code : recipientInfo.Name);
                string desc = row.Cells[ColDesc].Value?.ToString()?.Trim() ?? "";
                decimal amount = string.IsNullOrWhiteSpace(amountStr) ? 0 : NumberHelper.ParseFormatted(amountStr);
                int rowNo = row.Index + 1;

                if (amount <= 0)
                {
                    if (recipient.Length > 0 || desc.Length > 0)
                    {
                        Fail(row, ColAmount, $"ردیف {rowNo}: مبلغ وارد نشده است.");
                        return;
                    }
                    continue;   // ردیف خالی
                }

                string scopeKey = row.Cells[ColScope].Value?.ToString() ?? "";
                int projectId = row.Cells[ColProject].Value is int pid ? pid : Convert.ToInt32(row.Cells[ColProject].Value ?? 0);
                string category = row.Cells[ColCategory].Value?.ToString() ?? "";
                string costType = row.Cells[ColCost].Value?.ToString() ?? "";

                string paymentScope; int? clientContractId = null; int? contractorContractId = null; string contractTitle;

                switch (scopeKey)
                {
                    case var k when k == PaymentScopes.Administrative:
                        paymentScope = PaymentScopes.Administrative; contractTitle = "هزینه‌های اداری"; break;
                    case var k when k == PaymentScopes.ProjectExpense:
                        paymentScope = PaymentScopes.ProjectExpense;
                        if (projectId <= 0) { Fail(row, ColProject, $"ردیف {rowNo}: برای هزینه‌های پروژه، انتخاب پروژه (قرارداد کارفرما) الزامی است."); return; }
                        { var it = _clientItems.First(i => i.Id == projectId); clientContractId = it.Id; contractTitle = it.Display; }
                        break;
                    case "Payroll":
                        paymentScope = PaymentScopes.Administrative; contractTitle = "حقوق و دستمزد پرسنل"; category = "پرسنلی";
                        if (projectId > 0) { var it = _clientItems.FirstOrDefault(i => i.Id == projectId); if (it is not null) { clientContractId = it.Id; contractTitle = it.Display; } }
                        break;
                    case var k when k == PaymentScopes.DirectPurchase || k == PaymentScopes.InformalContractor:
                        paymentScope = k;
                        if (projectId <= 0) { Fail(row, ColProject, $"ردیف {rowNo}: انتخاب قرارداد کارفرما الزامی است."); return; }
                        { var it = _clientItems.First(i => i.Id == projectId); clientContractId = it.Id; contractTitle = it.Display; }
                        break;
                    case var k when k == PaymentScopes.ContractorContract:
                        paymentScope = PaymentScopes.ContractorContract;
                        if (projectId <= 0) { Fail(row, ColProject, $"ردیف {rowNo}: انتخاب قرارداد پیمانکار الزامی است."); return; }
                        { var it = _contractorItems.First(i => i.Id == projectId); contractorContractId = it.Id; clientContractId = it.ClientContractId; contractTitle = it.Display; }
                        break;
                    default:
                        Fail(row, ColScope, $"ردیف {rowNo}: نوع پرداخت انتخاب نشده است."); return;
                }

                transactions.Add(new CashTransaction
                {
                    AccountId = accountId,
                    AccountTitle = accountTitle,
                    PaymentScope = paymentScope,
                    ContractId = clientContractId,
                    ContractorContractId = contractorContractId,
                    ContractTitle = contractTitle,
                    TransactionDate = date.Value,
                    Amount = amount,
                    TransactionType = "حواله",
                    Direction = DirectionTypes.Outgoing,
                    Category = category,
                    CostType = costType,
                    RecipientName = string.IsNullOrWhiteSpace(recipient) ? null : recipient,
                    TafsiliCode = string.IsNullOrWhiteSpace(recipientInfo?.Code) ? null : recipientInfo!.Code,
                    TafsiliTitle = string.IsNullOrWhiteSpace(recipientInfo?.Name) ? null : recipientInfo!.Name,
                    Description = desc
                });
            }

            if (transactions.Count == 0) { ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد."); return; }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _transRepo.BulkInsertAsync(transactions);
            }, "در حال ثبت گروهی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"{transactions.Count} حواله با موفقیت ثبت شد.", true);
                DialogResult = DialogResult.OK; Close();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "BulkCashTransactionForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BulkCashTransactionForm"); } }
    }

    private void Fail(DataGridViewRow row, string column, string message)
    {
        ModernMessageBox.ShowWarning(message);
        try { dgvRows.CurrentCell = row.Cells[column]; } catch { /* سلول فقط‌خواندنی */ }
    }
}
