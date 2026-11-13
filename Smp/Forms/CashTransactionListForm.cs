using ClosedXML.Excel;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class CashTransactionListForm : Form
    {
        private readonly CashTransactionRepository _repository;
        private readonly BankAccountRepository _bankRepo;
        private readonly AttachmentRepository _attachmentRepo;
        private readonly string _connectionString;

        private DataGridView dgv = null!;
        private TextBox txtSearch = null!;
        private ComboBox cmbAccountFilter = null!;
        private Button btnApplyFilter = null!;
        private System.Windows.Forms.Timer _searchTimer = null!;

        private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnPrint = null!, btnRefresh = null!;
        private Button btnDownloadTemplate = null!, btnImportExcel = null!, btnExportExcel = null!;

        private int _currentPage = 1;
        private const int _pageSize = 50;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private Button btnNextPage = null!, btnPrevPage = null!, btnFirstPage = null!, btnLastPage = null!;
        private Label lblPageInfo = null!;

        private string _sortColumn = "تاریخ";
        private bool _sortAscending = false;
        private bool _isInitializing = true;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public CashTransactionListForm(string connectionString)
        {
            _connectionString = connectionString;
            _repository = new CashTransactionRepository(connectionString);
            _bankRepo = new BankAccountRepository(connectionString);
            _attachmentRepo = new AttachmentRepository(connectionString);

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this);

            Load += async (s, e) => await InitializeFormAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _searchTimer?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private async Task InitializeFormAsync()
        {
            _isInitializing = true;
            await LoadAccountFilterAsync();
            _isInitializing = false;
            await LoadDataAsync();
        }

        private async Task LoadAccountFilterAsync()
        {
            try
            {
                var accounts = await _bankRepo.GetActiveAsync();
                var list = new List<CompanyBankAccount> { new CompanyBankAccount { Id = -1, Title = "همه حساب‌ها" } };
                list.AddRange(accounts);

                cmbAccountFilter.DataSource = null;
                cmbAccountFilter.DisplayMember = "Title";
                cmbAccountFilter.ValueMember = "Id";
                cmbAccountFilter.DataSource = list;
                cmbAccountFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "CashTransactionListForm.LoadAccountFilterAsync");
            }
        }

        private void BuildUI()
        {
            // ✅ استفاده از الگوی استاندارد لیست
            var (topRightPanel, topLeftPanel, grid, bottomPanel) = StandardFormHelper.SetupStandardListLayout(
                this, "لیست تراکنش‌های نقدی و حواله‌ها", new Size(1650, 800));

            dgv = grid;
            dgv.AutoGenerateColumns = false;

            // 🔴 اصلاح مهم: غیرفعال کردن حالت کشش خودکار کلی
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            // ✅ ترتیب ستون‌ها
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TableName", DataPropertyName = "TableName", Visible = false });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "تاریخ", DataPropertyName = "تاریخ", HeaderText = "تاریخ", Width = 90 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "نوع_پرداخت", DataPropertyName = "نوع_پرداخت", HeaderText = "روش پرداخت", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "محل_مصرف", DataPropertyName = "محل_مصرف", HeaderText = "نوع پرداخت", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "حساب_بانکی", DataPropertyName = "حساب_بانکی", HeaderText = "حساب / بانک", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ماهیت_هزینه", DataPropertyName = "ماهیت_هزینه", HeaderText = "ماهیت هزینه", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "جهت", DataPropertyName = "جهت", HeaderText = "جهت", Width = 70 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "طرف_حساب", DataPropertyName = "طرف_حساب", HeaderText = "طرف حساب", FillWeight = 20 });

            // ستون توضیحات با وزن بالا و حداقل عرض
            var colDesc = new DataGridViewTextBoxColumn { Name = "توضیحات", DataPropertyName = "توضیحات", HeaderText = "توضیحات", FillWeight = 30, MinimumWidth = 200 };
            dgv.Columns.Add(colDesc);

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "بدهکار", DataPropertyName = "بدهکار", HeaderText = "بدهکار (دریافتی)", Width = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "بستانکار", DataPropertyName = "بستانکار", HeaderText = "بستانکار (پرداختی)", Width = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "وضعیت", DataPropertyName = "وضعیت", HeaderText = "وضعیت", Width = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "پیوست", DataPropertyName = "پیوست", HeaderText = "پیوست", Width = 50 });

            // 🔴 تنظیم هوشمند عرض ستون‌ها
            dgv.Columns["تاریخ"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgv.Columns["نوع_پرداخت"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgv.Columns["جهت"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgv.Columns["بدهکار"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgv.Columns["بستانکار"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgv.Columns["وضعیت"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgv.Columns["پیوست"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

            dgv.Columns["محل_مصرف"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgv.Columns["حساب_بانکی"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgv.Columns["ماهیت_هزینه"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgv.Columns["طرف_حساب"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgv.Columns["توضیحات"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // جلوگیری از شکسته شدن کلمات در سلول‌ها
            dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            dgv.ColumnHeaderMouseClick += Dgv_ColumnHeaderMouseClick;
            dgv.CellFormatting += Dgv_CellFormatting;
            dgv.RowPrePaint += Dgv_RowPrePaint;
            dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && dgv.SelectedRows.Count == 1) OpenEditForm(); };

            // نوار بالا (پنل راست: صفحه‌بندی، پنل چپ: جستجو)
            lblPageInfo = new Label { Text = "صفحه 1 از 1", AutoSize = true, Font = UIHelper.PersianFont(10f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(10, 12, 0, 0) };

            btnFirstPage = UIHelper.CreateButton("⏮ اول", UIHelper.SectionGray, 70, 32);
            btnFirstPage.Margin = new Padding(3, 5, 3, 5);
            btnFirstPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage = 1; await LoadDataAsync(); } };

            btnPrevPage = UIHelper.CreateButton("◀ قبلی", UIHelper.SectionBlue, 80, 32);
            btnPrevPage.Margin = new Padding(3, 5, 3, 5);
            btnPrevPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage--; await LoadDataAsync(); } };

            btnNextPage = UIHelper.CreateButton("بعدی ▶", UIHelper.SectionBlue, 80, 32);
            btnNextPage.Margin = new Padding(3, 5, 3, 5);
            btnNextPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage++; await LoadDataAsync(); } };

            btnLastPage = UIHelper.CreateButton("آخر ⏭", UIHelper.SectionGray, 70, 32);
            btnLastPage.Margin = new Padding(3, 5, 3, 5);
            btnLastPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage = _totalPages; await LoadDataAsync(); } };

            topRightPanel.Controls.Add(lblPageInfo);
            topRightPanel.Controls.Add(btnFirstPage);
            topRightPanel.Controls.Add(btnPrevPage);
            topRightPanel.Controls.Add(btnNextPage);
            topRightPanel.Controls.Add(btnLastPage);

            var lblSearch = new Label { Text = "🔍 جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(0, 10, 5, 0) };
            txtSearch = new TextBox { Width = 200, Font = UIHelper.PersianFont(11f), PlaceholderText = "جستجو (شرح، طرف حساب...)", Margin = new Padding(0, 6, 15, 0) };
            _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); _currentPage = 1; await LoadDataAsync(); };
            txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            var lblAccount = new Label { Text = "حساب:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(0, 10, 5, 0) };
            cmbAccountFilter = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(0, 6, 5, 0) };
            cmbAccountFilter.SelectedIndexChanged += async (s, e) => {
                if (!_isInitializing) { _currentPage = 1; await LoadDataAsync(); }
            };

            topLeftPanel.Controls.Add(lblSearch);
            topLeftPanel.Controls.Add(txtSearch);
            topLeftPanel.Controls.Add(lblAccount);
            topLeftPanel.Controls.Add(cmbAccountFilter);

            // دکمه‌های عملیات (پایین)
            btnAdd = UIHelper.CreateButton("➕ ثبت تراکنش جدید", UIHelper.ActionGreen, 150, 42);
            btnAdd.Click += BtnAdd_Click;

            btnEdit = UIHelper.CreateButton("✏️ ویرایش", UIHelper.ActionBlue, 110, 42);
            btnEdit.Click += BtnEdit_Click;

            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.ActionRed, 100, 42);
            btnDelete.Click += BtnDelete_Click;

            btnPrint = UIHelper.CreateButton("🖨️ دستور پرداخت", UIHelper.ActionAmber, 150, 42);
            btnPrint.Click += BtnPrint_Click;

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 42);
            btnRefresh.Click += async (s, e) => { _currentPage = 1; txtSearch.Clear(); await LoadDataAsync(); };

            btnExportExcel = UIHelper.CreateButton("📤 خروجی اکسل", UIHelper.SectionPurple, 130, 42);
            btnExportExcel.Click += BtnExportExcel_Click;

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب", UIHelper.WarmSecondary, 130, 42);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            btnImportExcel = UIHelper.CreateButton("📥 ایمپورت اکسل", UIHelper.SectionTeal, 130, 42);
            btnImportExcel.Click += BtnImportExcel_Click;

            bottomPanel.Controls.Add(btnAdd);
            bottomPanel.Controls.Add(btnEdit);
            bottomPanel.Controls.Add(btnDelete);
            bottomPanel.Controls.Add(btnPrint);
            bottomPanel.Controls.Add(btnRefresh);
            bottomPanel.Controls.Add(btnExportExcel);
            bottomPanel.Controls.Add(btnDownloadTemplate);
            bottomPanel.Controls.Add(btnImportExcel);
        }

        private void Dgv_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].Name;

            if (_sortColumn == colName)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = colName;
                _sortAscending = true;
            }

            _currentPage = 1;
            _ = LoadDataAsync();
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].Name;

            if ((colName == "بدهکار" || colName == "بستانکار") && e.Value is decimal amount)
            {
                e.Value = NumberHelper.FormatThousands(amount, usePersianDigits: true);
                e.FormattingApplied = true;
            }
            else if (colName == "نوع_پرداخت")
            {
                string val = e.Value?.ToString() ?? "";
                if (val == "انتقال")
                {
                    e.CellStyle.ForeColor = Color.Purple;
                    e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
                }
            }
            else if (colName == "محل_مصرف")
            {
                string val = e.Value?.ToString() ?? "";
                if (val.Contains("متفرقه")) { e.CellStyle.ForeColor = Color.FromArgb(153, 27, 27); e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("اداری")) { e.CellStyle.ForeColor = UIHelper.ActionGreen; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("پروژه") || val.Contains("🏗️")) { e.CellStyle.ForeColor = UIHelper.SectionBlue; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else { e.CellStyle.ForeColor = UIHelper.ActionRed; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
            }
            else if (colName == "وضعیت")
            {
                string val = e.Value?.ToString() ?? "";
                if (val.Contains("در انتظار")) { e.CellStyle.ForeColor = Color.FromArgb(153, 27, 27); e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("صورت وضعیت شده")) { e.CellStyle.ForeColor = UIHelper.ActionGreen; }
            }
        }

        private void Dgv_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgv.Rows[e.RowIndex];
            string accountTitle = row.Cells["حساب_بانکی"].Value?.ToString() ?? "";

            if (accountTitle.Contains("تنخواه"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 247, 230); // کرم ملایم
                return;
            }

            var tableName = row.Cells["TableName"].Value?.ToString() ?? "";
            var type = row.Cells["نوع_پرداخت"].Value?.ToString() ?? "";
            var usage = row.Cells["محل_مصرف"].Value?.ToString() ?? "";

            if (tableName == "AccountTransfers" || (tableName == "CashTransactions" && type == "انتقال"))
                row.DefaultCellStyle.BackColor = Color.FromArgb(245, 235, 255);
            else if (tableName == "CashChecks")
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 248, 255);
            else if (usage.Contains("متفرقه"))
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226);
            else if (usage.Contains("اداری"))
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
            else if (usage.Contains("پروژه") || usage.Contains("🏗️"))
                row.DefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
            else
                row.DefaultCellStyle.BackColor = Color.White;
        }

        private int? GetSelectedAccountId()
        {
            if (cmbAccountFilter.SelectedItem is CompanyBankAccount selectedAcc)
            {
                return selectedAcc.Id != -1 ? selectedAcc.Id : null;
            }
            return null;
        }

        private async Task LoadDataAsync()
        {
            if (IsDisposed) return;

            await _loadLock.WaitAsync();

            try
            {
                string search = txtSearch.Text.Trim();
                string sortDir = _sortAscending ? "ASC" : "DESC";
                int? accountId = GetSelectedAccountId();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var (list, totalCount) = await _repository.GetPagedAsync(_currentPage, _pageSize, search, _sortColumn, sortDir, accountId);
                    _totalCount = totalCount;
                    _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
                    if (_totalPages == 0) _totalPages = 1;

                    if (IsDisposed) return;

                    var ids = list.Select(t => t.Id).ToList();
                    var transIdsWithAttachments = await _attachmentRepo.GetAttachedEntityKeysAsync(new List<string> { "CashTransaction", "CashCheck" }, ids);

                    var displayData = list.Select(t => new
                    {
                        t.Id,
                        t.TableName,
                        تاریخ = DateHelper.ToPersianDate(t.TransactionDate),
                        نوع_پرداخت = t.TransactionType,
                        محل_مصرف = t.PaymentScopeDisplay,
                        حساب_بانکی = t.AccountTitle ?? "---",
                        ماهیت_هزینه = t.CostType ?? "---",
                        جهت = t.Direction,
                        طرف_حساب = t.TafsiliTitle ?? t.RecipientName ?? "---",
                        توضیحات = t.Description ?? "---",
                        بدهکار = t.Direction == "دریافتی" ? t.Amount : (decimal?)null,
                        بستانکار = t.Direction == "پرداختی" ? t.Amount : (decimal?)null,
                        وضعیت = t.BillingStatus,
                        پیوست = transIdsWithAttachments.Contains($"CashTransaction_{t.Id}") ||
                                 transIdsWithAttachments.Contains($"CashCheck_{t.Id}") ? "📎" : ""
                    }).ToList();

                    dgv.DataSource = displayData;

                    foreach (DataGridViewColumn col in dgv.Columns)
                        col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;

                    if (dgv.Columns.Contains(_sortColumn))
                        dgv.Columns[_sortColumn].HeaderCell.SortGlyphDirection = _sortAscending ? System.Windows.Forms.SortOrder.Ascending : System.Windows.Forms.SortOrder.Descending;

                    lblPageInfo.Text = $"صفحه {NumberHelper.FormatThousands(_currentPage, usePersianDigits: true)} از {NumberHelper.FormatThousands(_totalPages, usePersianDigits: true)}  (کل: {NumberHelper.FormatThousands(_totalCount, usePersianDigits: true)} رکورد)";

                    btnFirstPage.Enabled = _currentPage > 1;
                    btnPrevPage.Enabled = _currentPage > 1;
                    btnNextPage.Enabled = _currentPage < _totalPages;
                    btnLastPage.Enabled = _currentPage < _totalPages;

                }, "در حال دریافت لیست تراکنش‌ها...");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "CashTransactionListForm.LoadDataAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            using (var form = new CashTransactionEditForm(_connectionString))
            {
                if (form.ShowDialog() == DialogResult.OK) { _currentPage = 1; _ = LoadDataAsync(); }
            }
        }

        private void BtnEdit_Click(object? sender, EventArgs e) => OpenEditForm();

        private void OpenEditForm()
        {
            if (dgv.CurrentRow == null) { ModernMessageBox.ShowWarning("لطفاً یک تراکنش را انتخاب کنید."); return; }

            int id = Convert.ToInt32(dgv.CurrentRow.Cells["Id"].Value);
            string desc = dgv.CurrentRow.Cells["توضیحات"].Value?.ToString() ?? "";
            string tableName = dgv.CurrentRow.Cells["TableName"].Value?.ToString() ?? "CashTransactions";

            if (tableName == "AccountTransfers" || desc.Contains("[TransferId:"))
            {
                ModernMessageBox.ShowWarning("این رکورد یک «انتقال بین حساب‌ها» است.\nبرای تغییر یا حذف آن، لطفاً از منوی «انتقال بین حساب‌ها» استفاده کنید.");
                return;
            }

            if (tableName == "CashChecks")
            {
                using (var form = new CashCheckEditForm(_connectionString, id))
                {
                    if (form.ShowDialog() == DialogResult.OK) _ = LoadDataAsync();
                }
            }
            else
            {
                using (var form = new CashTransactionEditForm(_connectionString, id))
                {
                    if (form.ShowDialog() == DialogResult.OK) _ = LoadDataAsync();
                }
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (dgv.SelectedRows.Count == 0) { ModernMessageBox.ShowWarning("لطفاً حداقل یک تراکنش را انتخاب کنید."); return; }

            foreach (DataGridViewRow row in dgv.SelectedRows)
            {
                string desc = row.Cells["توضیحات"].Value?.ToString() ?? "";
                string tableName = row.Cells["TableName"].Value?.ToString() ?? "CashTransactions";

                if (tableName == "AccountTransfers" || desc.Contains("[TransferId:"))
                {
                    ModernMessageBox.ShowWarning("شما نمی‌توانید «انتقال بین حساب‌ها» را از این لیست حذف کنید.\nبرای حذف، از منوی «انتقال بین حساب‌ها» استفاده کنید.");
                    return;
                }
            }

            string confirmMsg = $"آیا از حذف {dgv.SelectedRows.Count} تراکنش انتخاب شده مطمئن هستید؟";
            if (!ModernMessageBox.AskConfirm(confirmMsg, "حذف تراکنش‌ها")) return;

            var transIds = dgv.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Cells["TableName"].Value?.ToString() == "CashTransactions").Select(r => Convert.ToInt32(r.Cells["Id"].Value)).ToList();
            var checkIds = dgv.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Cells["TableName"].Value?.ToString() == "CashChecks").Select(r => Convert.ToInt32(r.Cells["Id"].Value)).ToList();

            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (transIds.Any()) await _repository.DeleteBatchAsync(transIds);
                    if (checkIds.Any()) await new CashCheckRepository(_connectionString).DeleteBatchAsync(checkIds);
                }, "در حال حذف...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show($"{dgv.SelectedRows.Count} تراکنش حذف شد.", isSuccess: true);
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "CashTransactionListForm.BtnDelete_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
            }
        }

        private async void BtnPrint_Click(object? sender, EventArgs e)
        {
            if (IsDisposed || dgv.SelectedRows.Count == 0) return;
            try
            {
                var selectedCashRows = dgv.SelectedRows.Cast<DataGridViewRow>()
                    .Where(r => r.Cells["TableName"].Value?.ToString() == "CashTransactions")
                    .ToList();

                if (selectedCashRows.Count == 0)
                {
                    ModernMessageBox.ShowWarning("هیچ تراکنش نقدی برای چاپ انتخاب نشده است.\n\n• برای چاپ چک‌ها، از «لیست و مدیریت چک‌ها» استفاده کنید.\n• برای انتقال‌ها، از «انتقال بین حساب‌ها» استفاده کنید.");
                    return;
                }

                var selectedIds = selectedCashRows.Select(r => Convert.ToInt32(r.Cells["Id"].Value)).ToList();
                var selectedTrans = new List<CashTransaction>();
                foreach (var id in selectedIds)
                {
                    var trans = await _repository.GetByIdAsync(id);
                    if (trans != null) selectedTrans.Add(trans);
                }

                if (!selectedTrans.Any()) return;

                var accountIds = selectedTrans.Where(t => t.AccountId.HasValue).Select(t => t.AccountId!.Value).Distinct().ToList();

                if (accountIds.Count == 0) { ModernMessageBox.ShowWarning("هیچ‌کدام از تراکنش‌های انتخاب‌شده به یک حساب بانکی متصل نیستند."); return; }
                if (accountIds.Count > 1) { ModernMessageBox.ShowWarning("تراکنش‌های انتخاب‌شده از چند حساب مختلف هستند.\nلطفاً تراکنش‌های یک حساب را انتخاب کنید."); return; }

                int accountId = accountIds[0];
                var accounts = await _bankRepo.GetAllAsync();
                var account = accounts.FirstOrDefault(a => a.Id == accountId);

                if (account == null) { ModernMessageBox.ShowWarning($"حساب بانکی مرتبط با این تراکنش‌ها در سیستم وجود ندارد."); return; }
                if (!account.IsActive) { ModernMessageBox.ShowWarning($"حساب بانکی «{account.Title}» غیرفعال شده است."); return; }

                using (var printForm = new PaymentOrderForm(selectedTrans, account)) { printForm.ShowDialog(); }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "CashTransactionListForm.BtnPrint_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در چاپ:\n" + ex.Message);
            }
        }

        private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "CashTransactions_Import_Template.xlsx", Title = "ذخیره قالب اکسل" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("تراکنش‌ها");
                ws.RightToLeft = true;
                string[] headers = { "تاریخ (شمسی 1403/01/01)", "حساب بانکی (عنوان)", "جهت (پرداختی/دریافتی)", "روش (نقدی/حواله)", "مبلغ (ریال)", "کد تفصیلی گیرنده", "شماره قرارداد", "دسته‌بندی", "نوع هزینه", "نوع پرداخت (اداری/پروژه/پیمانکار)", "توضیحات" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
                ws.Cell(2, 2).Value = "جاری اصلی";
                ws.Cell(2, 3).Value = "پرداختی";
                ws.Cell(2, 4).Value = "حواله";
                ws.Cell(2, 5).Value = 5000000;
                ws.Cell(2, 6).Value = "1001";
                ws.Cell(2, 7).Value = "CL-1001";
                ws.Cell(2, 8).Value = "اداری";
                ws.Cell(2, 9).Value = "خدمات";
                ws.Cell(2, 10).Value = "اداری";
                ws.Columns().AdjustToContents();
                workbook.SaveAs(sfd.FileName);
                ToastHelper.Show("قالب اکسل ساخته شد.", true);
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        }

        private async void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل تراکنش‌ها" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            var accounts = await _bankRepo.GetActiveAsync();
            using var selectAccountForm = new Form();
            selectAccountForm.Text = "انتخاب حساب مبدا";
            selectAccountForm.Size = new Size(400, 200);
            selectAccountForm.StartPosition = FormStartPosition.CenterParent;
            selectAccountForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            selectAccountForm.MaximizeBox = false;
            selectAccountForm.MinimizeBox = false;
            selectAccountForm.RightToLeft = RightToLeft.Yes;
            selectAccountForm.RightToLeftLayout = true;
            selectAccountForm.Font = UIHelper.PersianFont(11f);

            var lbl = new Label { Text = "این تراکنش‌ها مربوط به کدام حساب است؟", Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
            var cmb = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id" };
            cmb.DataSource = accounts;
            var btnOk = UIHelper.CreateButton("تایید و ادامه", UIHelper.ActionGreen, 120, 35);
            btnOk.Dock = DockStyle.Bottom;
            btnOk.Click += (s, ev) => { selectAccountForm.DialogResult = DialogResult.OK; selectAccountForm.Close(); };

            selectAccountForm.Controls.Add(btnOk); selectAccountForm.Controls.Add(cmb); selectAccountForm.Controls.Add(lbl);

            if (selectAccountForm.ShowDialog(this) != DialogResult.OK || cmb.SelectedItem == null) return;
            var selectedAccount = (CompanyBankAccount)cmb.SelectedItem;

            List<CashTransaction> transactions = new();
            bool readSuccess = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var workbook = new XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheet(1);
                var rows = ws.RowsUsed().ToList();
                if (rows.Count < 2) return;

                var headerRow = rows.First();
                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.CellsUsed())
                {
                    string text = cell.GetString().Trim();
                    if (!string.IsNullOrEmpty(text) && !headers.ContainsKey(text))
                        headers[text] = cell.Address.ColumnNumber;
                }

                int GetColIndex(string containsText)
                {
                    var match = headers.FirstOrDefault(h => h.Key.Contains(containsText, StringComparison.OrdinalIgnoreCase));
                    return match.Value > 0 ? match.Value : -1;
                }

                int dateCol = GetColIndex("تاریخ");
                int amountCol = GetColIndex("مبلغ");
                int directionCol = GetColIndex("جهت");
                int methodCol = GetColIndex("روش");
                int descCol = GetColIndex("توضیحات");
                int costTypeCol = GetColIndex("نوع هزینه");
                int categoryCol = GetColIndex("دسته‌بندی");
                int tafsiliCol = GetColIndex("کد تفصیلی");
                int contractCol = GetColIndex("قرارداد");

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                foreach (var row in rows.Skip(1))
                {
                    string dateStr = dateCol > 0 ? row.Cell(dateCol).GetString().Trim() : "";
                    string amountStr = amountCol > 0 ? row.Cell(amountCol).GetString().Trim() : "";
                    string directionStr = directionCol > 0 ? row.Cell(directionCol).GetString().Trim() : "";
                    string methodStr = methodCol > 0 ? row.Cell(methodCol).GetString().Trim() : "حواله";
                    string desc = descCol > 0 ? row.Cell(descCol).GetString().Trim() : "";
                    string costType = costTypeCol > 0 ? row.Cell(costTypeCol).GetString().Trim() : "سایر";
                    string category = categoryCol > 0 ? row.Cell(categoryCol).GetString().Trim() : "سایر";
                    string tafsiliCode = tafsiliCol > 0 ? row.Cell(tafsiliCol).GetString().Trim() : "";
                    string contractNum = contractCol > 0 ? row.Cell(contractCol).GetString().Trim() : "";

                    if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

                    DateTime? date = DateHelper.ParsePersianDate(dateStr);
                    if (date == null) date = DateTime.Today;

                    decimal amount = NumberHelper.ParseFormatted(amountStr);
                    if (amount == 0) continue;

                    string direction = directionStr.Contains("دریافتی") ? "دریافتی" : "پرداختی";
                    string method = string.IsNullOrWhiteSpace(methodStr) ? "حواله" : methodStr;

                    int? contractId = null;
                    string contractTitle = null;
                    string paymentScope = PaymentScopes.Administrative;

                    if (!string.IsNullOrWhiteSpace(contractNum))
                    {
                        using var cmdC = new SqlCommand("SELECT TOP 1 Id, Subject FROM ClientContracts WHERE ContractNumber = @num", conn);
                        cmdC.Parameters.AddWithValue("@num", contractNum);
                        using var r = await cmdC.ExecuteReaderAsync();
                        if (await r.ReadAsync())
                        {
                            contractId = r.GetInt32(0);
                            contractTitle = r.IsDBNull(1) ? "" : r.GetString(1);
                            paymentScope = PaymentScopes.DirectPurchase;
                        }
                        else
                        {
                            contractTitle = $"قرارداد نامشخص ({contractNum})";
                        }
                    }

                    transactions.Add(new CashTransaction
                    {
                        AccountId = selectedAccount.Id,
                        AccountTitle = selectedAccount.Title,
                        TransactionDate = date.Value,
                        Amount = amount,
                        Direction = direction,
                        TransactionType = method,
                        PaymentScope = paymentScope,
                        ContractId = contractId,
                        ContractTitle = contractTitle,
                        CostType = string.IsNullOrWhiteSpace(costType) ? "سایر" : costType,
                        Category = string.IsNullOrWhiteSpace(category) ? "سایر" : category,
                        TafsiliCode = string.IsNullOrWhiteSpace(tafsiliCode) ? null : tafsiliCode,
                        Description = desc
                    });
                }
            }, "در حال خواندن فایل اکسل...");

            if (!readSuccess || transactions.Count == 0)
            {
                ModernMessageBox.ShowWarning("هیچ داده معتبری در فایل اکسل یافت نشد.\n\nلطفاً مطمئن شوید که فایل طبق قالب دانلود شده پر شده است (ستون‌های تاریخ و مبلغ حتماً باید مقدار داشته باشند).");
                return;
            }

            if (!ModernMessageBox.AskConfirm($"تعداد {transactions.Count} تراکنش خوانده شد.\nآیا از ثبت آن‌ها در دیتابیس مطمئن هستید؟", "تایید ایمپورت")) return;

            bool saveSuccess = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.BulkInsertAsync(transactions);
            }, "در حال درج سریع در دیتابیس...");

            if (saveSuccess && !IsDisposed)
            {
                ToastHelper.Show($"{transactions.Count} تراکنش با موفقیت وارد شد.", true);
                _currentPage = 1;
                await LoadDataAsync();
            }
        }

        private async void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"لیست_تراکنش‌ها_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Title = "ذخیره خروجی اکسل",
                OverwritePrompt = true
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            string search = txtSearch.Text.Trim();
            string sortDir = _sortAscending ? "ASC" : "DESC";
            int? accountId = GetSelectedAccountId();

            string filterText = accountId.HasValue ? $"فیلتر حساب: {cmbAccountFilter.Text}" : "همه حساب‌ها";

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var (allList, _) = await _repository.GetPagedAsync(1, 1000000, search, _sortColumn, sortDir, accountId);
                if (allList.Count == 0) return;

                var ids = allList.Select(t => t.Id).ToList();
                var transIdsWithAttachments = await _attachmentRepo.GetAttachedEntityKeysAsync(new List<string> { "CashTransaction", "CashCheck" }, ids);

                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();
                    var ws = workbook.Worksheets.Add("تراکنش‌ها");
                    ws.RightToLeft = true;

                    string[] headers = { "تاریخ", "نوع پرداخت", "محل مصرف", "حساب / بانک", "ماهیت هزینه", "جهت", "طرف حساب", "توضیحات", "بدهکار (ریال)", "بستانکار (ریال)", "وضعیت صورت وضعیت", "پیوست" };
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
                    foreach (var t in allList)
                    {
                        string scopeDisplay = t.PaymentScopeDisplay;
                        string tafsiliName = t.TafsiliTitle ?? t.RecipientName ?? "---";

                        ws.Cell(rowIndex, 1).Value = DateHelper.ToPersianDate(t.TransactionDate);
                        ws.Cell(rowIndex, 2).Value = t.TransactionType;
                        ws.Cell(rowIndex, 3).Value = scopeDisplay;
                        ws.Cell(rowIndex, 4).Value = t.AccountTitle ?? "---";
                        ws.Cell(rowIndex, 5).Value = t.CostType ?? "---";
                        ws.Cell(rowIndex, 6).Value = t.Direction;
                        ws.Cell(rowIndex, 7).Value = tafsiliName;
                        ws.Cell(rowIndex, 8).Value = t.Description ?? "---";

                        if (t.Direction == "دریافتی") ws.Cell(rowIndex, 9).Value = t.Amount;
                        else ws.Cell(rowIndex, 9).Value = "";

                        if (t.Direction == "پرداختی") ws.Cell(rowIndex, 10).Value = t.Amount;
                        else ws.Cell(rowIndex, 10).Value = "";

                        ws.Cell(rowIndex, 11).Value = t.BillingStatus;
                        ws.Cell(rowIndex, 12).Value = (transIdsWithAttachments.Contains($"CashTransaction_{t.Id}") || transIdsWithAttachments.Contains($"CashCheck_{t.Id}")) ? "📎" : "";

                        ws.Cell(rowIndex, 9).Style.NumberFormat.Format = "#,##0";
                        ws.Cell(rowIndex, 10).Style.NumberFormat.Format = "#,##0";
                        ws.Range(rowIndex, 1, rowIndex, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Range(rowIndex, 1, rowIndex, 12).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                        rowIndex++;
                    }

                    ws.Columns().AdjustToContents();
                    ws.SheetView.FreezeRows(1);
                    workbook.SaveAs(sfd.FileName);
                });
            }, "در حال تولید فایل اکسل...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"فایل اکسل بر اساس {filterText} ذخیره شد.", isSuccess: true);

                if (ModernMessageBox.AskConfirm("فایل ذخیره شد. آیا می‌خواهید آن را باز کنید؟", "موفق"))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
        }
    }
}