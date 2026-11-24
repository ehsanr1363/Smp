using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

public class AccountTransferForm : Form
{
    private readonly AccountTransferRepository _repository;
    private readonly BankAccountRepository _bankRepo;
    private readonly CashTransactionRepository _cashTransRepo;
    private readonly string _connectionString;
    private readonly int? _cashTransactionId;

    private ComboBox cmbSourceAccount = null!, cmbDestinationAccount = null!;
    private TextBox txtFee = null!, txtAmount = null!, txtDate = null!, txtDescription = null!;

    private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!, btnDelete = null!, btnExportExcel = null!, btnPrintOrder = null!;
    private DataGridView dgvTransfers = null!;
    private TextBox txtSearch = null!;
    private ModernStatCard statCount = null!, statTotal = null!, statFee = null!;

    private List<AccountTransfer> _allTransfers = new();
    private string? _lastSortedColumn;
    private bool _sortAscending = true;
    private int? _editingTransferId = null;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public AccountTransferForm(string connectionString, int? cashTransactionId = null)
    {
        _connectionString = connectionString;
        _repository = new AccountTransferRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);
        _cashTransRepo = new CashTransactionRepository(connectionString);
        _cashTransactionId = cashTransactionId;

        BuildUI();
        // ارتفاع فرم 850 برای تامین فضای کافی در نظر گرفته شده است
        this.ApplyStandardHeader("انتقال وجه بین حساب‌های بانکی", new Size(1200, 850));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AccountTransferForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ═══════════ ۱. کارت ورودی ═══════════
        cmbSourceAccount = ModernUI.Combo();
        cmbSourceAccount.DisplayMember = "DisplayTitle";
        cmbSourceAccount.ValueMember = "Id";
        cmbDestinationAccount = ModernUI.Combo();
        cmbDestinationAccount.DisplayMember = "DisplayTitle";
        cmbDestinationAccount.ValueMember = "Id";

        txtAmount = new TextBox();
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        txtDate = new TextBox { TextAlign = HorizontalAlignment.Center };
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        txtFee = new TextBox { Text = "۰" };
        SmartInputHelper.EnableSmartAmountTextBox(txtFee);
        txtDescription = new TextBox { PlaceholderText = "توضیحات اختیاری..." };

        var arrow = ModernUI.Caption("⬅", 16f, true, ModernTheme.Primary);
        arrow.Dock = DockStyle.Fill;
        arrow.TextAlign = ContentAlignment.MiddleCenter;

        const int rh = 50;
        var cols = new[]
        {
            new ColumnStyle(SizeType.Percent, 50F),
            new ColumnStyle(SizeType.Absolute, 44F),
            new ColumnStyle(SizeType.Percent, 50F)
        };
        var card = new ModernCard("اطلاعات انتقال وجه", ModernTheme.Primary, cols, rh, rh, rh);
        card.Body.Controls.Add(ModernUI.Row("از حساب (مبدأ)", cmbSourceAccount), 0, 0);
        card.Body.Controls.Add(arrow, 1, 0);
        card.Body.Controls.Add(ModernUI.Row("به حساب (مقصد)", cmbDestinationAccount), 2, 0);
        card.Body.Controls.Add(ModernUI.Row("مبلغ (ریال)", txtAmount), 0, 1);
        card.Body.Controls.Add(ModernUI.Row("تاریخ", txtDate), 2, 1);
        card.Body.Controls.Add(ModernUI.Row("کارمزد (ریال)", txtFee), 0, 2);
        card.Body.Controls.Add(ModernUI.Row("توضیحات", txtDescription), 2, 2);

        // ═══════════ ۲. کارت‌های خلاصه ═══════════
        var strip = ModernReport.CreateStatStrip(3, 76);
        statCount = strip.Add("تعداد انتقال‌ها", ModernTheme.Primary);
        statTotal = strip.Add("جمع مبلغ انتقال‌ها", ModernTheme.Success);
        statFee   = strip.Add("جمع کارمزدها", ModernTheme.Warning);
        statCount.SetCount(0, "انتقال");
        statTotal.SetAmount(0, "ریال");
        statFee.SetAmount(0, "ریال");
        strip.Dock = DockStyle.Fill;

