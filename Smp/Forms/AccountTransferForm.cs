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
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
{
    public class AccountTransferForm : Form
    {
        private readonly AccountTransferRepository _repository;
        private readonly BankAccountRepository _bankRepo;
        private readonly CashTransactionRepository _cashTransRepo;
        private readonly string _connectionString;
        private readonly int? _cashTransactionId;

        private ComboBox cmbSourceAccount = null!, cmbDestinationAccount = null!;
        private TextBox txtAmount = null!, txtDate = null!, txtDescription = null!;

        private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!, btnDelete = null!, btnExportExcel = null!;
        private DataGridView dgvTransfers = null!;
        private TextBox txtSearch = null!;

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
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F)); // پنل ورودی
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));  // نوار ابزار
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // جدول
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));  // دکمه‌های پایین

            // ═══════════ ۱. پنل ورودی ═══════════
            var inputContainer = UIHelper.CreateWarmPanel();

            var formLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 3,
                Padding = new Padding(10, 15, 10, 10)
            };

            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));    // Spacer Right
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));  // لیبل مبدا
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));  // کمبوی مبدا
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));   // فلش
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));  // لیبل مقصد
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));  // کمبوی مقصد
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));    // Spacer Left

            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            cmbSourceAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };
            cmbDestinationAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };

            var lblArrow = new Label { Text = "⬅", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue };

            // ردیف اول: از حساب ➡ به حساب
            formLayout.Controls.Add(new Label { Text = "از حساب (مبدا):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 1, 0);
            formLayout.Controls.Add(cmbSourceAccount, 2, 0);
            formLayout.Controls.Add(lblArrow, 3, 0);
            formLayout.Controls.Add(new Label { Text = "به حساب (مقصد):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 4, 0);
            formLayout.Controls.Add(cmbDestinationAccount, 5, 0);

            // ردیف دوم: مبلغ / تاریخ
            txtAmount = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtDate = new TextBox { Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f) };
            DateHelper.ApplyDateMask(txtDate);
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            formLayout.Controls.Add(new Label { Text = "مبلغ (ریال):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 1, 1);
            formLayout.Controls.Add(txtAmount, 2, 1);
            formLayout.Controls.Add(new Label { Text = "تاریخ:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 4, 1);
            formLayout.Controls.Add(txtDate, 5, 1);

            // ردیف سوم: توضیحات (تمام عرض)
            txtDescription = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };
            formLayout.Controls.Add(new Label { Text = "توضیحات:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 1, 2);
            formLayout.Controls.Add(txtDescription, 2, 2);
            formLayout.SetColumnSpan(txtDescription, 4);

            inputContainer.Controls.Add(formLayout);
            mainLayout.Controls.Add(inputContainer, 0, 0);

            // ═══════════ ۲. نوار ابزار ═══════════
            var pnlToolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UIHelper.WarmPanelBackground };
            pnlToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // سمت راست
            pnlToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460F)); // سمت چپ (عرض ثابت برای 3 دکمه)

            // سمت راست: جستجو
            var searchFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 5, 0) };
            var lblSearch = new Label { Text = "🔍 جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(0, 6, 10, 0) };
            txtSearch = new TextBox { Width = 250, Font = UIHelper.PersianFont(11f), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 3, 0, 0), PlaceholderText = "جستجو در لیست..." };
            txtSearch.TextChanged += (s, e) => ApplyFilterAndSort();
            searchFlow.Controls.Add(lblSearch);
            searchFlow.Controls.Add(txtSearch);
            pnlToolbar.Controls.Add(searchFlow, 0, 0);

            // سمت چپ: دکمه‌های اکسل
            var excelFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(5, 10, 0, 0) };
            btnDownloadTemplate = UIHelper.CreateButton("📥 قالب اکسل", UIHelper.SectionBlue, 110, 35);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.WarmSecondary, 130, 35);
            btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);
            btnExportExcel = UIHelper.CreateButton("📊 خروجی اکسل", UIHelper.SectionTeal, 120, 35);
            btnExportExcel.Click += (s, e) => ExportToExcel();

            excelFlow.Controls.Add(btnDownloadTemplate);
            excelFlow.Controls.Add(btnImportExcel);
            excelFlow.Controls.Add(btnExportExcel);
            pnlToolbar.Controls.Add(excelFlow, 1, 0);

            mainLayout.Controls.Add(pnlToolbar, 0, 1);

            // ═══════════ ۳. جدول ═══════════
            dgvTransfers = new DataGridView { Dock = DockStyle.Fill, MultiSelect = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Margin = new Padding(0, 5, 0, 5) };
            dgvTransfers.ApplyModernStyle();
            dgvTransfers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            foreach (DataGridViewColumn col in dgvTransfers.Columns)
                col.SortMode = DataGridViewColumnSortMode.Programmatic;

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
            mainLayout.Controls.Add(dgvTransfers, 0, 2);

            // ═══════════ ۴. دکمه‌های پایین ═══════════
            var pnlBottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UIHelper.WarmPanelBackground };
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450F)); // سمت راست (دکمه‌ها)
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // سمت چپ (فاصله)

            var flowBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };

            btnSave = UIHelper.CreateButton("💾 ثبت انتقال وجه", UIHelper.ActionGreen, 180, 45);
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            btnDelete = UIHelper.CreateButton("🗑️ حذف ردیف‌های انتخاب‌شده", UIHelper.ActionRed, 200, 45);
            btnDelete.Click += async (s, e) => await DeleteSafeAsync();

            flowBtns.Controls.Add(btnSave);
            flowBtns.Controls.Add(btnDelete);

            pnlBottom.Controls.Add(flowBtns, 0, 0);
            mainLayout.Controls.Add(pnlBottom, 0, 3);

            this.Controls.Add(mainLayout);
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
                        if (trans != null)
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
                if (property != null)
                {
                    if (_sortAscending) query = query.OrderBy(x => property.GetValue(x, null));
                    else query = query.OrderByDescending(x => property.GetValue(x, null));
                }
            }
            else
            {
                query = query.OrderByDescending(t => t.TransferDate).ThenByDescending(t => t.Id);
            }

            dgvTransfers.DataSource = new BindingList<AccountTransfer>(query.ToList());
            ConfigureGridColumns();
        }

        private void ConfigureGridColumns()
        {
            if (dgvTransfers.Columns["Id"] != null) dgvTransfers.Columns["Id"].Visible = false;
            if (dgvTransfers.Columns["SourceAccountId"] != null) dgvTransfers.Columns["SourceAccountId"].Visible = false;
            if (dgvTransfers.Columns["DestinationAccountId"] != null) dgvTransfers.Columns["DestinationAccountId"].Visible = false;
            if (dgvTransfers.Columns["CreatedAt"] != null) dgvTransfers.Columns["CreatedAt"].Visible = false;
            if (dgvTransfers.Columns["TransferFee"] != null) dgvTransfers.Columns["TransferFee"].Visible = false;
            if (dgvTransfers.Columns["TransferType"] != null) dgvTransfers.Columns["TransferType"].Visible = false;

            if (dgvTransfers.Columns["SourceAccountTitle"] != null) { dgvTransfers.Columns["SourceAccountTitle"].HeaderText = "از حساب"; dgvTransfers.Columns["SourceAccountTitle"].FillWeight = 20; }
            if (dgvTransfers.Columns["DestinationAccountTitle"] != null) { dgvTransfers.Columns["DestinationAccountTitle"].HeaderText = "به حساب"; dgvTransfers.Columns["DestinationAccountTitle"].FillWeight = 20; }
            if (dgvTransfers.Columns["Amount"] != null) { dgvTransfers.Columns["Amount"].HeaderText = "مبلغ"; dgvTransfers.Columns["Amount"].FillWeight = 15; }
            if (dgvTransfers.Columns["TransferDate"] != null) { dgvTransfers.Columns["TransferDate"].HeaderText = "تاریخ"; dgvTransfers.Columns["TransferDate"].FillWeight = 15; }
            if (dgvTransfers.Columns["Description"] != null) { dgvTransfers.Columns["Description"].HeaderText = "توضیحات"; dgvTransfers.Columns["Description"].FillWeight = 30; }
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

            btnSave.Text = "💾 ذخیره تغییرات";
            btnSave.BackColor = UIHelper.SectionBlue;
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;
            btnSave.Enabled = false;

            try
            {
                if (cmbSourceAccount.SelectedValue == null || cmbDestinationAccount.SelectedValue == null)
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
                if (date == null)
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
                        if (res != null && res != DBNull.Value) actualBalance = Convert.ToDecimal(res);
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

                var transfer = new AccountTransfer
                {
                    Id = _editingTransferId ?? 0,
                    SourceAccountId = sourceId,
                    DestinationAccountId = destId,
                    Amount = amount,
                    TransferFee = 0,
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
                    btnSave.Text = "💾 ثبت انتقال وجه";
                    btnSave.BackColor = UIHelper.ActionGreen;
                    txtAmount.Clear();
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
                try { _actionLock.Release(); } catch { }
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
                    .Where(t => t != null)
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
                try { _actionLock.Release(); } catch { }
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
                            if (srcAcc == null) throw new Exception($"حساب بانکی مبدا «{srcTitle}» یافت نشد.");

                            var destAcc = accounts.FirstOrDefault(a => a.Title == destTitle || a.AccountNumber == destTitle);
                            if (destAcc == null) throw new Exception($"حساب بانکی مقصد «{destTitle}» یافت نشد.");

                            if (srcAcc.Id == destAcc.Id) throw new Exception("حساب مبدا و مقصد نمی‌توانند یکسان باشند.");

                            decimal amount = NumberHelper.ParseFormatted(amountStr);
                            if (amount <= 0) throw new Exception("مبلغ انتقال نامعتبر است.");

                            DateTime? date = DateHelper.ParsePersianDate(dateStr);
                            if (date == null && DateTime.TryParse(dateStr, out DateTime gDate)) date = gDate;
                            if (date == null) throw new Exception($"تاریخ «{dateStr}» معتبر نیست.");

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
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}