using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
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

namespace SMP.Forms
{
    public class CashCheckListForm : Form
    {
        private readonly CashCheckRepository _repository;
        private readonly BankAccountRepository _bankRepo;
        private readonly string _connectionString;

        private DataGridView dgvChecks = null!;
        private TextBox txtSearch = null!;
        private System.Windows.Forms.Timer _searchTimer = null!;

        private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnChangeStatus = null!, btnPrintOrder = null!, btnPrintCheque = null!;
        private Button btnDownloadTemplate = null!, btnImportExcel = null!, btnExportExcel = null!;

        private int _currentPage = 1;
        private const int _pageSize = 50;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private Button btnNextPage = null!, btnPrevPage = null!;
        private Label lblPageInfo = null!;

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
                try { _loadLock?.Dispose(); } catch { }
                try { _searchTimer?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            // ✅ ۱. استفاده از الگوی استاندارد لیست (پنل راست، پنل چپ، گرید، پنل پایین)
            var (topRightPanel, topLeftPanel, grid, bottomPanel) = StandardFormHelper.SetupStandardListLayout(
                this, "لیست چک‌ها", new Size(1500, 800));

            dgvChecks = grid;

            // ✅ ۲. تعریف ستون‌ها (به صورت صریح و بدون هدرهای انگلیسی)
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "CheckNumber", DataPropertyName = "CheckNumber", HeaderText = "شماره چک", FillWeight = 12 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "BankName", DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 12 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "DueDate", DataPropertyName = "DueDate", HeaderText = "تاریخ سررسید", FillWeight = 10 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 12 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "CheckType", DataPropertyName = "CheckType", HeaderText = "نوع چک", FillWeight = 8 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "وضعیت", FillWeight = 10 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "RecipientName", DataPropertyName = "RecipientName", HeaderText = "نام گیرنده", FillWeight = 15 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaymentScope", DataPropertyName = "PaymentScope", HeaderText = "نوع پرداخت", FillWeight = 12 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostType", DataPropertyName = "CostType", HeaderText = "ماهیت هزینه", FillWeight = 10 });
            dgvChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Attachment", DataPropertyName = "Attachment", HeaderText = "پیوست", FillWeight = 5 });

