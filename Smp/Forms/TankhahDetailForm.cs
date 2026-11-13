using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;

namespace SMP.Forms
{
    public class TankhahDetailForm : Form
    {
        private readonly string _connectionString;
        private readonly BankAccountRepository _bankRepo;
        private readonly CashTransactionRepository _transRepo;
        private readonly CashCheckRepository _checkRepo;
        private readonly AccountTransferRepository _transferRepo;
        private CompanyBankAccount _activeTankhah;
        private List<CashTransaction> _activeTransactions = new();
        private List<CashCheck> _activeChecks = new();
        private List<AccountTransfer> _activeTransfers = new();

        private DataGridView dgvTransactions = null!;
        private Label lblTotalExpenses = null!;
        private TextBox txtSearch = null!;
        private Button btnAddRow = null!, btnEditRow = null!, btnDeleteRow = null!, btnChargeTankhah = null!;
        private Button btnPrintTankhah = null!, btnDownloadTemplate = null!, btnImportExcel = null!, btnClose = null!;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        private class TankhahDisplayItem
        {
            public int Id { get; set; }
            public string SourceType { get; set; } = ""; // "Tx", "Chk", "Trf"
            public DateTime Date { get; set; }
            public string Description { get; set; } = "";
            public string Scope { get; set; } = "";
            public string Contract { get; set; } = "";
            public string CostType { get; set; } = "";
            public string Tafsili { get; set; } = "";
            public decimal Amount { get; set; }
            public bool IsIncome { get; set; }
            public string Status { get; set; } = "";
        }

        public TankhahDetailForm(string connectionString, CompanyBankAccount tankhah)
        {
            _connectionString = connectionString;
            _bankRepo = new BankAccountRepository(connectionString);
            _transRepo = new CashTransactionRepository(connectionString);
            _checkRepo = new CashCheckRepository(connectionString);
            _transferRepo = new AccountTransferRepository(connectionString);
            _activeTankhah = tankhah;

            BuildUI();
            this.ApplyStandardHeader($"جزئیات {_activeTankhah.Title}", new Size(1200, 700));
            ShowInTaskbar = false;

            Shown += async (s, e) => await LoadDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            var pnlToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5, 5, 5, 5), WrapContents = true, BackColor = UIHelper.WarmPanelBackground };

            btnAddRow = UIHelper.CreateButton("➕ ثبت ردیف هزینه", UIHelper.ActionGreen, 140, 35);
            btnAddRow.Click += BtnAddRow_Click;

            btnEditRow = UIHelper.CreateButton("✏️ ویرایش ردیف", UIHelper.SectionBlue, 110, 35);
            btnEditRow.Enabled = false;
            btnEditRow.Click += BtnEditRow_Click;

            btnDeleteRow = UIHelper.CreateButton("🗑️ حذف ردیف", UIHelper.ActionRed, 100, 35);
            btnDeleteRow.Enabled = false;
            btnDeleteRow.Click += BtnDeleteRow_Click;

            btnChargeTankhah = UIHelper.CreateButton("💵 شارژ لیست", UIHelper.ActionAmber, 130, 35);
            btnChargeTankhah.Click += BtnChargeTankhah_Click;

