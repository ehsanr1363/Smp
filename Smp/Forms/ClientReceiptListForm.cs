using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using SMP.Helpers;
using SMP.Data;
using SMP.Models;

namespace SMP.Forms
{
    public class ClientReceiptListForm : Form
    {
        private readonly string _connectionString;
        private readonly CashTransactionRepository _cashRepo;
        private readonly CashCheckRepository _checkRepo;

        private DataGridView dgvReceipts = null!;
        private ComboBox cmbContract = null!;
        private TextBox txtSearch = null!, txtFromDate = null!, txtToDate = null!;

        private Button btnFilter = null!, btnDelete = null!, btnRefresh = null!, btnEdit = null!, btnPrint = null!;
        private Button btnImportExcel = null!, btnDownloadTemplate = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        private Image? _companyLogo = null;
        private PrintDocument _printDoc = null!;
        private List<PrintGroup> _printGroups = new();
        private int _currentGroupIndex = 0;
        private int _currentRowIndex = 0;

        private class PrintGroup
        {
            public string ContractDisplayName { get; set; } = "";
            public List<DataGridViewRow> Rows { get; set; } = new();
            public decimal TotalAmount { get; set; }
        }

        public ClientReceiptListForm(string connectionString)
        {
            _connectionString = connectionString;
            _cashRepo = new CashTransactionRepository(connectionString);
            _checkRepo = new CashCheckRepository(connectionString);

            BuildUI();
            LoadCompanyLogo();
            SetupPrintDocument();

            this.ApplyStandardHeader("لیست دریافتی‌های کارفرمایان", new Size(1200, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

            Shown += async (s, e) =>
            {
                await LoadContractsAsync();
                await LoadDataSafeAsync();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _actionLock?.Dispose(); } catch { }
                try { _printDoc?.Dispose(); } catch { }
                try { _companyLogo?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var pnlTop = UIHelper.CreateWarmPanel();
            pnlTop.Padding = new Padding(10);

            var topLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));

            var inputLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, Margin = new Padding(0) };
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            inputLayout.Controls.Add(new Label { Text = "قرارداد:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold) }, 0, 0);

            cmbContract = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(10.5f), Margin = new Padding(5, 8, 5, 5) };
            cmbContract.SelectedIndexChanged += async (s, e) => await LoadDataSafeAsync();
            inputLayout.Controls.Add(cmbContract, 1, 0);

            inputLayout.Controls.Add(new Label { Text = "از تاریخ:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold) }, 2, 0);

            txtFromDate = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10.5f), TextAlign = HorizontalAlignment.Center, Margin = new Padding(5, 8, 5, 5) };
            DateHelper.ApplyDateMask(txtFromDate);
            inputLayout.Controls.Add(txtFromDate, 3, 0);

            inputLayout.Controls.Add(new Label { Text = "تا تاریخ:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold) }, 4, 0);

            txtToDate = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10.5f), TextAlign = HorizontalAlignment.Center, Margin = new Padding(5, 8, 5, 5) };
            DateHelper.ApplyDateMask(txtToDate);
            inputLayout.Controls.Add(txtToDate, 5, 0);

            inputLayout.Controls.Add(new Label { Text = "جستجو:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold) }, 6, 0);

            txtSearch = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10.5f), PlaceholderText = "توضیحات...", Margin = new Padding(5, 8, 5, 5) };
            inputLayout.Controls.Add(txtSearch, 7, 0);

            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };

            btnFilter = UIHelper.CreateButton("🔍 اعمال فیلتر", UIHelper.SectionBlue, 130, 36);
            btnFilter.Margin = new Padding(5, 5, 5, 5);
            btnFilter.Click += async (s, e) => await LoadDataSafeAsync();

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 36);
            btnRefresh.Margin = new Padding(5, 5, 5, 5);
            btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.SectionTeal, 170, 36);
            btnDownloadTemplate.Margin = new Padding(15, 5, 5, 5);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.ActionGreen, 170, 36);
            btnImportExcel.Margin = new Padding(5, 5, 5, 5);
            btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);

            btnFlow.Controls.Add(btnFilter);
            btnFlow.Controls.Add(btnRefresh);
            btnFlow.Controls.Add(btnDownloadTemplate);
            btnFlow.Controls.Add(btnImportExcel);

            topLayout.Controls.Add(inputLayout, 0, 0);
            topLayout.Controls.Add(btnFlow, 0, 1);

            pnlTop.Controls.Add(topLayout);
            mainLayout.Controls.Add(pnlTop, 0, 0);

            dgvReceipts = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, ReadOnly = true };
            dgvReceipts.ApplyModernStyle();
            dgvReceipts.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnEdit_Click(null, EventArgs.Empty); };

            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "TableName", DataPropertyName = "TableName", Visible = false });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountId", DataPropertyName = "AccountId", Visible = false });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", DataPropertyName = "Category", Visible = false });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsSummary", DataPropertyName = "IsSummary", Visible = false });

            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractDisplayName", DataPropertyName = "ContractDisplayName", HeaderText = "قرارداد / پروژه", FillWeight = 20 });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountTitle", DataPropertyName = "AccountTitle", HeaderText = "حساب / بانک واریز", FillWeight = 20 });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReceiptDate", DataPropertyName = "ReceiptDate", HeaderText = "تاریخ دریافت", FillWeight = 15 });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReceiptType", DataPropertyName = "ReceiptType", HeaderText = "روش دریافت", FillWeight = 15 });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });
            dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 20 });

            dgvReceipts.RowPrePaint += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value is bool isSum && isSum)
                {
                    dgvReceipts.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(245, 235, 220);
                    dgvReceipts.Rows[e.RowIndex].DefaultCellStyle.Font = UIHelper.PersianFont(11f, FontStyle.Bold);
                    dgvReceipts.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(90, 60, 40);
                }
            };

            dgvReceipts.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvReceipts.Columns[e.ColumnIndex].DataPropertyName;

                bool isSum = false;
                if (dgvReceipts.Columns.Contains("IsSummary") && dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value != DBNull.Value && dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value != null)
                    isSum = Convert.ToBoolean(dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value);

                if (colName == "ReceiptDate")
                {
                    if (isSum) { e.Value = "---"; e.FormattingApplied = true; }
                    else if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
                }
                else if (colName == "Amount" && e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
                else if (colName == "AccountTitle" && isSum)
                {
                    e.Value = "---";
                    e.FormattingApplied = true;
                }
            };

            mainLayout.Controls.Add(dgvReceipts, 0, 1);

            var pnlBottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };

            btnEdit = UIHelper.CreateButton("✏️ ویرایش دریافتی", UIHelper.SectionBlue, 150, 40);
            btnEdit.Click += BtnEdit_Click;

            btnPrint = UIHelper.CreateButton("🖨️ چاپ لیست", UIHelper.ActionAmber, 130, 40);
            btnPrint.Click += BtnPrint_Click;

            btnDelete = UIHelper.CreateButton("🗑️ حذف دریافتی", UIHelper.WarmDanger, 150, 40);
            btnDelete.Click += BtnDelete_Click;

            pnlBottom.Controls.Add(btnEdit);
            pnlBottom.Controls.Add(btnPrint);
            pnlBottom.Controls.Add(btnDelete);

            mainLayout.Controls.Add(pnlBottom, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private class ContractComboItem
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public override string ToString() => Display;
        }

        private void LoadCompanyLogo()
        {
            try
            {
                var companyRepo = new CompanyRepository(_connectionString);
                var company = companyRepo.GetAll().FirstOrDefault();
                if (company?.Logo != null && company.Logo.Length > 0)
                {
                    _companyLogo = ImageHelper.ByteArrayToImage(company.Logo);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "ClientReceiptListForm");
            }
        }

        private async Task LoadContractsAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT Id, ContractNumber, Subject FROM ClientContracts ORDER BY Id DESC", conn);
                using var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                cmbContract.Items.Clear();
                cmbContract.Items.Add(new ContractComboItem { Id = 0, Display = "--- همه قراردادها ---" });

                foreach (DataRow row in dt.Rows)
                {
                    cmbContract.Items.Add(new ContractComboItem
                    {
                        Id = Convert.ToInt32(row["Id"]),
                        Display = $"{row["ContractNumber"]} - {row["Subject"]}"
                    });
                }

                if (cmbContract.Items.Count > 0)
                    cmbContract.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientReceiptListForm.LoadContracts");
                ModernMessageBox.ShowError("خطا در لود قراردادها: " + ex.Message);
            }
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                int? contractId = null;
                if (cmbContract.SelectedItem is ContractComboItem selectedContract && selectedContract.Id > 0)
                {
                    contractId = selectedContract.Id;
                }

                DateTime? fromDate = DateHelper.ParsePersianDate(txtFromDate.Text);
                DateTime? toDate = DateHelper.ParsePersianDate(txtToDate.Text);
                string search = txtSearch.Text.Trim();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();

                    string query = @"
                        SELECT 
                            r.Id, 
                            r.TableName, 
                            r.ContractId,
                            r.AccountId,
                            CASE 
                                WHEN c.Id IS NOT NULL THEN ISNULL(c.ContractNumber, '') + CASE WHEN ISNULL(c.Subject, '') <> '' THEN ' - ' + c.Subject ELSE '' END
                                ELSE ISNULL(NULLIF(r.ContractTitle, ''), '---')
                            END AS ContractDisplayName,
                            r.ReceiptDate, 
                            r.Amount, 
                            r.ReceiptType, 
                            r.Description,
                            r.Category,
                            ISNULL(r.AccountTitle, '---') AS AccountTitle
                        FROM (
                            SELECT t.Id, 'CashTransactions' AS TableName, t.ContractId, t.AccountId, 
                                   t.TransactionDate AS ReceiptDate, t.Amount, t.TransactionType AS ReceiptType, 
                                   t.Description, ISNULL(t.ContractTitle, '') AS ContractTitle, ISNULL(t.Category, '') AS Category,
                                   ISNULL(a.Title, '---') AS AccountTitle
                            FROM CashTransactions t 
                            LEFT JOIN CompanyBankAccounts a ON t.AccountId = a.Id
                            WHERE (LTRIM(RTRIM(t.Direction)) = N'دریافتی' OR LTRIM(RTRIM(t.Direction)) = N'دریافت')
                              AND LTRIM(RTRIM(ISNULL(t.Category, ''))) NOT IN (N'انتقال بین حساب‌ها', N'سود بانکی', N'سود', N'شارژ تنخواه')
                              
                            UNION ALL
                            
                            SELECT k.Id, 'CashChecks' AS TableName, k.ContractId, k.AccountId, 
                                   k.DueDate AS ReceiptDate, k.Amount, N'چک دریافتی' AS ReceiptType, 
                                   k.Description, ISNULL(k.ContractTitle, '') AS ContractTitle, N'چک' AS Category,
                                   ISNULL(a.Title, '---') AS AccountTitle
                            FROM CashChecks k 
                            LEFT JOIN CompanyBankAccounts a ON k.AccountId = a.Id
                            WHERE LTRIM(RTRIM(k.CheckType)) = N'دریافتی'
                        ) r
                        LEFT JOIN ClientContracts c ON r.ContractId = c.Id
                        WHERE 1=1";

                    if (contractId.HasValue) query += " AND r.ContractId = @cid";
                    if (fromDate.HasValue) query += " AND CAST(r.ReceiptDate AS DATE) >= CAST(@from AS DATE)";
                    if (toDate.HasValue) query += " AND CAST(r.ReceiptDate AS DATE) <= CAST(@to AS DATE)";
                    if (!string.IsNullOrWhiteSpace(search)) query += " AND (r.Description LIKE @search OR c.ContractNumber LIKE @search OR r.ContractTitle LIKE @search)";

                    query += " ORDER BY r.ReceiptDate DESC, r.Id DESC";

                    using var cmd = new SqlCommand(query, conn);
                    if (contractId.HasValue) cmd.Parameters.AddWithValue("@cid", contractId.Value);
                    if (fromDate.HasValue) cmd.Parameters.AddWithValue("@from", fromDate.Value);
                    if (toDate.HasValue) cmd.Parameters.AddWithValue("@to", toDate.Value);
                    if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search}%");

                    using var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    await Task.Run(() => adapter.Fill(dt));

                    if (IsDisposed) return;

                    var groupedDt = dt.Clone();
                    groupedDt.Columns.Add("IsSummary", typeof(bool));

                    var sortedRows = dt.AsEnumerable()
                        .OrderBy(r => r.Field<string>("ContractDisplayName") ?? "---")
                        .ThenByDescending(r => r.Field<DateTime>("ReceiptDate"));

                    foreach (var group in sortedRows.GroupBy(r => r.Field<string>("ContractDisplayName") ?? "---"))
                    {
                        decimal total = 0;
                        foreach (var row in group)
                        {
                            var newRow = groupedDt.NewRow();
                            newRow.ItemArray = row.ItemArray;
                            newRow["IsSummary"] = false;
                            groupedDt.Rows.Add(newRow);
                            total += row.Field<decimal>("Amount");
                        }

                        var sumRow = groupedDt.NewRow();
                        sumRow["ContractDisplayName"] = group.Key;
                        sumRow["ReceiptType"] = "جمع کل";
                        sumRow["Amount"] = total;
                        sumRow["Description"] = $"جمع کل واریزی بابت {group.Key}";
                        sumRow["IsSummary"] = true;
                        if (groupedDt.Columns.Contains("Id")) sumRow["Id"] = 0;
                        if (groupedDt.Columns.Contains("TableName")) sumRow["TableName"] = "Summary";
                        if (groupedDt.Columns.Contains("AccountTitle")) sumRow["AccountTitle"] = "---";
                        if (groupedDt.Columns.Contains("Category")) sumRow["Category"] = "";
                        if (groupedDt.Columns.Contains("AccountId")) sumRow["AccountId"] = DBNull.Value;
                        if (groupedDt.Columns.Contains("ContractId")) sumRow["ContractId"] = DBNull.Value;
                        if (groupedDt.Columns.Contains("ReceiptDate")) sumRow["ReceiptDate"] = DBNull.Value;

                        groupedDt.Rows.Add(sumRow);
                    }

                    dgvReceipts.DataSource = groupedDt;
                }, "در حال بارگذاری لیست دریافتی‌ها...");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientReceiptListForm.LoadDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری لیست:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private void SetupPrintDocument()
        {
            _printDoc = new PrintDocument();
            _printDoc.DefaultPageSettings.Landscape = true;
            _printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
            _printDoc.BeginPrint += (s, e) => { _currentGroupIndex = 0; _currentRowIndex = 0; };
            _printDoc.PrintPage += PrintDoc_PrintPage;
        }

        private void BtnPrint_Click(object? sender, EventArgs e)
        {
            if (IsDisposed || dgvReceipts.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای برای چاپ وجود ندارد.");
                return;
            }

            _printGroups.Clear();
            string currentContract = "";
            PrintGroup? currentGroup = null;

            foreach (DataGridViewRow row in dgvReceipts.Rows)
            {
                if (row.IsNewRow) continue;

                string contract = row.Cells["ContractDisplayName"].Value?.ToString() ?? "---";
                bool isSummary = row.Cells["IsSummary"].Value != null && Convert.ToBoolean(row.Cells["IsSummary"].Value);

                if (currentGroup == null || currentContract != contract)
                {
                    currentContract = contract;
                    currentGroup = new PrintGroup { ContractDisplayName = contract };
                    _printGroups.Add(currentGroup);
                }

                if (!isSummary)
                {
                    currentGroup.Rows.Add(row);
                }
                else
                {
                    currentGroup.TotalAmount = NumberHelper.ParseFormatted(row.Cells["Amount"].Value?.ToString());
                }
            }

            using var preview = new PrintPreviewDialog
            {
                Document = _printDoc,
                Width = 1100,
                Height = 800,
                WindowState = FormWindowState.Maximized,
                UseAntiAlias = true,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Text = "پیش‌نمایش چاپ گزارش دریافتی‌ها"
            };
            preview.ShowDialog(this);
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 40f;

            var fontMainTitle = UIHelper.PersianFont(16f, FontStyle.Bold);
            var fontGroupTitle = UIHelper.PersianFont(12f, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(10f, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(10f);
            var fontTotal = UIHelper.PersianFont(10f, FontStyle.Bold);

            using StringFormat rtlCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat rtlRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

            float yPos = margin;
            float topMarginOffset = 0;

            if (_companyLogo != null)
            {
                try
                {
                    float logoWidth = 90f;
                    float logoHeight = 80f;
                    float logoX = pageWidth - margin - logoWidth;
                    g.DrawImage(_companyLogo, logoX, margin - 10, logoWidth, logoHeight);
                    topMarginOffset = 85f;
                }
                catch (ArgumentException) { }
            }

            yPos = margin + topMarginOffset;

            if (_currentGroupIndex == 0)
            {
                g.DrawString("گزارش لیست دریافتی‌های کارفرمایان", fontMainTitle, Brushes.Black, new RectangleF(0, yPos, pageWidth, 30), titleFormat);
                g.DrawString($"تاریخ چاپ: {DateHelper.ToPersianDate(DateTime.Now)}", fontBody, Brushes.Gray, new RectangleF(margin, yPos + 30, 200, 20), rtlRight);
                yPos += 60;
            }

            float tableWidth = pageWidth - (margin * 2);
            float[] proportions = { 15f, 20f, 15f, 15f, 35f };
            float[] colWidths = proportions.Select(p => (p / 100f) * tableWidth).ToArray();
            float rowHeight = 25f;
            float headerRowHeight = 30f;

            while (_currentGroupIndex < _printGroups.Count)
            {
                var group = _printGroups[_currentGroupIndex];

                if (yPos > margin + 60) yPos += 20;

                string headerText = $"دریافتی‌های بابت {group.ContractDisplayName}";
                g.DrawString(headerText, fontGroupTitle, Brushes.DarkBlue, new RectangleF(margin, yPos, tableWidth, 25), rtlRight);
                yPos += 30;

                string[] headers = { "تاریخ دریافت", "حساب / بانک واریز", "روش دریافت", "مبلغ (ریال)", "توضیحات" };
                g.FillRectangle(Brushes.LightGray, margin, yPos, tableWidth, headerRowHeight);
                g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, headerRowHeight);

                float currentX = pageWidth - margin;
                for (int i = 0; i < headers.Length; i++)
                {
                    currentX -= colWidths[i];
                    // ✅ اصلاح شد: رسم خطوط عمودی برای تمام ستون‌ها به جز آخرین ستون (لبه سمت چپ)
                    if (i < headers.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + headerRowHeight);
                    g.DrawString(headers[i], fontHeader, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], headerRowHeight), rtlCenter);
                }
                yPos += headerRowHeight;

                while (_currentRowIndex < group.Rows.Count)
                {
                    if (yPos + rowHeight > pageHeight - margin)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    var row = group.Rows[_currentRowIndex];
                    string dateVal = row.Cells["ReceiptDate"].FormattedValue?.ToString() ?? "";
                    string accVal = row.Cells["AccountTitle"].FormattedValue?.ToString() ?? "";
                    string methodVal = row.Cells["ReceiptType"].FormattedValue?.ToString() ?? "";
                    string amountVal = row.Cells["Amount"].FormattedValue?.ToString() ?? "";
                    string descVal = row.Cells["Description"].FormattedValue?.ToString() ?? "";

                    string[] rowData = { dateVal, accVal, methodVal, amountVal, descVal };

                    g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, rowHeight);
                    currentX = pageWidth - margin;
                    for (int i = 0; i < rowData.Length; i++)
                    {
                        currentX -= colWidths[i];
                        // ✅ اصلاح شد
                        if (i < rowData.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                        g.DrawString(rowData[i], fontBody, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlCenter);
                    }

                    yPos += rowHeight;
                    _currentRowIndex++;
                }

                if (yPos + rowHeight > pageHeight - margin)
                {
                    e.HasMorePages = true;
                    return;
                }

                g.FillRectangle(Brushes.Beige, margin, yPos, tableWidth, rowHeight);
                g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, rowHeight);

                currentX = pageWidth - margin;
                string[] totalRowData = { "جمع کل", "---", "---", NumberHelper.FormatThousands(group.TotalAmount, usePersianDigits: true), $"جمع کل دریافتی‌های این قرارداد" };

                for (int i = 0; i < totalRowData.Length; i++)
                {
                    currentX -= colWidths[i];
                    // ✅ اصلاح شد
                    if (i < totalRowData.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                    g.DrawString(totalRowData[i], fontTotal, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlCenter);
                }

                yPos += rowHeight + 10;
                _currentGroupIndex++;
                _currentRowIndex = 0;
            }

            e.HasMorePages = false;
        }

        private async void BtnEdit_Click(object? sender, EventArgs e)
        {
            if (dgvReceipts.CurrentRow == null || IsDisposed) return;

            if (dgvReceipts.CurrentRow.Cells["IsSummary"].Value is bool isSum && isSum)
            {
                ModernMessageBox.ShowWarning("این ردیف مربوط به جمع کل است و قابل ویرایش نیست.");
                return;
            }

            try
            {
                string category = dgvReceipts.CurrentRow.Cells["Category"].Value?.ToString() ?? "";
                if (category == "انتقال بین حساب‌ها")
                {
                    ModernMessageBox.ShowWarning("این رکورد مربوط به انتقال بین حساب‌هاست. برای ویرایش، از منوی «انتقال بین حساب‌ها» استفاده کنید.");
                    return;
                }

                int id = Convert.ToInt32(dgvReceipts.CurrentRow.Cells["Id"].Value);
                string tableName = dgvReceipts.CurrentRow.Cells["TableName"].Value?.ToString() ?? "";
                bool isUpdated = false;

                if (tableName == "CashTransactions")
                {
                    using var form = new CashTransactionEditForm(_connectionString, id);
                    if (form.ShowDialog(this) == DialogResult.OK) isUpdated = true;
                }
                else if (tableName == "CashChecks")
                {
                    using var form = new CashCheckEditForm(_connectionString, id);
                    if (form.ShowDialog(this) == DialogResult.OK) isUpdated = true;
                }

                if (isUpdated && !IsDisposed)
                {
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientReceiptListForm.BtnEdit_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ویرایش:\n" + ex.Message);
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvReceipts.CurrentRow == null || IsDisposed) return;

            if (dgvReceipts.CurrentRow.Cells["IsSummary"].Value is bool isSum && isSum)
            {
                ModernMessageBox.ShowWarning("این ردیف مربوط به جمع کل است و قابل حذف نیست.");
                return;
            }

            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                string category = dgvReceipts.CurrentRow.Cells["Category"].Value?.ToString() ?? "";
                if (category == "انتقال بین حساب‌ها")
                {
                    ModernMessageBox.ShowWarning("این رکورد مربوط به انتقال بین حساب‌هاست. برای حذف، از منوی «انتقال بین حساب‌ها» استفاده کنید.");
                    return;
                }

                int id = Convert.ToInt32(dgvReceipts.CurrentRow.Cells["Id"].Value);
                string tableName = dgvReceipts.CurrentRow.Cells["TableName"].Value?.ToString() ?? "";
                decimal amount = NumberHelper.ParseFormatted(dgvReceipts.CurrentRow.Cells["Amount"].Value?.ToString());

                if (!ModernMessageBox.AskConfirm($"آیا از حذف این دریافتی به مبلغ {NumberHelper.FormatThousands(amount, usePersianDigits: true)} ریال مطمئن هستید؟\n\nتوجه: اگر حواله یا نقدی باشد، مبلغ از موجودی حساب بانکی کسر می‌شود.", "حذف دریافتی"))
                    return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (tableName == "CashChecks")
                    {
                        await _checkRepo.DeleteBatchAsync(new List<int> { id });
                    }
                    else if (tableName == "CashTransactions")
                    {
                        await _cashRepo.DeleteBatchAsync(new List<int> { id });
                    }
                }, "در حال حذف دریافتی...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("دریافتی با موفقیت حذف شد.", isSuccess: true);
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientReceiptListForm.BtnDelete_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = "ClientReceipts_Import_Template.xlsx",
                Title = "ذخیره قالب اکسل دریافتی‌ها"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("دریافتی‌ها");
                ws.RightToLeft = true;

                string[] headers = {
                    "تاریخ دریافت (1403/01/01)",
                    "مبلغ (ریال)",
                    "روش دریافت (نقدی/حواله بانکی/چک)",
                    "توضیحات",
                    "شماره قرارداد (اختیاری - اگر خالی باشد از فیلتر بالای صفحه استفاده می‌شود)"
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
                ws.Cell(2, 2).Value = 10000000;
                ws.Cell(2, 3).Value = "نقدی";
                ws.Cell(2, 4).Value = "بابت دریافت وجه";
                ws.Cell(2, 5).Value = "";

                ws.Range("C2:C1000").CreateDataValidation().List("\"نقدی,حواله بانکی,چک,سایر\"");

                ws.Columns().AdjustToContents();
                ws.Row(1).Height = 40;
                workbook.SaveAs(sfd.FileName);

                ToastHelper.Show("قالب اکسل با موفقیت ساخته شد.", true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientReceiptListForm.DownloadTemplate");
                ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
            }
        }

        private async Task BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل دریافتی‌ها" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            var accounts = await new BankAccountRepository(_connectionString).GetActiveAsync();
            if (accounts == null || accounts.Count == 0)
            {
                ModernMessageBox.ShowWarning("هیچ حساب بانکی فعالی یافت نشد. ابتدا یک حساب تعریف کنید.");
                return;
            }

            using var selectForm = new Form();
            selectForm.Text = "انتخاب حساب بانکی";
            selectForm.Size = new Size(400, 200);
            selectForm.StartPosition = FormStartPosition.CenterParent;
            selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            selectForm.MaximizeBox = false;
            selectForm.MinimizeBox = false;
            selectForm.RightToLeft = RightToLeft.Yes;
            selectForm.RightToLeftLayout = true;
            selectForm.Font = UIHelper.PersianFont(11f);

            var lbl = new Label { Text = "این دریافتی‌ها به کدام حساب بانکی واریز شده‌اند؟", Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            var cmb = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", DataSource = new List<CompanyBankAccount>(accounts) };
            var btnOk = UIHelper.CreateButton("تایید و ادامه", UIHelper.ActionGreen, 120, 35);
            btnOk.Dock = DockStyle.Bottom;
            btnOk.Click += (s, ev) => { selectForm.DialogResult = DialogResult.OK; selectForm.Close(); };

            selectForm.Controls.Add(btnOk); selectForm.Controls.Add(cmb); selectForm.Controls.Add(lbl);

            if (selectForm.ShowDialog(this) != DialogResult.OK || cmb.SelectedItem is not CompanyBankAccount targetAcc) return;

            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                int defaultContractId = 0;
                if (cmbContract.SelectedItem is ContractComboItem selectedContract)
                {
                    defaultContractId = selectedContract.Id;
                }

                int successCount = 0;
                int errorCount = 0;
                List<string> errors = new();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    using var workbook = new XLWorkbook(ofd.FileName);
                    var ws = workbook.Worksheet(1);
                    var rows = ws.RowsUsed().Skip(1);

                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();

                    int rowNum = 1;
                    foreach (var row in rows)
                    {
                        rowNum++;
                        try
                        {
                            string dateStr = row.Cell(1).GetString().Trim();
                            string amountStr = row.Cell(2).GetString().Trim();
                            string method = row.Cell(3).GetString().Trim();
                            string desc = row.Cell(4).GetString().Trim();
                            string contractNum = row.Cell(5).GetString().Trim();

                            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

                            DateTime? date = DateHelper.ParsePersianDate(dateStr);
                            if (date == null) throw new Exception("تاریخ دریافت نامعتبر است.");

                            decimal amount = NumberHelper.ParseFormatted(amountStr);
                            if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                            if (string.IsNullOrWhiteSpace(method)) method = "نقدی";

                            int contractId = defaultContractId;
                            if (!string.IsNullOrWhiteSpace(contractNum))
                            {
                                using var cmdC = new SqlCommand("SELECT TOP 1 Id FROM ClientContracts WHERE ContractNumber = @num", conn);
                                cmdC.Parameters.AddWithValue("@num", contractNum);
                                var res = await cmdC.ExecuteScalarAsync();
                                if (res != null && res != DBNull.Value) contractId = Convert.ToInt32(res);
                                else throw new Exception($"قرارداد با شماره «{contractNum}» یافت نشد.");
                            }

                            if (contractId == 0) throw new Exception("شماره قرارداد مشخص نیست (نه در اکسل و نه در فیلتر بالای صفحه).");

                            var trans = new CashTransaction
                            {
                                ContractId = contractId,
                                TransactionDate = date.Value,
                                Amount = amount,
                                TransactionType = method,
                                Direction = DirectionTypes.Incoming,
                                AccountId = targetAcc.Id,
                                AccountTitle = targetAcc.Title,
                                Category = "دریافت از کارفرما",
                                CostType = "خدمات",
                                Description = string.IsNullOrWhiteSpace(desc) ? "ایمپورت از اکسل" : desc
                            };

                            await _cashRepo.AddAsync(trans);

                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"ردیف {rowNum}: {ex.Message}");
                        }
                    }
                }, "در حال ایمپورت دریافتی‌ها از اکسل...");

                if (success && !IsDisposed)
                {
                    string msg = $"✅ تعداد {successCount} ردیف دریافتی با موفقیت ثبت و به موجودی حساب اضافه شد.";
                    if (errorCount > 0)
                    {
                        msg += $"\n\n⚠️ {errorCount} ردیف ثبت نشد:\n" + string.Join("\n", errors.Take(5));
                        if (errors.Count > 5) msg += "\n... و موارد دیگر";
                        ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
                    }
                    else
                    {
                        ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                    }
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientReceiptListForm.BtnImportExcel_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}