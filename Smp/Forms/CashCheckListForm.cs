using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
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

namespace Smp.Forms;

public class CashCheckListForm : Form
{
    private readonly CashCheckRepository _repository;
    private readonly BankAccountRepository _bankRepo;
    private readonly string _connectionString;

    private DataGridView dgvChecks = null!;
    private ModernTextBox txtSearch = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;

    private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnChangeStatus = null!, btnPrintOrder = null!, btnPrintCheque = null!;
    private Button btnDownloadTemplate = null!, btnImportExcel = null!, btnExportExcel = null!;

    private int _currentPage = 1;
    private const int _pageSize = 50;
    private int _totalPages = 1;
    private int _totalCount = 0;
    private Button btnNextPage = null!, btnPrevPage = null!, btnFirstPage = null!, btnLastPage = null!;
    private Label lblPageInfo = null!;

    private ModernStatCard cardTotal = null!, cardSum = null!, cardPending = null!;
    private readonly Dictionary<string, string> _headers = new();
    private readonly Dictionary<string, Color> _statusTones = new();
    private readonly Dictionary<string, Color> _scopeTones = new();

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public CashCheckListForm(string connectionString)
    {
        _connectionString = connectionString;
        _repository = new CashCheckRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this);

        Load += async (s, e) => await LoadChecksAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashCheckListForm"); }
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashCheckListForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── جدول ──
        dgvChecks = ModernGrid.Create();
        dgvChecks.AutoGenerateColumns = false;
        dgvChecks.MultiSelect = true;          // حذف و تغییر وضعیت گروهی
        dgvChecks.FillColumns();
        dgvChecks.UseAiryLook(46);
        dgvChecks.UseSelectionAccent(ModernTheme.Primary);

        dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "DueDateRaw", DataPropertyName = "DueDateRaw", Visible = false });   // تاریخ میلادی سررسید برای رنگ‌بندی چک‌های گذشته
        AddCol("CheckNumber", "شماره چک", 12, center: true);
        AddCol("BankName", "بانک", 12);
        AddCol("DueDate", "سررسید", 10, center: true);
        AddCol("Amount", "مبلغ (ریال)", 14, center: true);
        AddCol("CheckType", "نوع چک", 8, center: true);
        AddCol("Status", "وضعیت", 10, center: true);
        AddCol("RecipientName", "گیرنده", 16);
        AddCol("PaymentScope", "نوع پرداخت", 15, center: true, min: 110);
        AddCol("CostType", "ماهیت هزینه", 10);
        AddCol("Attachment", "پیوست", 6, center: true, min: 50);

        dgvChecks.EnableThousandsFormattingFor("Amount");
        dgvChecks.UseBadges("Status", _statusTones);
        dgvChecks.UseBadges("PaymentScope", _scopeTones);

        dgvChecks.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenEditForm(); };
        dgvChecks.CellFormatting += DgvChecks_CellFormatting;
        // ModernGrid هدرها را از نام ستون می‌نویسد؛ عنوان‌های فارسی خودمان را دوباره می‌گذاریم
        dgvChecks.DataBindingComplete += (s, e) =>
        {
            foreach (var kv in _headers)
                if (dgvChecks.Columns.Contains(kv.Key)) dgvChecks.Columns[kv.Key].HeaderText = kv.Value;
        };

        void AddCol(string name, string header, float weight, bool center = false, int min = 70)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name, DataPropertyName = name, HeaderText = header,
                FillWeight = weight, MinimumWidth = min
            };
            col.DefaultCellStyle.Alignment = center ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight;
            dgvChecks.Columns.Add(col);
            _headers[name] = header;
        }

        // ── کارت‌های خلاصه ──
        var strip = ModernReport.CreateStatStrip(3, 84);
        cardTotal = strip.Add("تعداد کل چک‌ها", ModernTheme.Primary);
        cardSum = strip.Add("جمع مبلغ (صفحه‌ی جاری)", ModernTheme.Teal);
        cardPending = strip.Add("در جریان (صفحه‌ی جاری)", ModernTheme.Warning);
        cardTotal.SetCount(0, "چک");
        cardSum.SetAmount(0m, "ریال");
        cardPending.SetAmount(0m, "ریال");

        // ── جستجو ──
        txtSearch = ModernUI.Input(placeholder: "جستجو در شماره چک، گیرنده، بانک...");
        _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); _currentPage = 1; await LoadChecksAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
        var toolbar = ModernListKit.BuildToolbar((txtSearch, 380));

        // ── صفحه‌بندی ──
        var pager = ModernListKit.BuildPager(out lblPageInfo, out var first, out var prev, out var next, out var last);
        btnFirstPage = first; btnPrevPage = prev; btnNextPage = next; btnLastPage = last;
        btnFirstPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage = 1; await LoadChecksAsync(); } };
        btnPrevPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage--; await LoadChecksAsync(); } };
        btnNextPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage++; await LoadChecksAsync(); } };
        btnLastPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage = _totalPages; await LoadChecksAsync(); } };

        // ── دکمه‌ها ──
        btnAdd = ModernButton.Success("ثبت چک جدید", 140, 36);
        btnAdd.Click += BtnAdd_Click;
        btnEdit = ModernButton.Soft("ویرایش", ModernTheme.Primary, 100, 36);
        btnEdit.Click += BtnEdit_Click;
        btnDelete = ModernButton.Soft("حذف", ModernTheme.Danger, 100, 36);
        btnDelete.Click += BtnDelete_Click;
        btnChangeStatus = ModernButton.Soft("تغییر وضعیت", ModernTheme.Warning, 130, 36);
        btnChangeStatus.Click += BtnChangeStatus_Click;
        btnPrintOrder = ModernButton.Outline("دستور پرداخت", ModernTheme.Primary, 130, 36);
        btnPrintOrder.Click += BtnPrintOrder_Click;
        btnPrintCheque = ModernButton.Outline("چاپ روی چک", ModernTheme.Purple, 130, 36);
        btnPrintCheque.Click += BtnPrintCheque_Click;

        btnExportExcel = ModernButton.Outline("خروجی اکسل", ModernTheme.Neutral, 130, 36);
        btnExportExcel.Click += BtnExportExcel_Click;
        btnImportExcel = ModernButton.Outline("ایمپورت اکسل", ModernTheme.Neutral, 130, 36);
        btnImportExcel.Click += BtnImportExcel_Click;
        btnDownloadTemplate = ModernButton.Outline("دانلود قالب", ModernTheme.Neutral, 130, 36);
        btnDownloadTemplate.Click += (s, e) => ExcelTemplateGenerator.GenerateCashChecksTemplate();

        var actions = ModernListKit.BuildActionBar(
            new Control[] { btnAdd, btnEdit, btnDelete, btnChangeStatus, btnPrintOrder, btnPrintCheque },
            new Control[] { btnExportExcel, btnImportExcel, btnDownloadTemplate });

        ModernListKit.Compose(this, "لیست چک‌ها", "مدیریت چک‌های پرداختی و دریافتی", new Size(1500, 800),
            strip, 84, toolbar, ModernReport.WrapGrid(dgvChecks), pager, actions);
    }

    private async Task LoadChecksAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            string search = txtSearch.Text.Trim();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var (list, totalCount) = await _repository.GetPagedAsync(_currentPage, _pageSize, search);
                _totalCount = totalCount;
                _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
                if (_totalPages == 0) _totalPages = 1;

                if (IsDisposed) return;

                var checkIdsWithAttachments = new HashSet<int>();
                var currentPageIds = list.Select(c => c.Id).ToList();

                if (currentPageIds.Any())
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        string idList = string.Join(",", currentPageIds);
                        using var cmd = new SqlCommand(
                            $"SELECT DISTINCT EntityId FROM Attachments WHERE EntityType = 'CashCheck' AND EntityId IN ({idList})",
                            conn);
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync()) checkIdsWithAttachments.Add(reader.GetInt32(0));
                    }
                }

                var displayData = list.Select(c => new
                {
                    c.Id,
                    CheckNumber = c.CheckNumber,
                    BankName = c.BankName,
                    DueDate = DateHelper.ToPersianDate(c.DueDate),
                    DueDateRaw = c.DueDate,
                    Amount = c.Amount,
                    CheckType = c.CheckType,
                    Status = c.Status,
                    RecipientName = c.RecipientName,
                    // ✅ استفاده از هلپر برای ترجمه نوع پرداخت
                    PaymentScope = TranslationHelper.GetPaymentScopeName(c.PaymentScope),
                    CostType = c.CostType ?? "---",
                    Attachment = checkIdsWithAttachments.Contains(c.Id) ? "📎" : ""
                }).ToList();

                ModernListKit.FillTones(_statusTones, displayData.Select(d => (string?)d.Status), ModernListKit.StatusTone);
                ModernListKit.FillTones(_scopeTones, displayData.Select(d => (string?)d.PaymentScope), ModernListKit.ScopeTone);

                dgvChecks.DataSource = null;
                dgvChecks.DataSource = displayData;

                var pending = list.Where(c => c.Status == "در جریان").ToList();
                cardTotal.SetCount(_totalCount, "چک");
                cardSum.SetAmount(list.Sum(c => c.Amount), "ریال");
                cardPending.SetAmount(pending.Sum(c => c.Amount), "ریال");
                cardPending.SubText = StringHelper.ConvertToPersianDigits(pending.Count.ToString()) + " چک";

                lblPageInfo.Text = $"صفحه {NumberHelper.FormatThousands(_currentPage, usePersianDigits: true)} از {NumberHelper.FormatThousands(_totalPages, usePersianDigits: true)}  (کل: {NumberHelper.FormatThousands(_totalCount, usePersianDigits: true)} رکورد)";
                btnPrevPage.Enabled = _currentPage > 1;
                btnNextPage.Enabled = _currentPage < _totalPages;
                btnFirstPage.Enabled = _currentPage > 1;
                btnLastPage.Enabled = _currentPage < _totalPages;

            }, "در حال دریافت لیست چک‌ها...");

            if (!success && !IsDisposed) ModernMessageBox.ShowError("خطا در دریافت لیست چک‌ها.");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "CashCheckListForm.LoadChecksAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashCheckListForm"); }
        }
    }

    private void DgvChecks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= dgvChecks.RowCount) return;
        string colName = dgvChecks.Columns[e.ColumnIndex].Name;

        // چک برگشتی یا باطل‌شده: کل ردیف زمینه‌ی کم‌رنگ؛ بقیه‌ی ردیف‌ها بدون رنگ‌بندی (وضعیت با بج نشان داده می‌شود)
        string status = dgvChecks.Rows[e.RowIndex].Cells["Status"].Value?.ToString() ?? "";
        if (status == "برگشتی" || status == "باطل شده")
            e.CellStyle.BackColor = ModernPaint.Tint(ModernTheme.Danger, 6);
        else if (status == "در جریان"
                 && dgvChecks.Rows[e.RowIndex].Cells["DueDateRaw"].Value is DateTime dueDate
                 && dueDate.Date < DateTime.Today)
        {
            // سررسید گذشته و هنوز پاس نشده: کل ردیف زمینه‌ی کهربایی، تاریخ سررسید قرمز و پررنگ
            e.CellStyle.BackColor = ModernPaint.Tint(ModernTheme.Warning, 16);
            if (colName == "DueDate")
            {
                e.CellStyle.ForeColor = ModernTheme.DangerText;
                e.CellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
            }
        }

        if (colName == "Amount") e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
        else if (colName == "RecipientName") e.CellStyle.ForeColor = ModernTheme.TextStrong;
        else if (colName == "BankName" || colName == "CostType" || colName == "CheckType")
            e.CellStyle.ForeColor = ModernTheme.TextMuted;
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using (var form = new CashCheckEditForm(_connectionString, null))
        {
            if (form.ShowDialog() == DialogResult.OK) _ = LoadChecksAsync();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e) => OpenEditForm();

    private void OpenEditForm()
    {
        if (dgvChecks.SelectedRows.Count == 0) { ModernMessageBox.ShowWarning("لطفاً یک چک را از لیست انتخاب کنید."); return; }
        if (dgvChecks.SelectedRows.Count > 1) { ModernMessageBox.ShowWarning("برای ویرایش، فقط یک چک را انتخاب کنید."); return; }

        int checkId = Convert.ToInt32(dgvChecks.SelectedRows[0].Cells["Id"].Value);

        using (var form = new CashCheckEditForm(_connectionString, checkId))
        {
            if (form.ShowDialog() == DialogResult.OK) _ = LoadChecksAsync();
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;

        if (dgvChecks.SelectedRows.Count == 0) { ModernMessageBox.ShowWarning("لطفاً حداقل یک چک را از لیست انتخاب کنید."); return; }

        var selectedIds = dgvChecks.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => Convert.ToInt32(r.Cells["Id"].Value)).ToList();

        if (!ModernMessageBox.AskConfirm($"آیا از حذف {selectedIds.Count} چک انتخاب شده مطمئن هستید؟\n\n⚠️ این عملیات غیرقابل بازگشت است.", "حذف چک‌ها"))
            return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _repository.DeleteBatchAsync(selectedIds);
        }, "در حال حذف چک‌ها...");

        if (success && !IsDisposed)
        {
            ToastHelper.Show($"{selectedIds.Count} چک با موفقیت حذف شد.", isSuccess: true);
            await LoadChecksAsync();
        }
    }

    private async void BtnChangeStatus_Click(object? sender, EventArgs e)
    {
        if (dgvChecks.SelectedRows.Count == 0 || IsDisposed) { ModernMessageBox.ShowWarning("لطفاً حداقل یک چک را از لیست انتخاب کنید."); return; }

        var selectedIds = dgvChecks.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => Convert.ToInt32(r.Cells["Id"].Value)).ToList();

        using (var form = new Form())
        {
            ModernForm.Setup(form, "تغییر وضعیت گروهی چک‌ها", new Size(400, 250), isDialog: true);
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;

            var lbl = ModernUI.Caption($"وضعیت جدید برای {selectedIds.Count} چک انتخاب‌شده:", 11f, true);
            lbl.Dock = DockStyle.Fill;
            var cmb = ModernUI.Combo();
            cmb.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmb.Margin = new Padding(0, 4, 0, 4);
            cmb.Items.AddRange(TranslationHelper.CheckStatusesList);
            cmb.SelectedIndex = 0;

            var btnOk = ModernButton.Save("اعمال تغییرات", 150, 38);
            btnOk.Anchor = AnchorStyles.None;

            btnOk.Click += async (s, ev) =>
            {
                string newStatus = cmb.SelectedItem!.ToString()!;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await _repository.UpdateStatusBatchAsync(selectedIds, newStatus);
                }, "در حال بروزرسانی وضعیت چک‌ها...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show($"وضعیت {selectedIds.Count} چک تغییر کرد.", true);
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                }
            };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(24, 20, 24, 16) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(lbl, 0, 0);
            layout.Controls.Add(cmb, 0, 1);
            layout.Controls.Add(btnOk, 0, 2);
            form.Controls.Add(layout);

            if (form.ShowDialog() == DialogResult.OK && !IsDisposed)
            {
                _ = LoadChecksAsync();
            }
        }
    }

    private async void BtnPrintOrder_Click(object? sender, EventArgs e)
    {
        if (dgvChecks.SelectedRows.Count == 0 || IsDisposed) return;
        if (dgvChecks.SelectedRows.Count > 1) { ModernMessageBox.ShowWarning("برای چاپ دستور پرداخت، فقط یک چک را انتخاب کنید."); return; }

        int checkId = Convert.ToInt32(dgvChecks.SelectedRows[0].Cells["Id"].Value);
        var check = await _repository.GetByIdAsync(checkId);
        if (check is null || IsDisposed) return;

        if (!check.AccountId.HasValue) { ModernMessageBox.ShowWarning("این چک به یک حساب بانکی متصل نیست."); return; }

        var accounts = await _bankRepo.GetAllAsync();
        var account = accounts.FirstOrDefault(a => a.Id == check.AccountId.Value);

        if (account is null) { ModernMessageBox.ShowWarning("حساب بانکی مرتبط با این چک در سیستم وجود ندارد."); return; }
        if (account.IsCashDesk) { ModernMessageBox.ShowWarning("دستور پرداخت بانکی برای صندوق نقدی صادر نمی‌شود."); return; }
        if (!account.IsActive) { ModernMessageBox.ShowWarning($"حساب بانکی «{account.Title}» غیرفعال شده است."); return; }

        var checks = new List<CashCheck> { check };
        using (var paymentOrderForm = new PaymentOrderForm(checks, account))
        {
            paymentOrderForm.ShowDialog();
        }
    }

    private async void BtnPrintCheque_Click(object? sender, EventArgs e)
    {
        if (dgvChecks.SelectedRows.Count == 0 || IsDisposed) return;
        if (dgvChecks.SelectedRows.Count > 1) { ModernMessageBox.ShowWarning("برای چاپ روی چک، فقط یک چک را انتخاب کنید."); return; }

        int checkId = Convert.ToInt32(dgvChecks.SelectedRows[0].Cells["Id"].Value);
        var check = await _repository.GetByIdAsync(checkId);
        if (check is null || IsDisposed) return;

        if (!check.AccountId.HasValue) { ModernMessageBox.ShowWarning("این چک به یک حساب بانکی متصل نیست."); return; }

        var accounts = await _bankRepo.GetAllAsync();
        var account = accounts.FirstOrDefault(a => a.Id == check.AccountId.Value);

        if (account is null) { ModernMessageBox.ShowWarning("حساب بانکی مرتبط با این چک در سیستم وجود ندارد."); return; }
        if (account.IsCashDesk) { ModernMessageBox.ShowWarning("چاپ روی چک فقط برای حساب‌های بانکی ممکن است."); return; }

        if (!account.IsActive)
        {
            var confirm = ModernMessageBox.AskConfirm($"حساب بانکی «{account.Title}» غیرفعال شده است.\n\nچاپ روی چک همچنان ادامه یابد؟", "حساب غیرفعال");
            if (!confirm) return;
        }

        using (var chequePrintForm = new ChequePrintForm(check, account))
        {
            chequePrintForm.ShowDialog();
        }
    }

    private async void BtnImportExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل چک‌ها" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        DataTable? dt = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            dt = await ExcelImportHelper.ParseCashChecksExcelAsync(ofd.FileName);
        }, "در حال خواندن فایل اکسل...");

        if (!success || dt is null || dt.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد.");
            return;
        }

        var previewForm = new Form();
        ModernForm.Setup(previewForm, "پیش‌نمایش چک‌های ایمپورت‌شده", new Size(1400, 700), isDialog: false);
        previewForm.ShowInTaskbar = false;

        var dgvPreview = new DataGridView { Dock = DockStyle.Fill, DataSource = dt };
        dgvPreview.ApplyModernStyle();

        dgvPreview.DataBindingComplete += (s2, e2) => {
            foreach (DataGridViewRow row in dgvPreview.Rows)
            {
                var statusVal = row.Cells["وضعیت"].Value?.ToString();
                if (statusVal != "معتبر")
                {
                    row.DefaultCellStyle.BackColor = ModernTheme.DangerSoft;
                    row.DefaultCellStyle.ForeColor = ModernTheme.DangerText;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = ModernTheme.SuccessSoft;
                }
            }
        };

        if (dgvPreview.Columns["AccountId"] is not null) dgvPreview.Columns["AccountId"].Visible = false;
        if (dgvPreview.Columns["ContractId"] is not null) dgvPreview.Columns["ContractId"].Visible = false;
        if (dgvPreview.Columns["ContractTitle"] is not null) dgvPreview.Columns["ContractTitle"].Visible = false;
        if (dgvPreview.Columns["ContractorContractId"] is not null) dgvPreview.Columns["ContractorContractId"].Visible = false;
        if (dgvPreview.Columns["PaymentScope"] is not null) dgvPreview.Columns["PaymentScope"].Visible = false;

        var btnSave = ModernButton.Save("انتقال چک‌های معتبر به دیتابیس", 300, 40);
        btnSave.Dock = DockStyle.Fill;
        var saveHost = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(14, 10, 14, 10), BackColor = ModernTheme.Background };
        saveHost.Controls.Add(btnSave);

        btnSave.Click += async (s, ev) =>
        {
            var validRows = dt.Rows.Cast<DataRow>().Where(r => r["وضعیت"].ToString() == "معتبر").CopyToDataTable();

            if (validRows.Rows.Count == 0) { ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد."); return; }
            if (!ModernMessageBox.AskConfirm($"آیا از ثبت {NumberHelper.FormatThousands(validRows.Rows.Count, usePersianDigits: true)} چک معتبر مطمئن هستید؟", "تایید ایمپورت")) return;

            string msg = "";
            bool saveSuccess = await AsyncHelper.RunWithLoading(previewForm, async () =>
            {
                msg = await ExcelImportHelper.SaveCashChecksToDbAsync(validRows);
            }, "در حال ثبت چک‌ها...");

            if (saveSuccess && !IsDisposed)
            {
                ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
                previewForm.Close();
                _currentPage = 1;
                await LoadChecksAsync();
            }
        };

        previewForm.Controls.Add(dgvPreview);
        previewForm.Controls.Add(saveHost);
        previewForm.ShowDialog(this);
    }

    private async void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (dgvChecks.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد."); return; }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"لیست_چک‌ها_{DateTime.Now:yyyyMMdd}.xlsx",
            Title = "ذخیره خروجی اکسل",
            OverwritePrompt = true
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("لیست چک‌ها");
                ws.RightToLeft = true;

                string[] headers = { "شماره چک", "بانک", "تاریخ سررسید", "مبلغ (ریال)", "نوع چک", "وضعیت", "نام گیرنده", "نوع پرداخت", "ماهیت هزینه", "پیوست" };

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

                for (int r = 0; r < dgvChecks.Rows.Count; r++)
                {
                    var row = dgvChecks.Rows[r];
                    int er = r + 2;

                    ws.Cell(er, 1).Value = row.Cells["CheckNumber"].Value?.ToString() ?? "";
                    ws.Cell(er, 2).Value = row.Cells["BankName"].Value?.ToString() ?? "";
                    ws.Cell(er, 3).Value = row.Cells["DueDate"].Value?.ToString() ?? "";
                    ws.Cell(er, 4).Value = Convert.ToDecimal(row.Cells["Amount"].Value ?? 0m);
                    ws.Cell(er, 5).Value = row.Cells["CheckType"].Value?.ToString() ?? "";
                    ws.Cell(er, 6).Value = row.Cells["Status"].Value?.ToString() ?? "";
                    ws.Cell(er, 7).Value = row.Cells["RecipientName"].Value?.ToString() ?? "";
                    ws.Cell(er, 8).Value = row.Cells["PaymentScope"].Value?.ToString() ?? "";
                    ws.Cell(er, 9).Value = row.Cells["CostType"].Value?.ToString() ?? "";
                    ws.Cell(er, 10).Value = row.Cells["Attachment"].Value?.ToString() ?? "";

                    ws.Cell(er, 4).Style.NumberFormat.Format = "#,##0";
                    ws.Range(er, 1, er, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(er, 1, er, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);
                workbook.SaveAs(sfd.FileName);
            });
        }, "در حال تولید فایل اکسل...");

        if (success && !IsDisposed)
        {
            ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
        }
    }
}