        // ═══════════ ۳. نوار ابزار: جستجو + اکسل ═══════════
        txtSearch = new TextBox { PlaceholderText = "جستجو در لیست..." };
        txtSearch.TextChanged += (s, e) => ApplyFilterAndSort();
        var searchRow = ModernUI.Row("جستجو", txtSearch, 70);

        btnDownloadTemplate = ModernButton.Soft("قالب اکسل", ModernTheme.Primary, 120, 36);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
        btnImportExcel = ModernButton.Soft("ایمپورت از اکسل", ModernTheme.Secondary, 150, 36);
        btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);
        btnExportExcel = ModernButton.Soft("خروجی اکسل", ModernTheme.Teal, 130, 36);
        btnExportExcel.Click += (s, e) => ExportToExcel();

        var excelFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = Color.Transparent
        };
        excelFlow.Controls.Add(btnDownloadTemplate);
        excelFlow.Controls.Add(btnImportExcel);
        excelFlow.Controls.Add(btnExportExcel);

        var toolbar = new BufferedTable
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = Padding.Empty, BackColor = Color.Transparent
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430F));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        toolbar.Controls.Add(searchRow, 0, 0);
        toolbar.Controls.Add(excelFlow, 2, 0);

        // ═══════════ ۴. جدول ═══════════
        dgvTransfers = ModernGrid.Create();
        dgvTransfers.MultiSelect = true;
        dgvTransfers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvTransfers.UseAiryLook(44);
        dgvTransfers.UseSelectionAccent();
        dgvTransfers.UseTextAlignment("SourceAccountTitle", "DestinationAccountTitle", "Description");

        dgvTransfers.ColumnHeaderMouseClick += DgvTransfers_ColumnHeaderMouseClick;
        dgvTransfers.CellDoubleClick += DgvTransfers_CellDoubleClick;

        dgvTransfers.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvTransfers.Columns[e.ColumnIndex].Name;

            if (colName == "TransferDate" || colName == "CreatedAt")
            {
                if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            }
            else if (colName == "Amount")
            {
                if (e.Value is decimal dec) { e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true; }
            }
        };

        // ═══════════ ۵. دکمه‌های پایین ═══════════
        btnSave = ModernButton.Save("ثبت انتقال وجه", 180, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();

        btnDelete = ModernButton.Soft("حذف ردیف‌های انتخاب‌شده", ModernTheme.Danger, 220, 40);
        btnDelete.Click += async (s, e) => await DeleteSafeAsync();

        btnPrintOrder = ModernButton.Soft("چاپ دستور پرداخت", ModernTheme.Primary, 190, 40);
        btnPrintOrder.Click += async (s, e) => await PrintPaymentOrdersAsync();

        var bar = ModernForm.CreateButtonBar(btnSave, btnDelete, btnPrintOrder);

        // ═══════════ چیدمان صفحه ═══════════
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(14, 12, 14, 6), BackColor = ModernTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));              // کارت‌های خلاصه
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, card.Height + 10)); // کارت ورودی
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));              // نوار ابزار
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));              // جدول
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));              // دکمه‌ها
        card.Dock = DockStyle.Fill;
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(toolbar, 0, 2);
        root.Controls.Add(ModernReport.WrapGrid(dgvTransfers), 0, 3);
        root.Controls.Add(bar, 0, 4);

        this.Controls.Add(root);
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;

        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var allAccounts = await _bankRepo.GetActiveAsync();
                _allTransfers = await _repository.GetAllAsync();

                if (IsDisposed) return;

                var bankAccounts = allAccounts.Where(a => !a.IsCashDesk).ToList();

                cmbSourceAccount.DataSource = bankAccounts.ToList();
                cmbDestinationAccount.DataSource = bankAccounts.ToList();

                ApplyFilterAndSort();

                if (cmbSourceAccount.Items.Count > 0) cmbSourceAccount.SelectedIndex = 0;
                if (cmbDestinationAccount.Items.Count > 1) cmbDestinationAccount.SelectedIndex = 1;

                if (_cashTransactionId.HasValue)
                {
                    var trans = await _cashTransRepo.GetByIdAsync(_cashTransactionId.Value);
                    if (trans is not null)
                    {
                        txtAmount.Text = NumberHelper.FormatThousands(trans.Amount, usePersianDigits: true);
                        txtDate.Text = DateHelper.ToPersianDate(trans.TransactionDate);
                        txtDescription.Text = trans.Description;

                        if (trans.AccountId.HasValue)
                            cmbSourceAccount.SelectedValue = trans.AccountId.Value;
                    }
                }
            }, "در حال بارگذاری اطلاعات...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AccountTransferForm.LoadData");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
    }

    private void ApplyFilterAndSort()
    {
        if (IsDisposed) return;

        var bankAccountIds = new HashSet<int>(
            (cmbSourceAccount.Items.Cast<CompanyBankAccount>()
                .Concat(cmbDestinationAccount.Items.Cast<CompanyBankAccount>()))
                .Select(a => a.Id));

        string search = txtSearch.Text.Trim();
        var query = _allTransfers.AsEnumerable();

        query = query.Where(t => bankAccountIds.Contains(t.SourceAccountId) && bankAccountIds.Contains(t.DestinationAccountId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                (t.SourceAccountTitle ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (t.DestinationAccountTitle ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (t.Description ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Amount.ToString().Contains(search) ||
                DateHelper.ToPersianDate(t.TransferDate).Contains(search)
            );
        }

        if (!string.IsNullOrEmpty(_lastSortedColumn))
        {
            var property = typeof(AccountTransfer).GetProperty(_lastSortedColumn);
            if (property is not null)
            {
                if (_sortAscending) query = query.OrderBy(x => property.GetValue(x, null));
                else query = query.OrderByDescending(x => property.GetValue(x, null));
            }
        }
        else
        {
            query = query.OrderByDescending(t => t.TransferDate).ThenByDescending(t => t.Id);
        }

        var rows = query.ToList();
        dgvTransfers.DataSource = new BindingList<AccountTransfer>(rows);
        ConfigureGridColumns();

        statCount.SetCount(rows.Count, "انتقال");
        statTotal.SetAmount(rows.Sum(t => t.Amount), "ریال");
        statFee.SetAmount(rows.Sum(t => t.TransferFee), "ریال");
    }

    private void ConfigureGridColumns()
    {
        foreach (DataGridViewColumn c in dgvTransfers.Columns)
            c.SortMode = DataGridViewColumnSortMode.Programmatic;

        if (dgvTransfers.Columns["Id"] is not null) dgvTransfers.Columns["Id"].Visible = false;
        if (dgvTransfers.Columns["SourceAccountId"] is not null) dgvTransfers.Columns["SourceAccountId"].Visible = false;
        if (dgvTransfers.Columns["DestinationAccountId"] is not null) dgvTransfers.Columns["DestinationAccountId"].Visible = false;
        if (dgvTransfers.Columns["CreatedAt"] is not null) dgvTransfers.Columns["CreatedAt"].Visible = false;
        if (dgvTransfers.Columns["TransferFee"] is not null) dgvTransfers.Columns["TransferFee"].Visible = false;
        if (dgvTransfers.Columns["TransferType"] is not null) dgvTransfers.Columns["TransferType"].Visible = false;

        if (dgvTransfers.Columns["SourceAccountTitle"] is not null) { dgvTransfers.Columns["SourceAccountTitle"].HeaderText = "از حساب"; dgvTransfers.Columns["SourceAccountTitle"].FillWeight = 20; }
        if (dgvTransfers.Columns["DestinationAccountTitle"] is not null) { dgvTransfers.Columns["DestinationAccountTitle"].HeaderText = "به حساب"; dgvTransfers.Columns["DestinationAccountTitle"].FillWeight = 20; }
        if (dgvTransfers.Columns["Amount"] is not null) { dgvTransfers.Columns["Amount"].HeaderText = "مبلغ"; dgvTransfers.Columns["Amount"].FillWeight = 15; }
        if (dgvTransfers.Columns["TransferDate"] is not null) { dgvTransfers.Columns["TransferDate"].HeaderText = "تاریخ"; dgvTransfers.Columns["TransferDate"].FillWeight = 15; }
        if (dgvTransfers.Columns["Description"] is not null) { dgvTransfers.Columns["Description"].HeaderText = "توضیحات"; dgvTransfers.Columns["Description"].FillWeight = 30; }
    }

    private void DgvTransfers_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0) return;

        string colName = dgvTransfers.Columns[e.ColumnIndex].DataPropertyName;
        if (string.IsNullOrEmpty(colName)) colName = dgvTransfers.Columns[e.ColumnIndex].Name;

        if (_lastSortedColumn == colName) _sortAscending = !_sortAscending;
        else { _lastSortedColumn = colName; _sortAscending = true; }

        ApplyFilterAndSort();
    }

    private void DgvTransfers_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (dgvTransfers.Rows[e.RowIndex].DataBoundItem is not AccountTransfer item) return;

        bool srcExists = cmbSourceAccount.Items.Cast<CompanyBankAccount>().Any(a => a.Id == item.SourceAccountId);
        bool destExists = cmbDestinationAccount.Items.Cast<CompanyBankAccount>().Any(a => a.Id == item.DestinationAccountId);

        if (!srcExists || !destExists)
        {
            ModernMessageBox.ShowWarning(
                "این انتقال قبلاً با فرم قدیمی ثبت شده و احتمالاً یک طرف آن تنخواه بوده است.\n\n" +
                "برای ویرایش یا حذف آن، از فرم «مدیریت تنخواه‌ها» یا از دفتر روزنامه جامع استفاده کنید.");
            return;
        }

        _editingTransferId = item.Id;
        cmbSourceAccount.SelectedValue = item.SourceAccountId;
        cmbDestinationAccount.SelectedValue = item.DestinationAccountId;
        txtAmount.Text = NumberHelper.FormatThousands(item.Amount, usePersianDigits: true);
        txtDate.Text = DateHelper.ToPersianDate(item.TransferDate);
        txtDescription.Text = item.Description ?? "";
        txtFee.Text = NumberHelper.FormatThousands(item.TransferFee, usePersianDigits: true);

        btnSave.Text = "ذخیره تغییرات";
        btnSave.BackColor = ModernTheme.Primary;
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        btnSave.Enabled = false;

        try
        {
            if (cmbSourceAccount.SelectedValue is null || cmbDestinationAccount.SelectedValue is null)
            {
                ModernMessageBox.ShowWarning("لطفاً حساب مبدا و مقصد را انتخاب کنید.");
                return;
            }

            int sourceId = (int)cmbSourceAccount.SelectedValue;
            int destId = (int)cmbDestinationAccount.SelectedValue;

            if (sourceId == destId)
            {
                ModernMessageBox.ShowWarning("حساب مبدا و مقصد نمی‌توانند یکسان باشند.");
                return;
            }

            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0)
            {
                ModernMessageBox.ShowWarning("مبلغ انتقال باید بزرگتر از صفر باشد.");
                return;
            }

            DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
            if (date is null)
            {
                ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست.");
                return;
            }

            if (!_editingTransferId.HasValue)
            {
                decimal actualBalance = 0;
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT CurrentBalance FROM CompanyBankAccounts WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Id", sourceId);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res is not null && res != DBNull.Value) actualBalance = Convert.ToDecimal(res);
                }

                if (actualBalance < amount)
                {
                    string displayBalance = actualBalance < 0
                        ? "منفی " + NumberHelper.FormatThousands(Math.Abs(actualBalance), usePersianDigits: true)
                        : NumberHelper.FormatThousands(actualBalance, usePersianDigits: true);

                    var confirmResult = ModernMessageBox.AskConfirm(
                        $"⚠️ هشدار: موجودی حساب مبدا کافی نیست!\n\n" +
                        $"موجودی فعلی: {displayBalance} ریال\n" +
                        $"مبلغ مورد نیاز: {NumberHelper.FormatThousands(amount, usePersianDigits: true)} ریال\n\n" +
                        $"با تایید شما، تلاش می‌شود موجودی منفی شود. آیا ادامه می‌دهید؟",
                        "کمبود موجودی");

                    if (!confirmResult) return;
                }
            }

            decimal fee = string.IsNullOrWhiteSpace(txtFee.Text) ? 0 : NumberHelper.ParseFormatted(txtFee.Text);
            if (fee < 0)
            {
                ModernMessageBox.ShowWarning("کارمزد نمی‌تواند منفی باشد.");
                return;
            }

            var transfer = new AccountTransfer
            {
                Id = _editingTransferId ?? 0,
                SourceAccountId = sourceId,
                DestinationAccountId = destId,
                Amount = amount,
                TransferFee = fee,
                TransferDate = date.Value,
                Description = txtDescription.Text.Trim(),
                TransferType = "BankToBank"
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingTransferId.HasValue) await _repository.UpdateAsync(transfer);
                else await _repository.AddAsync(transfer);
            }, "در حال ثبت انتقال...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show(_editingTransferId.HasValue ? "تغییرات ذخیره شد." : "انتقال با موفقیت ثبت شد.", isSuccess: true);
                _editingTransferId = null;
                btnSave.Text = "ثبت انتقال وجه";
                btnSave.BackColor = ModernTheme.Brand;
                txtAmount.Clear();
                txtFee.Text = "۰";
                txtDescription.Clear();
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AccountTransferForm.Save");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            btnSave.Enabled = true;
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AccountTransferForm"); }
        }
    }

    /// <summary>
    /// چاپ «دستور پرداخت خزانه‌داری» (همان PaymentOrderForm چک‌ها/تراکنش‌ها/تنخواه) برای انتقال‌های انتخاب‌شده.
    /// انتقال‌هایی که از یک حساب مبدأ هستند در یک دستور می‌آیند (هر حساب مبدأ یک دستور جدا)؛
    /// گیرنده‌ی هر ردیف = حساب مقصد انتقال.
    /// </summary>
    private async Task PrintPaymentOrdersAsync()
    {
        if (IsDisposed) return;

        var selected = dgvTransfers.SelectedRows.Cast<DataGridViewRow>()
            .OrderBy(r => r.Index)
            .Select(r => r.DataBoundItem as AccountTransfer)
            .Where(t => t is not null)
            .Cast<AccountTransfer>()
            .ToList();

        if (selected.Count == 0)
        {
            ModernMessageBox.ShowWarning("لطفاً ابتدا یک یا چند انتقال را از جدول انتخاب کنید.");
            return;
        }

        try
        {
            var accounts = (await _bankRepo.GetAllAsync()).ToDictionary(a => a.Id);

            // نام گیرنده: «بانک - شعبه (شماره حساب)» از حساب مقصد؛ اگر پیدا نشد همان عنوان ذخیره‌شده
            static string DescribeAccount(CompanyBankAccount? a, string? fallbackTitle)
            {
                if (a is null) return fallbackTitle ?? "";
                string text = string.IsNullOrWhiteSpace(a.BankName) ? a.BankTitle : a.BankName.Trim();
                if (!string.IsNullOrWhiteSpace(a.Branch)) text += " - شعبه " + a.Branch.Trim();
                if (!string.IsNullOrWhiteSpace(a.AccountNumber)) text += " (" + a.AccountNumber.Trim() + ")";
                return text;
            }

            foreach (var group in selected.GroupBy(t => t.SourceAccountId))
            {
                if (!accounts.TryGetValue(group.Key, out var sourceAccount))
                {
                    ModernMessageBox.ShowWarning($"حساب مبدأ «{group.First().SourceAccountTitle}» پیدا نشد؛ دستور پرداخت آن چاپ نشد.");
                    continue;
                }

                if (sourceAccount.IsCashDesk)
                {
                    ModernMessageBox.ShowWarning("دستور پرداخت بانکی برای صندوق نقدی صادر نمی‌شود.");
                    continue;
                }

                var items = group.Select(t =>
                {
                    accounts.TryGetValue(t.DestinationAccountId, out var destAccount);
                    return new PaymentOrderItem
                    {
                        Description = string.IsNullOrWhiteSpace(t.Description) ? "انتقال وجه بین حساب‌های بانکی" : t.Description!,
                        ReferenceNumber = "---",
                        Date = t.TransferDate,
                        Amount = t.Amount,
                        RecipientName = DescribeAccount(destAccount, t.DestinationAccountTitle)
                    };
                }).ToList();

                using var form = new PaymentOrderForm(items, sourceAccount);
                form.ShowDialog(this);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AccountTransferForm.PrintPaymentOrders");
            ModernMessageBox.ShowError("خطا در چاپ دستور پرداخت:\n" + ex.Message);
        }
    }

    private async Task DeleteSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        btnDelete.Enabled = false;
        try
        {
            if (dgvTransfers.SelectedRows.Count == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً ابتدا حداقل یک ردیف را از جدول انتخاب کنید.");
                return;
            }

            var selectedTransfers = dgvTransfers.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as AccountTransfer)
                .Where(t => t is not null)
                .ToList();

            if (selectedTransfers.Count == 0) return;

            string confirmMsg = selectedTransfers.Count == 1
                ? $"آیا از حذف این انتقال وجه مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(selectedTransfers[0].Amount, usePersianDigits: true)} ریال"
                : $"آیا از حذف {NumberHelper.FormatThousands(selectedTransfers.Count, usePersianDigits: true)} انتقال وجه انتخاب شده مطمئن هستید؟";

            if (!ModernMessageBox.AskConfirm(confirmMsg + "\n\nتوجه: با حذف این رکوردها، مبالغ به حساب‌های مبدا و مقصد بازگردانده می‌شود.", "حذف انتقال‌ها"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                foreach (var transfer in selectedTransfers)
                {
                    await _repository.DeleteAsync(transfer.Id);
                }
            }, "در حال حذف انتقال‌ها...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"{NumberHelper.FormatThousands(selectedTransfers.Count, usePersianDigits: true)} انتقال وجه با موفقیت حذف و موجودی‌ها اصلاح شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AccountTransferForm.Delete");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
        }
        finally
        {
            btnDelete.Enabled = true;
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AccountTransferForm"); }
        }
    }

    private void ExportToExcel()
    {
        if (IsDisposed || dgvTransfers.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد."); return; }
        using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = $"AccountTransfers_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx", Title = "ذخیره خروجی اکسل" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("انتقالات بین حساب‌های بانکی");
            ws.RightToLeft = true;

            string[] headers = { "از حساب", "به حساب", "مبلغ (ریال)", "تاریخ", "توضیحات" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int rowIndex = 2;
            foreach (DataGridViewRow dgvRow in dgvTransfers.Rows)
            {
                if (dgvRow.IsNewRow) continue;
                if (dgvRow.DataBoundItem is not AccountTransfer item) continue;

                ws.Cell(rowIndex, 1).Value = item.SourceAccountTitle ?? "";
                ws.Cell(rowIndex, 2).Value = item.DestinationAccountTitle ?? "";
                ws.Cell(rowIndex, 3).Value = item.Amount;
                ws.Cell(rowIndex, 4).Value = DateHelper.ToPersianDate(item.TransferDate);
                ws.Cell(rowIndex, 5).Value = item.Description ?? "";

                ws.Cell(rowIndex, 3).Style.NumberFormat.Format = "#,##0";
                ws.Range(rowIndex, 1, rowIndex, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(rowIndex, 1, rowIndex, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowIndex++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
            workbook.SaveAs(sfd.FileName);
            ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);

            if (ModernMessageBox.AskConfirm("فایل ذخیره شد. آیا می‌خواهید آن را باز کنید؟", "موفق"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AccountTransferForm.ExportToExcel");
            ModernMessageBox.ShowError("خطا در تولید فایل اکسل:\n" + ex.Message);
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "AccountTransfer_Import_Template.xlsx", Title = "ذخیره قالب اکسل انتقال وجه" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("انتقال وجه بانکی");
            ws.RightToLeft = true;

            string[] headers = { "از حساب (عنوان حساب بانکی مبدا)", "به حساب (عنوان حساب بانکی مقصد)", "مبلغ انتقال (ریال)", "تاریخ (شمسی 1403/01/01)", "توضیحات" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontName = "Tahoma";
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            ws.Cell(2, 1).Value = "جاری اصلی";
            ws.Cell(2, 2).Value = "سپرده کوتاه مدت";
            ws.Cell(2, 3).Value = 500000000;
            ws.Cell(2, 4).Value = DateHelper.ToPersianDate(DateTime.Now);
            ws.Cell(2, 5).Value = "انتقال بین حساب‌های بانکی";

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 35;
            workbook.SaveAs(sfd.FileName);
            ToastHelper.Show("قالب اکسل با موفقیت ساخته شد.", true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AccountTransferForm.DownloadTemplate");
            ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
        }
    }

    private async Task BtnImportExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل انتقال وجه" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        if (!await _actionLock.WaitAsync(0)) return;
        btnImportExcel.Enabled = false;

        try
        {
            int successCount = 0; int errorCount = 0; List<string> errors = new();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var accounts = (await _bankRepo.GetActiveAsync()).Where(a => !a.IsCashDesk).ToList();

                using var workbook = new XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheet(1);
                var rows = ws.RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    try
                    {
                        string srcTitle = row.Cell(1).GetFormattedString().Trim();
                        string destTitle = row.Cell(2).GetFormattedString().Trim();
                        string amountStr = row.Cell(3).GetFormattedString().Trim();
                        string dateStr = row.Cell(4).GetFormattedString().Trim();
                        string desc = row.Cell(5).GetFormattedString().Trim();

                        if (string.IsNullOrWhiteSpace(srcTitle) && string.IsNullOrWhiteSpace(destTitle)) continue;

                        var srcAcc = accounts.FirstOrDefault(a => a.Title == srcTitle || a.AccountNumber == srcTitle);
                        if (srcAcc is null) throw new Exception($"حساب بانکی مبدا «{srcTitle}» یافت نشد.");

                        var destAcc = accounts.FirstOrDefault(a => a.Title == destTitle || a.AccountNumber == destTitle);
                        if (destAcc is null) throw new Exception($"حساب بانکی مقصد «{destTitle}» یافت نشد.");

                        if (srcAcc.Id == destAcc.Id) throw new Exception("حساب مبدا و مقصد نمی‌توانند یکسان باشند.");

                        decimal amount = NumberHelper.ParseFormatted(amountStr);
                        if (amount <= 0) throw new Exception("مبلغ انتقال نامعتبر است.");

                        DateTime? date = DateHelper.ParsePersianDate(dateStr);
                        if (date is null && DateTime.TryParse(dateStr, out DateTime gDate)) date = gDate;
                        if (date is null) throw new Exception($"تاریخ «{dateStr}» معتبر نیست.");

                        var transfer = new AccountTransfer
                        {
                            SourceAccountId = srcAcc.Id,
                            DestinationAccountId = destAcc.Id,
                            Amount = amount,
                            TransferFee = 0,
                            TransferDate = date.Value,
                            Description = string.IsNullOrWhiteSpace(desc) ? "ایمپورت از اکسل" : desc,
                            TransferType = "BankToBank"
                        };

                        await _repository.AddAsync(transfer);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"سطر {row.RowNumber()}: {ex.Message}");
                    }
                }
            }, "در حال ایمپورت از اکسل...");

            if (success && !IsDisposed)
            {
                string msg = $"✅ تعداد {successCount} ردیف انتقال وجه بانکی با موفقیت ثبت شد.";
                if (errorCount > 0)
                {
                    msg += $"\n\n⚠️ {errorCount} ردیف ثبت نشد:\n" + string.Join("\n", errors.Take(5));
                    if (errors.Count > 5) msg += "\n... و موارد دیگر";
                    ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
                }
                else ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AccountTransferForm.ImportExcel");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
        }
        finally
        {
            btnImportExcel.Enabled = true;
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AccountTransferForm"); }
        }
    }
}