            dgvChecks.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenEditForm(); };
            dgvChecks.RowPrePaint += DgvChecks_RowPrePaint;
            dgvChecks.CellFormatting += DgvChecks_CellFormatting;

            // ✅ ۳. تجهیز پنل چپ (جستجو)
            var lblSearch = new Label { Text = "🔍 جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(0, 10, 5, 0) };
            txtSearch = new TextBox { Width = 250, Font = UIHelper.PersianFont(11f), PlaceholderText = "شماره چک، گیرنده، بانک...", Margin = new Padding(0, 6, 0, 0) };

            _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); _currentPage = 1; await LoadChecksAsync(); };
            txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            topLeftPanel.Controls.Add(lblSearch);
            topLeftPanel.Controls.Add(txtSearch);

            // ✅ ۴. تجهیز پنل راست (صفحه‌بندی)
            lblPageInfo = new Label { Text = "صفحه 1 از 1", AutoSize = true, Font = UIHelper.PersianFont(10f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(10, 12, 0, 0) };

            btnPrevPage = UIHelper.CreateButton("◀ قبلی", UIHelper.SectionBlue, 80, 32);
            btnPrevPage.Margin = new Padding(5, 5, 5, 5);
            btnPrevPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage--; await LoadChecksAsync(); } };

            btnNextPage = UIHelper.CreateButton("بعدی ▶", UIHelper.SectionBlue, 80, 32);
            btnNextPage.Margin = new Padding(5, 5, 5, 5);
            btnNextPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage++; await LoadChecksAsync(); } };

            topRightPanel.Controls.Add(lblPageInfo);
            topRightPanel.Controls.Add(btnPrevPage);
            topRightPanel.Controls.Add(btnNextPage);

            // ✅ ۵. دکمه‌های پایین
            btnAdd = UIHelper.CreateButton("➕ ثبت چک جدید", UIHelper.ActionGreen, 130, 42);
            btnAdd.Click += BtnAdd_Click;

            btnEdit = UIHelper.CreateButton("✏️ ویرایش", UIHelper.ActionBlue, 110, 42);
            btnEdit.Click += BtnEdit_Click;

            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.ActionRed, 100, 42);
            btnDelete.Click += BtnDelete_Click;

            btnChangeStatus = UIHelper.CreateButton("🔄 تغییر وضعیت", UIHelper.ActionAmber, 130, 42);
            btnChangeStatus.Click += BtnChangeStatus_Click;

            btnPrintOrder = UIHelper.CreateButton("🖨️ دستور پرداخت", UIHelper.ActionBlue, 130, 42);
            btnPrintOrder.Click += BtnPrintOrder_Click;

            btnPrintCheque = UIHelper.CreateButton("📝 چاپ روی چک", UIHelper.SectionPurple, 120, 42);
            btnPrintCheque.Click += BtnPrintCheque_Click;

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب", UIHelper.WarmSecondary, 130, 42);
            btnDownloadTemplate.Click += (s, e) => ExcelTemplateGenerator.GenerateCashChecksTemplate();

            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت اکسل", UIHelper.WarmSecondary, 130, 42);
            btnImportExcel.Click += BtnImportExcel_Click;

            btnExportExcel = UIHelper.CreateButton("📊 خروجی اکسل", UIHelper.SectionTeal, 120, 42);
            btnExportExcel.Click += BtnExportExcel_Click;

            bottomPanel.Controls.Add(btnAdd);
            bottomPanel.Controls.Add(btnEdit);
            bottomPanel.Controls.Add(btnDelete);
            bottomPanel.Controls.Add(btnChangeStatus);
            bottomPanel.Controls.Add(btnPrintOrder);
            bottomPanel.Controls.Add(btnPrintCheque);
            bottomPanel.Controls.Add(btnDownloadTemplate);
            bottomPanel.Controls.Add(btnImportExcel);
            bottomPanel.Controls.Add(btnExportExcel);
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
                        Amount = NumberHelper.FormatThousands(c.Amount, usePersianDigits: true),
                        CheckType = c.CheckType,
                        Status = c.Status,
                        RecipientName = c.RecipientName,
                        // ✅ استفاده از هلپر برای ترجمه نوع پرداخت
                        PaymentScope = TranslationHelper.GetPaymentScopeName(c.PaymentScope),
                        CostType = c.CostType ?? "---",
                        Attachment = checkIdsWithAttachments.Contains(c.Id) ? "📎" : ""
                    }).ToList();

                    dgvChecks.DataSource = null;
                    dgvChecks.DataSource = displayData;

                    lblPageInfo.Text = $"صفحه {NumberHelper.FormatThousands(_currentPage, usePersianDigits: true)} از {NumberHelper.FormatThousands(_totalPages, usePersianDigits: true)}  (کل: {NumberHelper.FormatThousands(_totalCount, usePersianDigits: true)} رکورد)";
                    btnPrevPage.Enabled = _currentPage > 1;
                    btnNextPage.Enabled = _currentPage < _totalPages;

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
                try { _loadLock.Release(); } catch { }
            }
        }

        private void DgvChecks_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var scope = dgvChecks.Rows[e.RowIndex].Cells["PaymentScope"].Value?.ToString() ?? "";
            var status = dgvChecks.Rows[e.RowIndex].Cells["Status"].Value?.ToString() ?? "";

            // بررسی وضعیت‌های خاص (چک‌های برگشتی یا باطل شده)
            if (status == "برگشتی" || status == "باطل شده")
            {
                dgvChecks.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226); // قرمز ملایم
                return;
            }

            // بررسی نوع پرداخت (بر اساس مقادیر ترجمه‌شده)
            if (scope.Contains("اداری"))
            {
                dgvChecks.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244); // سبز ملایم
            }
            else if (scope.Contains("پروژه") || scope.Contains("پیمانکار"))
            {
                dgvChecks.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255); // آبی ملایم
            }
            else
            {
                dgvChecks.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White; // پیش‌فرض
            }
        }

        private void DgvChecks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvChecks.Columns[e.ColumnIndex].Name;

            if (colName == "PaymentScope")
            {
                string val = e.Value?.ToString() ?? "";
                if (val.Contains("اداری")) { e.CellStyle.ForeColor = UIHelper.ActionGreen; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("پروژه") || val.Contains("پیمانکار")) { e.CellStyle.ForeColor = UIHelper.SectionBlue; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else { e.CellStyle.ForeColor = UIHelper.WarmText; }
            }
            else if (colName == "Status")
            {
                string val = e.Value?.ToString() ?? "";
                if (val.Contains("در جریان") || val.Contains("برگشتی")) { e.CellStyle.ForeColor = Color.FromArgb(153, 27, 27); e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("پاس شده")) { e.CellStyle.ForeColor = UIHelper.ActionGreen; }
            }
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
                form.Text = "تغییر وضعیت گروهی چک‌ها";
                form.Size = new Size(320, 170);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.RightToLeft = RightToLeft.Yes;
                form.RightToLeftLayout = true;
                form.Font = UIHelper.PersianFont(11f);
                form.BackColor = Color.White;

                var lbl = new Label { Text = $"وضعیت جدید برای {selectedIds.Count} چک:", Location = new Point(20, 20), AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
                var cmb = new ComboBox { Location = new Point(20, 50), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f) };
                // ✅ استفاده از لیست وضعیت‌های آماده در هلپر
                cmb.Items.AddRange(TranslationHelper.CheckStatusesList);
                cmb.SelectedIndex = 0;

                var btnOk = new Button { Text = "اعمال تغییرات", Location = new Point(80, 90), Width = 140, Height = 35, BackColor = UIHelper.ActionGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };

                btnOk.Click += async (s, ev) =>
                {
                    string newStatus = cmb.SelectedItem.ToString()!;
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

                form.Controls.Add(lbl); form.Controls.Add(cmb); form.Controls.Add(btnOk);

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
            if (check == null || IsDisposed) return;

            if (!check.AccountId.HasValue) { ModernMessageBox.ShowWarning("این چک به یک حساب بانکی متصل نیست."); return; }

            var accounts = await _bankRepo.GetAllAsync();
            var account = accounts.FirstOrDefault(a => a.Id == check.AccountId.Value);

            if (account == null) { ModernMessageBox.ShowWarning("حساب بانکی مرتبط با این چک در سیستم وجود ندارد."); return; }
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
            if (check == null || IsDisposed) return;

            if (!check.AccountId.HasValue) { ModernMessageBox.ShowWarning("این چک به یک حساب بانکی متصل نیست."); return; }

            var accounts = await _bankRepo.GetAllAsync();
            var account = accounts.FirstOrDefault(a => a.Id == check.AccountId.Value);

            if (account == null) { ModernMessageBox.ShowWarning("حساب بانکی مرتبط با این چک در سیستم وجود ندارد."); return; }
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

            if (!success || dt == null || dt.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد.");
                return;
            }

            var previewForm = new Form();
            UIHelper.SetupForm(previewForm, "پیش‌نمایش چک‌های ایمپورت‌شده", new Size(1400, 700));
            previewForm.ShowInTaskbar = false;

            var dgvPreview = new DataGridView { Dock = DockStyle.Fill, DataSource = dt };
            dgvPreview.ApplyModernStyle();

            dgvPreview.DataBindingComplete += (s, e) => {
                foreach (DataGridViewRow row in dgvPreview.Rows)
                {
                    var statusVal = row.Cells["وضعیت"].Value?.ToString();
                    if (statusVal != "معتبر")
                    {
                        row.DefaultCellStyle.BackColor = UIHelper.WarmErrorBackground;
                        row.DefaultCellStyle.ForeColor = UIHelper.WarmErrorText;
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = UIHelper.WarmSuccessBackground;
                    }
                }
            };

            if (dgvPreview.Columns["AccountId"] != null) dgvPreview.Columns["AccountId"].Visible = false;
            if (dgvPreview.Columns["ContractId"] != null) dgvPreview.Columns["ContractId"].Visible = false;
            if (dgvPreview.Columns["ContractTitle"] != null) dgvPreview.Columns["ContractTitle"].Visible = false;

            var btnSave = UIHelper.CreateButton("انتقال چک‌های معتبر به دیتابیس", UIHelper.WarmPrimary, 300, 50);
            btnSave.Dock = DockStyle.Bottom;

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
            previewForm.Controls.Add(btnSave);
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
                        ws.Cell(er, 4).Value = NumberHelper.ParseFormatted(row.Cells["Amount"].Value?.ToString());
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
}