            btnPrintTankhah = UIHelper.CreateButton("🖨️ چاپ صورت ریز", UIHelper.SectionTeal, 140, 35);
            btnPrintTankhah.Click += BtnPrintTankhah_Click;

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.WarmSecondary, 150, 35);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.SectionOrange, 140, 35);
            btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);

            var lblSearch = new Label { Text = "جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(15, 10, 5, 0), ForeColor = UIHelper.WarmText };
            txtSearch = new TextBox { Width = 200, Font = UIHelper.PersianFont(11f), PlaceholderText = "شرح، طرف حساب...", Margin = new Padding(5, 5, 5, 5) };
            txtSearch.TextChanged += (s, e) => ApplyFilterToTransactions();

            btnClose = UIHelper.CreateButton("بستن", UIHelper.SectionGray, 100, 35);
            btnClose.Click += (s, e) => this.Close();

            pnlToolbar.Controls.Add(btnClose);
            pnlToolbar.Controls.Add(btnDownloadTemplate);
            pnlToolbar.Controls.Add(btnImportExcel);
            pnlToolbar.Controls.Add(btnPrintTankhah);
            pnlToolbar.Controls.Add(btnChargeTankhah);
            pnlToolbar.Controls.Add(btnDeleteRow);
            pnlToolbar.Controls.Add(btnEditRow);
            pnlToolbar.Controls.Add(btnAddRow);
            pnlToolbar.Controls.Add(txtSearch);
            pnlToolbar.Controls.Add(lblSearch);

            dgvTransactions = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, RightToLeft = RightToLeft.Yes, AutoGenerateColumns = false };
            dgvTransactions.ApplyModernStyle();
            dgvTransactions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvTransactions.SelectionChanged += (s, e) => UpdateRowButtonsState(dgvTransactions.CurrentRow != null);
            dgvTransactions.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnEditRow_Click(null, EventArgs.Empty); };

            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "SourceType", DataPropertyName = "SourceType", Visible = false });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", DataPropertyName = "Date", HeaderText = "تاریخ", MinimumWidth = 80, FillWeight = 10 });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "شرح", MinimumWidth = 150, FillWeight = 25 });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scope", DataPropertyName = "Scope", HeaderText = "نوع سند", MinimumWidth = 100, FillWeight = 15 });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Contract", DataPropertyName = "Contract", HeaderText = "قرارداد / پروژه", MinimumWidth = 120, FillWeight = 15 });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostType", DataPropertyName = "CostType", HeaderText = "ماهیت هزینه", MinimumWidth = 90, FillWeight = 10 });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tafsili", DataPropertyName = "Tafsili", HeaderText = "طرف حساب", MinimumWidth = 100, FillWeight = 15 });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", MinimumWidth = 100, FillWeight = 13 });
            dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "وضعیت", MinimumWidth = 90, FillWeight = 12 });

            dgvTransactions.CellFormatting += DgvTransactions_CellFormatting;
            dgvTransactions.RowPrePaint += DgvTransactions_RowPrePaint;

            var pnlTransactionsFooter = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10, 5, 10, 0) };
            pnlTransactionsFooter.Paint += (s, e) => { using var pen = new Pen(UIHelper.WarmBorder, 1); e.Graphics.DrawLine(pen, 0, 0, pnlTransactionsFooter.Width, 0); };

            lblTotalExpenses = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UIHelper.PersianFont(11.5f, FontStyle.Bold),
                ForeColor = UIHelper.ActionRed,
                Text = "مانده حساب: 0 ریال"
            };
            pnlTransactionsFooter.Controls.Add(lblTotalExpenses);

            mainLayout.Controls.Add(pnlToolbar, 0, 0);
            mainLayout.Controls.Add(dgvTransactions, 0, 1);
            mainLayout.Controls.Add(pnlTransactionsFooter, 0, 2);
            this.Controls.Add(mainLayout);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var transTask = _transRepo.GetByAccountIdAsync(_activeTankhah.Id);
                    var checksTask = _checkRepo.GetByAccountIdAsync(_activeTankhah.Id);
                    var transfersTask = _transferRepo.GetByAccountIdAsync(_activeTankhah.Id);

                    await Task.WhenAll(transTask, checksTask, transfersTask);

                    _activeTransactions = transTask.Result ?? new List<CashTransaction>();
                    _activeChecks = checksTask.Result ?? new List<CashCheck>();
                    _activeTransfers = transfersTask.Result ?? new List<AccountTransfer>();

                    if (IsDisposed) return;
                    ApplyFilterToTransactions();
                }, "در حال بارگذاری ریز گردش حساب...");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahDetailForm.LoadDataAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
        }

        // ✅ FIX بحرانی: حالا شامل دریافتی نقدی/چک هم می‌شود
        // - قبلاً فقط Outgoing نمایش داده می‌شد → موجودی نهایی اشتباه بود
        // - حالا: هر سه منبع (Trf/Tx/Chk) در هر دو جهت
        private List<TankhahDisplayItem> BuildUnifiedList()
        {
            var list = new List<TankhahDisplayItem>();

            // ۱. انتقالات (شارژ و تسویه)
            foreach (var trf in _activeTransfers)
            {
                bool isIncoming = trf.DestinationAccountId == _activeTankhah.Id;
                list.Add(new TankhahDisplayItem
                {
                    Id = trf.Id,
                    SourceType = "Trf",
                    Date = trf.TransferDate,
                    Description = isIncoming
                        ? $"شارژ تنخواه از {trf.SourceAccountTitle}" + (string.IsNullOrWhiteSpace(trf.Description) ? "" : $" - {trf.Description}")
                        : $"تسویه تنخواه به {trf.DestinationAccountTitle}" + (string.IsNullOrWhiteSpace(trf.Description) ? "" : $" - {trf.Description}"),
                    Scope = isIncoming ? "شارژ (دریافتی)" : "تسویه (پرداختی)",
                    Amount = isIncoming ? trf.Amount : -trf.Amount,
                    IsIncome = isIncoming,
                    Status = "ثبت شده"
                });
            }

            // ۲. تراکنش‌های نقدی/حواله — هر دو جهت
            foreach (var t in _activeTransactions.Where(t => !string.Equals(t.Category?.Trim(), "انتقال بین حساب‌ها", StringComparison.Ordinal)))
            {
                bool isIncoming = t.Direction == DirectionTypes.Incoming;
                list.Add(new TankhahDisplayItem
                {
                    Id = t.Id,
                    SourceType = "Tx",
                    Date = t.TransactionDate,
                    Description = t.Description,
                    Scope = isIncoming ? "دریافت نقدی/حواله" : t.PaymentScopeDisplay,
                    Contract = t.ContractTitle,
                    CostType = t.CostType,
                    Tafsili = t.TafsiliTitle ?? t.RecipientName,
                    Amount = isIncoming ? t.Amount : -t.Amount,
                    IsIncome = isIncoming,
                    Status = isIncoming ? "ثبت شده" : t.BillingStatus
                });
            }

            // ۳. چک‌ها — هر دو جهت (بدون باطل/برگشتی)
            foreach (var c in _activeChecks.Where(c => c.Status != "باطل شده" && c.Status != "برگشتی"))
            {
                bool isIncoming = c.CheckType == DirectionTypes.Incoming;
                list.Add(new TankhahDisplayItem
                {
                    Id = c.Id,
                    SourceType = "Chk",
                    Date = c.DueDate,
                    Description = c.Description,
                    Scope = isIncoming ? "چک دریافتی" : "چک پرداختی",
                    Contract = c.ContractTitle,
                    CostType = c.CostType,
                    Tafsili = c.RecipientName,
                    Amount = isIncoming ? c.Amount : -c.Amount,
                    IsIncome = isIncoming,
                    Status = c.Status
                });
            }

            return list.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToList();
        }

        private void ApplyFilterToTransactions()
        {
            if (IsDisposed || _activeTankhah == null) return;

            string search = StringHelper.NormalizePersianText(txtSearch.Text.Trim());
            var allItems = BuildUnifiedList();

            var filtered = allItems.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(x =>
                    StringHelper.NormalizePersianText(x.Description ?? "").Contains(search) ||
                    StringHelper.NormalizePersianText(x.Tafsili ?? "").Contains(search) ||
                    StringHelper.NormalizePersianText(x.Contract ?? "").Contains(search) ||
                    StringHelper.NormalizePersianText(x.CostType ?? "").Contains(search) ||
                    StringHelper.NormalizePersianText(x.Scope ?? "").Contains(search));
            }

            var filteredList = filtered.ToList();

            var displayData = filteredList.Select(x => new
            {
                x.Id,
                x.SourceType,
                Date = DateHelper.ToPersianDate(x.Date),
                Description = string.IsNullOrWhiteSpace(x.Description) ? "---" : x.Description,
                Scope = string.IsNullOrWhiteSpace(x.Scope) ? "---" : x.Scope,
                Contract = string.IsNullOrWhiteSpace(x.Contract) ? "---" : x.Contract,
                CostType = string.IsNullOrWhiteSpace(x.CostType) ? "---" : x.CostType,
                Tafsili = string.IsNullOrWhiteSpace(x.Tafsili) ? "---" : x.Tafsili,
                Amount = NumberHelper.FormatThousands(x.Amount, usePersianDigits: true),
                Status = string.IsNullOrWhiteSpace(x.Status) ? "---" : x.Status
            }).ToList();

            dgvTransactions.DataSource = null;
            dgvTransactions.DataSource = displayData;
            UpdateRowButtonsState(dgvTransactions.CurrentRow != null);

            if (lblTotalExpenses != null)
            {
                // ✅ حالا همه‌ی گردش (دریافتی + پرداختی) در allItems هست
                decimal netFlow = allItems.Sum(x => x.Amount);
                decimal trueBalance = _activeTankhah.InitialBalance + netFlow;

                decimal filteredFlow = filteredList.Sum(x => x.Amount);
                bool isFiltered = !string.IsNullOrWhiteSpace(search);

                string text = $"مانده حساب: {NumberHelper.FormatThousands(trueBalance, usePersianDigits: true)} ریال";
                if (isFiltered)
                {
                    text += $"   |   خالص گردش موارد فیلترشده: {NumberHelper.FormatThousands(filteredFlow, usePersianDigits: true)} ریال";
                }
                lblTotalExpenses.Text = text;
                lblTotalExpenses.ForeColor = trueBalance >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed;
            }
        }

        private void UpdateRowButtonsState(bool hasRow)
        {
            if (IsDisposed) return;
            btnEditRow.Enabled = hasRow; btnDeleteRow.Enabled = hasRow;
        }

        private void DgvTransactions_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvTransactions.Columns[e.ColumnIndex].Name;
            if (colName == "Scope")
            {
                string val = e.Value?.ToString() ?? "";
                if (val.Contains("شارژ")) { e.CellStyle.ForeColor = UIHelper.ActionGreen; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("تسویه") || val.Contains("چک")) { e.CellStyle.ForeColor = UIHelper.SectionPurple; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("اداری")) { e.CellStyle.ForeColor = UIHelper.WarmText; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("خرید مستقیم") || val.Contains("پیمانکار")) { e.CellStyle.ForeColor = UIHelper.SectionBlue; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
                else if (val.Contains("دریافت")) { e.CellStyle.ForeColor = UIHelper.ActionGreen; e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold); }
            }
            else if (colName == "Amount")
            {
                string val = e.Value?.ToString() ?? "";
                if (val.StartsWith("-")) { e.CellStyle.ForeColor = UIHelper.ActionRed; }
                else { e.CellStyle.ForeColor = UIHelper.ActionGreen; }
            }
        }

        private void DgvTransactions_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string scope = dgvTransactions.Rows[e.RowIndex].Cells["Scope"].Value?.ToString() ?? "";

            if (scope.Contains("شارژ") || scope.Contains("دریافت")) dgvTransactions.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
            else if (scope.Contains("چک") || scope.Contains("تسویه")) dgvTransactions.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(248, 240, 252);
            else if (scope.Contains("اداری")) dgvTransactions.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(253, 251, 247);
            else if (scope.Contains("خرید مستقیم")) dgvTransactions.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
            else if (scope.Contains("پیمانکار")) dgvTransactions.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
            else dgvTransactions.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
        }

        private async void BtnAddRow_Click(object? sender, EventArgs e)
        {
            using var form = new TankhahRowEditForm(_connectionString, _activeTankhah.Id, _activeTankhah.Title);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed) await LoadDataAsync();
        }

        private async void BtnEditRow_Click(object? sender, EventArgs e)
        {
            if (dgvTransactions.CurrentRow == null || IsDisposed) return;

            int id = Convert.ToInt32(dgvTransactions.CurrentRow.Cells["Id"].Value);
            string sourceType = dgvTransactions.CurrentRow.Cells["SourceType"].Value?.ToString() ?? "Tx";

            if (sourceType == "Trf")
            {
                ModernMessageBox.ShowWarning("برای ویرایش شارژ/تسویه تنخواه، لطفاً از طریق دکمه «شارژ لیست» در نوار ابزار اقدام کنید.");
                return;
            }

            if (sourceType == "Chk")
            {
                using var checkForm = new CashCheckEditForm(_connectionString, id);
                if (checkForm.ShowDialog(this) == DialogResult.OK && !IsDisposed) await LoadDataAsync();
                return;
            }

            // ✅ FIX: برای دریافتی نقدی، از CashTransactionEditForm استفاده کن
            // (TankhahRowEditForm فقط برای هزینه است و Direction را Outgoing هاردکد می‌کند)
            var item = _activeTransactions.FirstOrDefault(t => t.Id == id);
            bool isIncoming = item != null && item.Direction == DirectionTypes.Incoming;

            if (isIncoming)
            {
                using var cashForm = new CashTransactionEditForm(_connectionString, id);
                if (cashForm.ShowDialog(this) == DialogResult.OK && !IsDisposed) await LoadDataAsync();
            }
            else
            {
                using var form = new TankhahRowEditForm(_connectionString, _activeTankhah.Id, _activeTankhah.Title, id);
                if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed) await LoadDataAsync();
            }
        }

        private async void BtnDeleteRow_Click(object? sender, EventArgs e)
        {
            if (dgvTransactions.CurrentRow == null || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                int id = Convert.ToInt32(dgvTransactions.CurrentRow.Cells["Id"].Value);
                string sourceType = dgvTransactions.CurrentRow.Cells["SourceType"].Value?.ToString() ?? "Tx";
                string desc = dgvTransactions.CurrentRow.Cells["Description"].Value?.ToString() ?? "";
                decimal amount = Math.Abs(NumberHelper.ParseFormatted(dgvTransactions.CurrentRow.Cells["Amount"].Value?.ToString()));

                if (sourceType == "Trf")
                {
                    ModernMessageBox.ShowWarning("برای حذف شارژ/تسویه تنخواه، لطفاً به منوی مدیریت تنخواه‌ها بروید.");
                    return;
                }

                if (sourceType == "Tx")
                {
                    var trans = _activeTransactions.FirstOrDefault(t => t.Id == id);
                    if (trans != null && trans.ClientStatementId.HasValue)
                    {
                        ModernMessageBox.ShowWarning("این ردیف در یک صورت وضعیت استفاده شده و قابل حذف نیست.");
                        return;
                    }
                }
                else if (sourceType == "Chk")
                {
                    var check = _activeChecks.FirstOrDefault(c => c.Id == id);
                    if (check != null && check.ClientStatementId.HasValue)
                    {
                        ModernMessageBox.ShowWarning("این چک در یک صورت وضعیت استفاده شده و قابل حذف نیست.");
                        return;
                    }
                }

                if (!ModernMessageBox.AskConfirm(
                    $"آیا از حذف این ردیف مطمئن هستید؟\n\n" +
                    $"شرح: {desc}\n" +
                    $"مبلغ: {NumberHelper.FormatThousands(amount, usePersianDigits: true)} ریال\n\n" +
                    $"⚠️ مبلغ به موجودی لیست برگردانده می‌شود.", "حذف ردیف"))
                    return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (sourceType == "Chk")
                        await _checkRepo.DeleteBatchAsync(new List<int> { id });
                    else
                        await _transRepo.DeleteBatchAsync(new List<int> { id });
                }, "در حال حذف...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("ردیف حذف و موجودی اصلاح شد.", true);
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahDetailForm.BtnDeleteRow_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private void BtnChargeTankhah_Click(object? sender, EventArgs e)
        {
            using var form = new TankhahTransferForm(_connectionString, TankhahTransferMode.BankToTankhah, _activeTankhah.Id);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
            {
                _ = LoadDataAsync();
            }
        }

        private void BtnPrintTankhah_Click(object? sender, EventArgs e)
        {
            var expensesOnly = _activeTransactions
                .Where(t => t.Direction == DirectionTypes.Outgoing && !string.Equals(t.Category?.Trim(), "انتقال بین حساب‌ها", StringComparison.Ordinal))
                .ToList();

            if (expensesOnly.Count == 0) { ModernMessageBox.ShowWarning("این لیست هیچ هزینه نقدی/حواله‌ای برای چاپ ندارد."); return; }
            using var form = new TankhahPrintForm(_connectionString, _activeTankhah, expensesOnly);
            form.ShowDialog(this);
        }

        private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = "Tankhah_Import_Template.xlsx",
                Title = "ذخیره قالب اکسل لیست"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("لیست هزینه");
                ws.RightToLeft = true;

                string[] headers = {
                    "تاریخ (شمسی 1403/01/01)", "شرح / توضیحات", "مبلغ (ریال)",
                    "نوع پرداخت (هزینه اداری / پیمانکار قراردادی / پیمانکار فاکتوری / فروشنده کالا / حقوق و مساعده)",
                    "ماهیت هزینه (مثلاً خرید کالا/خدمات/سایر)", "طرف حساب (تفصیلی)", "شماره قرارداد (اختیاری)"
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
                ws.Cell(2, 2).Value = "خرید اقلام مصرفی";
                ws.Cell(2, 3).Value = 5000000;
                ws.Cell(2, 4).Value = "هزینه اداری";
                ws.Cell(2, 5).Value = "لوازم مصرفی";
                ws.Cell(2, 6).Value = "فروشگاه الف";
                ws.Cell(2, 7).Value = "";

                ws.Range("D2:D1000").CreateDataValidation().List("\"هزینه اداری,پیمانکار قراردادی,پیمانکار فاکتوری,فروشنده کالا,حقوق و مساعده\"");
                ws.Columns().AdjustToContents();
                ws.Row(1).Height = 45;
                workbook.SaveAs(sfd.FileName);

                ToastHelper.Show("قالب اکسل با موفقیت ساخته شد.", true);
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message); }
        }

        private async Task BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل لیست" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                var transactions = new List<CashTransaction>();
                var errors = new List<string>();
                int successCount = 0;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    using var workbook = new XLWorkbook(ofd.FileName);
                    var ws = workbook.Worksheet(1);
                    var rows = ws.RowsUsed().Skip(1);

                    int rowNum = 1;
                    foreach (var row in rows)
                    {
                        rowNum++;
                        try
                        {
                            string dateStr = row.Cell(1).GetFormattedString().Trim();
                            string desc = row.Cell(2).GetFormattedString().Trim();
                            string amountStr = row.Cell(3).GetFormattedString().Trim();
                            string scopeStr = row.Cell(4).GetFormattedString().Trim();
                            string costTypeStr = row.Cell(5).GetFormattedString().Trim();
                            string tafsiliStr = row.Cell(6).GetFormattedString().Trim();
                            string contractNumStr = row.Cell(7).GetFormattedString().Trim();

                            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

                            DateTime? date = DateHelper.ParsePersianDate(dateStr);
                            if (date == null) throw new Exception($"تاریخ «{dateStr}» معتبر نیست.");

                            decimal amount = NumberHelper.ParseFormatted(amountStr);
                            if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");
                            if (string.IsNullOrWhiteSpace(desc)) throw new Exception("شرح/توضیحات خالی است.");

                            string paymentScope = PaymentScopes.Administrative;
                            string category = "عملیاتی";
                            string finalCostType = string.IsNullOrWhiteSpace(costTypeStr) ? "سایر" : costTypeStr;

                            if (scopeStr.Contains("پیمانکار قراردادی")) paymentScope = PaymentScopes.ContractorContract;
                            else if (scopeStr.Contains("پیمانکار فاکتوری") || scopeStr.Contains("بدون قرارداد")) paymentScope = PaymentScopes.InformalContractor;
                            else if (scopeStr.Contains("فروشنده کالا") || scopeStr.Contains("خرید مستقیم")) paymentScope = PaymentScopes.DirectPurchase;
                            else if (scopeStr.Contains("حقوق") || scopeStr.Contains("مساعده")) { paymentScope = PaymentScopes.Administrative; finalCostType = "حقوق و دستمزد"; category = "پرسنلی"; }

                            int? contractId = null;
                            string contractTitle = "---";

                            if (!string.IsNullOrWhiteSpace(contractNumStr))
                            {
                                using var conn = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                                await conn.OpenAsync();
                                using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT TOP 1 Id, Subject FROM ClientContracts WHERE ContractNumber = @num", conn);
                                cmd.Parameters.AddWithValue("@num", contractNumStr);
                                using var reader = await cmd.ExecuteReaderAsync();
                                if (await reader.ReadAsync())
                                {
                                    contractId = reader.GetInt32(0);
                                    contractTitle = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                }
                            }

                            transactions.Add(new CashTransaction
                            {
                                AccountId = _activeTankhah.Id,
                                AccountTitle = _activeTankhah.Title,
                                TransactionDate = date.Value,
                                Amount = amount,
                                Direction = DirectionTypes.Outgoing,
                                TransactionType = "نقدی",
                                PaymentScope = paymentScope,
                                CostType = finalCostType,
                                Category = category,
                                TafsiliTitle = string.IsNullOrWhiteSpace(tafsiliStr) ? null : tafsiliStr,
                                RecipientName = string.IsNullOrWhiteSpace(tafsiliStr) ? null : tafsiliStr,
                                ContractId = contractId,
                                ContractTitle = contractTitle,
                                Description = desc
                            });
                            successCount++;
                        }
                        catch (Exception ex) { errors.Add($"ردیف {rowNum}: {ex.Message}"); }
                    }

                    if (transactions.Count > 0) await _transRepo.BulkInsertAsync(transactions);
                }, "در حال ایمپورت از اکسل...");

                if (success && !IsDisposed)
                {
                    string msg = $"✅ تعداد {successCount} ردیف با موفقیت اضافه شد.";
                    if (errors.Count > 0) msg += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(5));
                    ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahDetailForm.ImportExcel");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}