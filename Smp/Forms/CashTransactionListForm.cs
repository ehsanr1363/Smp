using ClosedXML.Excel;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Smp.Forms;

public class CashTransactionListForm : Form
{
    private readonly CashTransactionRepository _repository;
    private readonly BankAccountRepository _bankRepo;
    private readonly AttachmentRepository _attachmentRepo;
    private readonly string _connectionString;

    private DataGridView dgv = null!;
    private ModernTextBox txtSearch = null!;
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

    private ModernStatCard cardTotal = null!, cardIn = null!, cardOut = null!, cardNet = null!;
    private readonly Dictionary<string, string> _headers = new();
    private readonly Dictionary<string, Color> _methodTones = new();
    private readonly Dictionary<string, Color> _scopeTones = new();
    private readonly Dictionary<string, Color> _statusTones = new();
    private static readonly Dictionary<string, Color> _directionTones = new()
    {
        ["دریافتی"] = ModernTheme.Success,
        ["پرداختی"] = ModernTheme.Danger
    };

    private string _sortColumn = "تاریخ";
    private bool _sortAscending = false;
    private bool _isInitializing = true;

    // ✅ مرحله دوم بهینه‌سازی: کش کردن حساب‌ها برای جلوگیری از کوئری تکراری در هر بار ورق زدن
    private List<CompanyBankAccount> _cachedAccounts = new();

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
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashTransactionListForm"); }
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashTransactionListForm"); }
        }
        base.Dispose(disposing);
    }

    private async Task InitializeFormAsync()
    {
        _isInitializing = true;

        // دریافت یک بار حساب‌ها و کش کردن آن‌ها
        _cachedAccounts = await _bankRepo.GetAllAsync();

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

            // فقط حساب‌های بانکی در فیلتر نمایش داده شوند
            var bankAccountsOnly = accounts.Where(a => !a.IsCashDesk).ToList();
            list.AddRange(bankAccountsOnly);

            cmbAccountFilter.DataSource = null;
            cmbAccountFilter.DisplayMember = "DisplayTitle";
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
        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.MultiSelect = true;                // حذف گروهی
        dgv.FillColumns();
        dgv.UseAiryLook(46);
        dgv.UseSelectionAccent(ModernTheme.Primary);
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TableName", DataPropertyName = "TableName", Visible = false });
        // نام ستون‌ها باید همین‌ها بماند (مرتب‌سازی سمت دیتابیس با همین نام‌ها کار می‌کند)؛ فقط عنوان نمایشی فارسی است
        AddCol("تاریخ", "تاریخ", 8, center: true, min: 90);
        AddCol("نوع_پرداخت", "روش پرداخت", 9, center: true, min: 100);
        AddCol("محل_مصرف", "نوع پرداخت", 13, center: true, min: 120);
        AddCol("حساب_بانکی", "حساب / بانک", 15);
        AddCol("ماهیت_هزینه", "ماهیت هزینه", 10);
        AddCol("جهت", "جهت", 7, center: true, min: 80);
        AddCol("طرف_حساب", "طرف حساب", 15);
        AddCol("توضیحات", "توضیحات", 20, min: 180);
        AddCol("بدهکار", "بدهکار (دریافتی)", 11, center: true, min: 110);
        AddCol("بستانکار", "بستانکار (پرداختی)", 11, center: true, min: 110);
        AddCol("وضعیت", "وضعیت", 12, center: true, min: 120);
        AddCol("پیوست", "پیوست", 5, center: true, min: 50);

        dgv.UseBadges("نوع_پرداخت", _methodTones);
        dgv.UseBadges("محل_مصرف", _scopeTones);
        dgv.UseBadges("وضعیت", _statusTones);
        dgv.UseBadges("جهت", _directionTones);

        dgv.ColumnHeaderMouseClick += Dgv_ColumnHeaderMouseClick;
        dgv.CellFormatting += Dgv_CellFormatting;
        dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && dgv.SelectedRows.Count == 1) OpenEditForm(); };
        // ModernGrid هدرها را از نام ستون می‌نویسد (نتیجه: «نوع_پرداخت» با زیرخط)؛ عنوان فارسی را دوباره می‌گذاریم
        dgv.DataBindingComplete += (s, e) =>
        {
            foreach (var kv in _headers)
                if (dgv.Columns.Contains(kv.Key)) dgv.Columns[kv.Key].HeaderText = kv.Value;
        };

        void AddCol(string name, string header, float weight, bool center = false, int min = 70)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name, DataPropertyName = name, HeaderText = header,
                FillWeight = weight, MinimumWidth = min
            };
            col.DefaultCellStyle.Alignment = center ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight;
            dgv.Columns.Add(col);
            _headers[name] = header;
        }

        // ── کارت‌های خلاصه ──
        var strip = ModernReport.CreateStatStrip(4, 84);
        cardTotal = strip.Add("تعداد کل تراکنش‌ها", ModernTheme.Primary);
        cardIn = strip.Add("دریافتی (صفحه‌ی جاری)", ModernTheme.Success);
        cardOut = strip.Add("پرداختی (صفحه‌ی جاری)", ModernTheme.Danger);
        cardNet = strip.Add("خالص (صفحه‌ی جاری)", ModernTheme.Teal);
        cardTotal.SetCount(0, "تراکنش");
        cardIn.SetAmount(0m, "ریال");
        cardOut.SetAmount(0m, "ریال");
        cardNet.SetAmount(0m, "ریال");

        // ── جستجو و فیلتر حساب ──
        txtSearch = ModernUI.Input(placeholder: "جستجو در شرح، طرف حساب...");
        _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); _currentPage = 1; await LoadDataAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        cmbAccountFilter = ModernUI.Combo();
        cmbAccountFilter.SelectedIndexChanged += async (s, e) =>
        {
            if (!_isInitializing) { _currentPage = 1; await LoadDataAsync(); }
        };

        var toolbar = ModernListKit.BuildToolbar(
            (txtSearch, 360),
            (ModernListKit.FilterLabel("حساب:"), 56),
            (cmbAccountFilter, 280));

        // ── صفحه‌بندی ──
        var pager = ModernListKit.BuildPager(out lblPageInfo, out var first, out var prev, out var next, out var last);
        btnFirstPage = first; btnPrevPage = prev; btnNextPage = next; btnLastPage = last;
        btnFirstPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage = 1; await LoadDataAsync(); } };
        btnPrevPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage--; await LoadDataAsync(); } };
        btnNextPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage++; await LoadDataAsync(); } };
        btnLastPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage = _totalPages; await LoadDataAsync(); } };

        // ── دکمه‌ها ──
        btnAdd = ModernButton.Success("ثبت تراکنش جدید", 160, 36);
        btnAdd.Click += BtnAdd_Click;
        btnEdit = ModernButton.Soft("ویرایش", ModernTheme.Primary, 100, 36);
        btnEdit.Click += BtnEdit_Click;
        btnDelete = ModernButton.Soft("حذف", ModernTheme.Danger, 100, 36);
        btnDelete.Click += BtnDelete_Click;
        btnPrint = ModernButton.Outline("دستور پرداخت", ModernTheme.Primary, 130, 36);
        btnPrint.Click += BtnPrint_Click;
        btnRefresh = ModernButton.Outline("بروزرسانی", ModernTheme.Neutral, 120, 36);
        btnRefresh.Click += async (s, e) => { _currentPage = 1; txtSearch.Text = string.Empty; await LoadDataAsync(); };

        btnExportExcel = ModernButton.Outline("خروجی اکسل", ModernTheme.Neutral, 130, 36);
        btnExportExcel.Click += BtnExportExcel_Click;
        btnImportExcel = ModernButton.Outline("ایمپورت اکسل", ModernTheme.Neutral, 130, 36);
        btnImportExcel.Click += BtnImportExcel_Click;
        btnDownloadTemplate = ModernButton.Outline("دانلود قالب", ModernTheme.Neutral, 130, 36);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

        var actions = ModernListKit.BuildActionBar(
            new Control[] { btnAdd, btnEdit, btnDelete, btnPrint, btnRefresh },
            new Control[] { btnExportExcel, btnImportExcel, btnDownloadTemplate });

        ModernListKit.Compose(this, "لیست تراکنش‌های نقدی و حواله‌ها", "دریافت‌ها، پرداخت‌ها، چک‌ها و انتقال‌های بین حساب‌ها",
            new Size(1650, 800), strip, 84, toolbar, ModernReport.WrapGrid(dgv), pager, actions);
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
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= dgv.RowCount) return;
        string colName = dgv.Columns[e.ColumnIndex].Name;
        var row = dgv.Rows[e.RowIndex];

        // هر بخش یک زمینه‌ی کم‌رنگ مخصوص خودش دارد:
        //   شارژ تنخواه = نارنجی | تراکنش روی حساب تنخواه = زرد | انتقال بین حساب‌ها = بنفش | چک = آبی
        string accountTitle = row.Cells["حساب_بانکی"].Value?.ToString() ?? "";
        string tableName = row.Cells["TableName"].Value?.ToString() ?? "";
        string method = row.Cells["نوع_پرداخت"].Value?.ToString() ?? "";
        if (tableName == "TankhahCharges")
            e.CellStyle.BackColor = ModernPaint.Tint(ModernTheme.Orange, 16);
        else if (accountTitle.Contains("تنخواه"))
            e.CellStyle.BackColor = ModernPaint.Tint(ModernTheme.Warning, 9);
        else if (tableName == "AccountTransfers" || (tableName == "CashTransactions" && method == "انتقال"))
            e.CellStyle.BackColor = ModernPaint.Tint(ModernTheme.Purple, 8);
        else if (tableName == "CashChecks")
            e.CellStyle.BackColor = ModernPaint.Tint(ModernTheme.Primary, 7);

        if (colName == "بدهکار" || colName == "بستانکار")
        {
            if (e.Value is decimal amount)
            {
                if (amount == 0m)
                {
                    e.Value = "—";
                    e.CellStyle.ForeColor = ModernTheme.Placeholder;
                }
                else
                {
                    e.Value = NumberHelper.FormatThousands(amount, usePersianDigits: true);
                    e.CellStyle.ForeColor = colName == "بدهکار" ? ModernPaint.Darken(ModernTheme.Success, 70) : ModernTheme.DangerText;
                    e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
                }
                e.FormattingApplied = true;
            }
        }
        else if (colName == "طرف_حساب")
            e.CellStyle.ForeColor = ModernTheme.TextStrong;
        else if (colName == "حساب_بانکی" || colName == "ماهیت_هزینه" || colName == "توضیحات")
            e.CellStyle.ForeColor = ModernTheme.Text;   // پررنگ‌تر از TextMuted تا روی زمینه‌های رنگی خوانا باشد
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

                // ✅ استفاده از حساب‌های کش شده به جای کوئری مجدد به دیتابیس
                var accDict = _cachedAccounts.ToDictionary(a => a.Id, a => a);

                var displayData = list.Select(t =>
                {
                    string accTitle = t.AccountTitle ?? "---";

                    if (t.AccountId.HasValue && accDict.TryGetValue(t.AccountId.Value, out var acc) && !string.IsNullOrEmpty(acc.BankName))
                    {
                        if (!accTitle.Contains(acc.BankName))
                        {
                            accTitle = $"{accTitle} - {acc.BankName}";
                        }
                    }

                    return new
                    {
                        t.Id,
                        t.TableName,
                        تاریخ = DateHelper.ToPersianDate(t.TransactionDate),
                        نوع_پرداخت = t.TransactionType,
                        محل_مصرف = t.PaymentScopeDisplay,
                        حساب_بانکی = accTitle,
                        ماهیت_هزینه = t.CostType ?? "---",
                        جهت = t.Direction,
                        طرف_حساب = t.TafsiliTitle ?? t.RecipientName ?? "---",
                        توضیحات = t.Description ?? "---",
                        بدهکار = t.Direction == "دریافتی" ? t.Amount : 0m,
                        بستانکار = t.Direction == "پرداختی" ? t.Amount : 0m,
                        وضعیت = t.BillingStatus,
                        پیوست = transIdsWithAttachments.Contains($"CashTransaction_{t.Id}") ||
                                 transIdsWithAttachments.Contains($"CashCheck_{t.Id}") ? "📎" : ""
                    };
                }).ToList();

                ModernListKit.FillTones(_methodTones, displayData.Select(d => (string?)d.نوع_پرداخت), ModernListKit.MethodTone);
                ModernListKit.FillTones(_scopeTones, displayData.Select(d => (string?)d.محل_مصرف), ModernListKit.ScopeTone);
                ModernListKit.FillTones(_statusTones, displayData.Select(d => (string?)d.وضعیت), ModernListKit.StatusTone);

                dgv.DataSource = displayData;

                decimal sumIn = list.Where(t => t.Direction == "دریافتی").Sum(t => t.Amount);
                decimal sumOut = list.Where(t => t.Direction == "پرداختی").Sum(t => t.Amount);
                cardTotal.SetCount(_totalCount, "تراکنش");
                cardIn.SetAmount(sumIn, "ریال");
                cardOut.SetAmount(sumOut, "ریال");
                cardNet.SetAmount(sumIn - sumOut, "ریال");

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
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashTransactionListForm"); }
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
        if (dgv.CurrentRow is null) { ModernMessageBox.ShowWarning("لطفاً یک تراکنش را انتخاب کنید."); return; }

        int id = Convert.ToInt32(dgv.CurrentRow.Cells["Id"].Value);
        string desc = dgv.CurrentRow.Cells["توضیحات"].Value?.ToString() ?? "";
        string tableName = dgv.CurrentRow.Cells["TableName"].Value?.ToString() ?? "CashTransactions";

        if (tableName == "TankhahCharges")
{
ModernMessageBox.ShowWarning("این رکورد یک «شارژ تنخواه» است و از این لیست قابل ویرایش یا حذف نیست.\nبرای تغییر آن از منوی «شارژ / انتقال به تنخواه» استفاده کنید.");
return;
}
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

            if (tableName == "TankhahCharges")
{
ModernMessageBox.ShowWarning("این رکورد یک «شارژ تنخواه» است و از این لیست قابل ویرایش یا حذف نیست.\nبرای تغییر آن از منوی «شارژ / انتقال به تنخواه» استفاده کنید.");
return;
}
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
                if (trans is not null) selectedTrans.Add(trans);
            }

            if (!selectedTrans.Any()) return;

            var accountIds = selectedTrans.Where(t => t.AccountId.HasValue).Select(t => t.AccountId!.Value).Distinct().ToList();

            if (accountIds.Count == 0) { ModernMessageBox.ShowWarning("هیچ‌کدام از تراکنش‌های انتخاب‌شده به یک حساب بانکی متصل نیستند."); return; }
            if (accountIds.Count > 1) { ModernMessageBox.ShowWarning("تراکنش‌های انتخاب‌شده از چند حساب مختلف هستند.\nلطفاً تراکنش‌های یک حساب را انتخاب کنید."); return; }

            int accountId = accountIds[0];
            var account = _cachedAccounts.FirstOrDefault(a => a.Id == accountId);

            if (account is null) { ModernMessageBox.ShowWarning($"حساب بانکی مرتبط با این تراکنش‌ها در سیستم وجود ندارد."); return; }
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

            string[] headers = {
                "تاریخ (شمسی 1403/01/01)",
                "حساب بانکی (عنوان)",
                "جهت (پرداختی/دریافتی)",
                "روش پرداخت (نقدی/حواله/کارمزد/سود)",
                "مبلغ (ریال)",
                "کد تفصیلی گیرنده",
                "شماره قرارداد",
                "دسته‌بندی",
                "نوع هزینه",
                "نوع پرداخت (اداری/پروژه/پیمانکار)",
                "توضیحات"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
            }

            ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
            ws.Cell(2, 2).Value = "جاری اصلی";
            ws.Cell(2, 3).Value = "پرداختی";
            ws.Cell(2, 4).Value = "کارمزد";
            ws.Cell(2, 5).Value = 50000;
            ws.Cell(2, 6).Value = "";
            ws.Cell(2, 7).Value = "";
            ws.Cell(2, 8).Value = "کارمزد بانکی";
            ws.Cell(2, 9).Value = "سایر";
            ws.Cell(2, 10).Value = "اداری";
            ws.Cell(2, 11).Value = "کارمزد انتقال وجه";

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 35;
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
        ModernForm.Setup(selectAccountForm, "انتخاب حساب مبدا", new Size(440, 250), isDialog: true);
        selectAccountForm.MinimizeBox = false;
        selectAccountForm.ShowInTaskbar = false;

        var lbl = ModernUI.Caption("این تراکنش‌ها مربوط به کدام حساب است؟", 11f, true);
        lbl.Dock = DockStyle.Fill;
        var cmb = ModernUI.Combo();
        cmb.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cmb.Margin = new Padding(0, 4, 0, 4);
        cmb.DisplayMember = "DisplayTitle";
        cmb.ValueMember = "Id";
        cmb.DataSource = accounts;
        var btnOk = ModernButton.Save("تایید و ادامه", 150, 38);
        btnOk.Anchor = AnchorStyles.None;
        btnOk.Click += (s, ev) => { selectAccountForm.DialogResult = DialogResult.OK; selectAccountForm.Close(); };

        var selLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(24, 20, 24, 16) };
        selLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        selLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        selLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        selLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        selLayout.Controls.Add(lbl, 0, 0);
        selLayout.Controls.Add(cmb, 0, 1);
        selLayout.Controls.Add(btnOk, 0, 2);
        selectAccountForm.Controls.Add(selLayout);

        if (selectAccountForm.ShowDialog(this) != DialogResult.OK || cmb.SelectedItem is null) return;
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
                if (date is null) date = DateTime.Today;

                decimal amount = NumberHelper.ParseFormatted(amountStr);
                if (amount == 0) continue;

                string direction = directionStr.Contains("دریافتی") ? "دریافتی" : "پرداختی";
                string method = string.IsNullOrWhiteSpace(methodStr) ? "حواله" : methodStr;

                if (method.Contains("کارمزد") || category.Contains("کارمزد"))
                {
                    method = "کارمزد";
                    direction = "پرداختی";
                    category = "کارمزد بانکی";
                }
                else if (method.Contains("سود") || category.Contains("سود"))
                {
                    method = "سود";
                    direction = "دریافتی";
                    category = "سود بانکی";
                }

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

                    // ✅ به‌جای خالی، صفر عددی ثبت می‌شود تا جمع و فیلتر اکسل درست کار کند
                    ws.Cell(rowIndex, 9).Value = t.Direction == "دریافتی" ? t.Amount : 0m;
                    ws.Cell(rowIndex, 10).Value = t.Direction == "پرداختی" ? t.Amount : 0m;

                    ws.Cell(rowIndex, 11).Value = t.BillingStatus;
                    ws.Cell(rowIndex, 12).Value = (transIdsWithAttachments.Contains($"CashTransaction_{t.Id}") || transIdsWithAttachments.Contains($"CashCheck_{t.Id}")) ? "📎" : "";

                    ws.Cell(rowIndex, 9).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(rowIndex, 10).Style.NumberFormat.Format = "#,##0";
                    ws.Range(rowIndex, 1, rowIndex, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(rowIndex, 1, rowIndex, 12).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    rowIndex++;
                }

                // ردیف جمع کل
                int lastDataRow = rowIndex - 1;
                ws.Cell(rowIndex, 8).Value = "جمع کل";
                ws.Cell(rowIndex, 9).FormulaA1 = $"SUM(I2:I{lastDataRow})";
                ws.Cell(rowIndex, 10).FormulaA1 = $"SUM(J2:J{lastDataRow})";
                ws.Cell(rowIndex, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIndex, 10).Style.NumberFormat.Format = "#,##0";
                var totalRange = ws.Range(rowIndex, 1, rowIndex, 12);
                totalRange.Style.Font.Bold = true;
                totalRange.Style.Fill.BackgroundColor = XLColor.FromArgb(226, 232, 240);
                totalRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                ws.Range(1, 1, lastDataRow, 12).SetAutoFilter();
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
